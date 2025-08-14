# Design Document

## Overview

Lithobrake is architected as a hybrid C#/GDScript system leveraging Godot 4.4.1's strengths while maintaining strict performance requirements. The design emphasizes separation of concerns with C# handling performance-critical physics and orbital calculations, while GDScript manages UI and gameplay logic. The system uses a deterministic execution order and careful precision management to ensure both accuracy and performance.

The core architecture follows a layered approach:
- **Physics Layer**: Jolt physics engine with custom vessel management
- **Orbital Layer**: Double-precision Kepler mechanics with floating origin
- **Gameplay Layer**: Part systems, staging, and flight controls  
- **Presentation Layer**: UI, camera, and visual effects

## Architecture

### System Boundaries and Precision Management

The design establishes clear boundaries between different precision domains:

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Orbital Math  │    │  Physics Sim    │    │   Rendering     │
│  (Double/C#)    │◄──►│  (Float/C#)     │◄──►│ (Float/GDScript)│
│                 │    │                 │    │                 │
│ • Kepler solver │    │ • Jolt physics  │    │ • Camera system │
│ • State vectors │    │ • Joint forces  │    │ • Visual FX     │
│ • Time warp     │    │ • Rigid bodies  │    │ • UI overlays   │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

**Conversion Rules:**
- Orbital calculations use `Double3` struct for position/velocity
- Physics uses Godot's `Vector3` (float precision)
- Conversions only occur at system boundaries
- Floating origin shifts preserve relative precision

### Execution Order Contract

The system enforces deterministic execution order every frame:

1. **Input Processing** (GDScript): Collect player input, UI events
2. **Physics Update** (C#): Jolt physics, joint forces, collision
3. **Orbital Update** (C#): Gravity, trajectory calculation, time warp
4. **Render Update** (GDScript): Camera positioning, visual effects
5. **UI Update** (GDScript): HUD, navball, information displays

This order ensures consistent state transitions and prevents race conditions between systems.

### Determinism Contract

The system enforces strict determinism requirements for reproducible simulation:

**Fixed Execution Order:** Input → Physics → Orbital → Render → UI (never varies)
**Time Ownership:** PhysicsManager owns simulation time advancement
**Physics Tick:** Fixed 60Hz external tick rate (16.67ms intervals)
**Solver Iterations:** Variable per tick for stability (more iterations under stress)
**Precision Boundaries:** Double for orbital calculations, float for physics/rendering
**Conversions:** Only at defined system boundaries with <0.1ms overhead per 1000 operations
**Random Sources:** Controlled seed management for deterministic testing and replay

### Performance Budget Allocation

```
Total Frame Budget: 16.67ms (60 FPS)
├── Physics: ≤5ms (30%)
│   ├── Jolt simulation: ~3ms
│   ├── Joint calculations: ~1ms
│   └── Collision detection: ~1ms
├── Scripts: ≤3ms (18%)
│   ├── C# orbital math: ~1.5ms
│   ├── GDScript UI: ~1ms
│   └── Input processing: ~0.5ms
├── Rendering: ≤8ms (48%)
│   ├── Scene rendering: ~6ms
│   ├── UI rendering: ~1.5ms
│   └── Effects: ~0.5ms
└── System overhead: ~0.67ms (4%)
```

### Performance Monitoring System

Real-time performance tracking and validation system:

**PerformanceMonitor.cs** - Central performance tracking
```csharp
public class PerformanceMonitor : Node {
    private readonly MovingAverage physicsTime = new(60);
    private readonly MovingAverage scriptsTime = new(60);
    private readonly MovingAverage renderTime = new(60);
    
    public void RecordFrameTiming(float physics, float scripts, float render) {
        physicsTime.Add(physics);
        scriptsTime.Add(scripts);
        renderTime.Add(render);
        
        // Hard assertions for CI
        Debug.Assert(physics <= 5.0f, "Physics exceeded 5ms budget");
        Debug.Assert(scripts <= 3.0f, "Scripts exceeded 3ms budget");
    }
    
    public PerformanceReport GenerateReport() {
        return new PerformanceReport {
            PhysicsAvg = physicsTime.Average,
            ScriptsAvg = scriptsTime.Average,
            RenderAvg = renderTime.Average,
            FrameRate = Engine.GetFramesPerSecond()
        };
    }
}
```

**Memory Management Strategy:**
- Object pooling for frequently allocated objects (particles, physics bodies)
- GC pressure target: <10MB allocation per second
- Reference cleanup on scene transitions
- Asset streaming for large resources

### Particle System Architecture

**EffectsManager.cs** - Centralized particle system management
```csharp
public class EffectsManager : Node {
    private ObjectPool<CPUParticles3D> exhaustPool;
    private List<ActiveEffect> activeEffects;
    
    public void CreateExhaustEffect(Vector3 position, float throttle, float pressure) {
        var particles = exhaustPool.Get();
        // Configure particle properties based on throttle and atmospheric pressure
        // Pooled management keeps within 3ms script budget
        particles.Amount = (int)(throttle * 100 * (1.0f - pressure * 0.001f));
        particles.GlobalPosition = position;
        activeEffects.Add(new ActiveEffect(particles, 2.0f)); // 2s lifetime
    }
    
    public override void _Process(double delta) {
        // Update and cleanup effects within performance budget
        UpdateActiveEffects((float)delta);
    }
}
```

**Performance Strategy:**
- C# management of CPUParticles3D for script budget compliance
- Object pooling prevents allocation spikes
- Lifetime management with automatic cleanup
- Throttle-based particle count scaling

## Components and Interfaces

### Core Physics System

**PhysicsManager.cs** - Central physics coordination
```csharp
public class PhysicsManager : Node {
    public const float FixedDelta = 1.0f / 60.0f;
    private PhysicsServer3D physicsServer;
    private List<PhysicsVessel> activeVessels;
    
    public void RegisterVessel(PhysicsVessel vessel);
    public void UpdatePhysics(float delta);
    private void ApplyAntiWobble();
}
```

**PhysicsVessel.cs** - Multi-part vessel management
```csharp
public class PhysicsVessel : Node3D {
    private List<RigidBody3D> parts;
    private List<Joint3D> joints;
    private JointTuning jointSettings;
    
    public Vector3 CenterOfMass { get; private set; }
    public float TotalMass { get; private set; }
    public Matrix3 InertiaTensor { get; private set; }
    
    public void RecalculateMassProperties();
    public void ApplyStiffnessMultiplier(float multiplier);
    public void StageParts(List<Part> partsToSeparate);
}
```

**Anti-Wobble System** - Dynamic joint stiffening
```csharp
public class AntiWobbleSystem {
    private const float Q_ENABLE = 12000f;   // 12 kPa
    private const float Q_DISABLE = 8000f;   // 8 kPa (hysteresis)
    private const float TAU = 0.3f;          // Time constant
    
    public float CalculateStiffnessMultiplier(float dynamicPressure, int chainLength) {
        // Smooth transitions with hysteresis
        // Returns 1.0x to 5.0x multiplier
    }
}
```

### Orbital Mechanics System

**OrbitalState.cs** - Double-precision orbital elements
```csharp
public struct OrbitalState {
    public double SemiMajorAxis;
    public double Eccentricity;
    public double Inclination;
    public double LongitudeOfAscendingNode;
    public double ArgumentOfPeriapsis;
    public double TrueAnomaly;
    public double Epoch;  // Universal Time
    
    public (Double3 position, Double3 velocity) ToStateVectors();
    public static OrbitalState FromStateVectors(Double3 pos, Double3 vel);
}
```

**Kepler.cs** - Centralized orbit solver
```csharp
public static class Kepler {
    public static (double E, int iterations) SolveElliptic(double M, double e, double tolerance = 1e-10);
    public static (double F, int iterations) SolveHyperbolic(double M, double e, double tolerance = 1e-10);
    public static Double3 GetPositionAtTime(OrbitalState orbit, double time);
    public static Double3 GetVelocityAtTime(OrbitalState orbit, double time);
}
```

**FloatingOriginManager.cs** - Precision preservation
```csharp
public class FloatingOriginManager : Node {
    public event Action OnPreOriginShift;
    public event Action<Vector3> OnOriginShift;
    public event Action OnPostOriginShift;
    
    private const float SHIFT_THRESHOLD = 20000f; // 20km
    
    public void CheckAndShift(Vector3 playerPosition) {
        // Only shift during coast periods
        if (CanShift(playerPosition)) {
            ExecuteShift(-playerPosition);
        }
    }
}
```

### Part and Vessel System

**Part.cs** - Base part class
```csharp
public abstract class Part : Node3D {
    public float DryMass { get; protected set; }
    public float FuelMass { get; protected set; }
    public Vector3 AttachTop { get; protected set; }
    public Vector3 AttachBottom { get; protected set; }
    public PartDefinition Definition { get; private set; }
    
    public abstract void Initialize(PartDefinition def);
    public virtual void UpdatePart(float delta);
    public virtual float GetMass() => DryMass + FuelMass;
}
```

**Engine.cs** - Propulsion system
```csharp
public class Engine : Part {
    public float MaxThrust { get; private set; }
    public float SeaLevelIsp { get; private set; }
    public float VacuumIsp { get; private set; }
    public float CurrentThrottle { get; set; }
    
    public float GetThrust(float throttle, float pressure) {
        float isp = Mathf.Lerp(SeaLevelIsp, VacuumIsp, GetIspCurve(pressure));
        float massFlow = (MaxThrust / (VacuumIsp * 9.81f)) * throttle;
        return massFlow * isp * 9.81f;
    }
    
    public float ConsumeFuel(float thrust, float delta) {
        return (thrust / (GetCurrentIsp() * 9.81f)) * delta;
    }
}
```

**StageManager.cs** - Staging system
```csharp
public class StageManager : Node {
    private List<Stage> stages;
    private int currentStage;
    
    public void ExecuteStaging() {
        var stage = stages[currentStage];
        foreach (var decoupler in stage.Decouplers) {
            decoupler.Separate(); // Applies 500 N·s impulse
        }
        RecalculateVesselProperties();
        currentStage++;
    }
}
```

### Time Management System

**TimeWarpManager.cs** - Time acceleration
```csharp
public class TimeWarpManager : Node {
    private readonly float[] warpLevels = { 1f, 10f, 100f, 1000f, 10000f };
    private int currentWarpIndex = 0;
    private bool onRails = false;
    
    public void SetTimeWarp(int level) {
        if (CanWarp()) {
            Engine.TimeScale = warpLevels[level];
            if (level > 1) EnterOnRails();
            else ExitOnRails();
        }
    }
    
    private void PropagateOnRails(double deltaTime) {
        // Use centralized Kepler solver - NO DUPLICATION
        var newState = Kepler.PropagateOrbit(currentOrbit, deltaTime);
        UpdateVesselPosition(newState);
    }
    
    private bool CanWarp() {
        // Verified Godot 4.4.1 C# API - Engine.TimeScale for warp
        // Engine.PhysicsTicksPerSecond for physics frequency (60Hz)
        return !vessel.IsThrusting && dynamicPressure < 1000f;
    }
}
```

### User Interface Architecture

**FlightCamera.gd** - Camera system (GDScript for UI integration)
```gdscript
extends Camera3D
class_name FlightCamera

var target_vessel: Node3D
var zoom_distance: float = 50.0
var follow_smoothing: float = 5.0

func _process(delta):
    if target_vessel:
        follow_vessel(delta)
        handle_input(delta)
    
    # CRITICAL: Zero allocations per frame
    # If script time > 3ms, port to C#
```

**HUD.gd** - Flight information display
```gdscript
extends Control
class_name FlightHUD

@onready var altitude_label: Label = $AltitudeLabel
@onready var velocity_label: Label = $VelocityLabel
@onready var fuel_bar: ProgressBar = $FuelBar

func update_displays(vessel_data: Dictionary):
    # Receive pre-calculated data from C#
    # Single signal per frame with packed data
    altitude_label.text = "ALT: %.0f m" % vessel_data.altitude
    velocity_label.text = "VEL: %.1f m/s" % vessel_data.velocity
    fuel_bar.value = vessel_data.fuel_percentage
```

## Data Models

### Universe Constants
All celestial parameters locked in UNIVERSE_CONSTANTS.cs for consistency:
```csharp
public static class UniverseConstants {
    public const double HOMEWORLD_RADIUS = 600000.0;   // 600 km (Kerbin-scale)
    public const double HOMEWORLD_GM = 3.5316e12;      // m³/s² (gravitational parameter)
    public const double STANDARD_GRAVITY = 9.81;       // m/s² (surface gravity)
    public const double ATMOSPHERE_HEIGHT = 70000.0;   // 70 km (atmosphere boundary)
    public const double SCALE_HEIGHT = 7500.0;         // 7.5 km (exponential atmosphere)
    public const double SEA_LEVEL_DENSITY = 1.225;     // kg/m³ (atmospheric density)
}
```

### Part Definition Format
```json
{
  "name": "LV-T30 Engine",
  "category": "engines",
  "mass": 1.25,
  "cost": 1100,
  "maxThrust": 215000,
  "seaLevelIsp": 270,
  "vacuumIsp": 320,
  "attachNodes": {
    "top": [0, 0.5, 0],
    "bottom": [0, -0.5, 0]
  },
  "dragCoefficient": 0.3,
  "meshPath": "res://models/engines/lv-t30.glb"
}
```

### Save File Schema
```json
{
  "version": "1.0",
  "schemaVersion": 1,
  "timestamp": "2025-01-14T10:30:00Z",
  "universalTime": 12345.67,
  "vessels": [
    {
      "name": "Rocket 1",
      "parts": [...],
      "orbitalState": {
        "semiMajorAxis": 700000.0,
        "eccentricity": 0.05,
        "inclination": 0.0,
        "epoch": 12345.67
      },
      "stateVectors": {
        "position": [0, 700000, 0],
        "velocity": [2400, 0, 0]
      }
    }
  ]
}
```

**Save File Versioning Strategy:**
- Schema version tracks data structure changes for migration support
- Version field tracks game version for compatibility warnings
- Both orbital elements AND state vectors saved for redundancy and validation
- Universal Time epoch ensures deterministic time reference

## Error Handling

### Physics Stability
- **Joint Failure**: Detect excessive forces, apply emergency stiffening
- **NaN Detection**: Monitor for invalid physics states, reset to last valid state
- **Collision Overflow**: Limit collision calculations per frame
- **Memory Pressure**: Implement object pooling for frequently created objects

### Orbital Calculation Robustness
- **Singularity Handling**: Special cases for circular/equatorial orbits
- **Convergence Failure**: Fallback to approximate solutions after max iterations
- **Hyperbolic Validation**: Verify escape trajectories reach correct v_infinity
- **Time Precision**: Use high-precision time representation to avoid drift

### Save System Reliability
- **Schema Validation**: Verify save file structure before loading
- **Corruption Detection**: Checksums for critical data
- **Migration Support**: Handle version upgrades gracefully
- **Backup Strategy**: Automatic backup before overwriting saves

## Testing Strategy

### Performance Validation
```csharp
public class PerformanceTests {
    [Test]
    public void Physics_75Parts_Under5ms() {
        var vessel = Create75PartVessel();
        var stopwatch = Stopwatch.StartNew();
        
        for (int i = 0; i < 60; i++) {
            physicsManager.UpdatePhysics(1/60f);
        }
        
        var avgTime = stopwatch.ElapsedMilliseconds / 60.0;
        Assert.That(avgTime, Is.LessThan(5.0));
    }
    
    [Test]
    public void Orbital_10Orbits_Under1PercentDrift() {
        var initialOrbit = new OrbitalState { /* 100km circular */ };
        var finalOrbit = PropagateOrbits(initialOrbit, 10);
        
        var driftPercent = CalculateDrift(initialOrbit, finalOrbit);
        Assert.That(driftPercent, Is.LessThan(1.0));
    }
}
```

### Integration Testing
- **Cross-Language Marshaling**: Verify C#/GDScript signal performance
- **Floating Origin**: Test precision preservation across multiple shifts
- **Save/Load Integrity**: Round-trip testing with complex vessel states
- **Time Warp Accuracy**: Validate orbital propagation at all warp levels

### User Experience Testing
- **30-Minute Orbit**: New player tutorial completion time
- **Smile Test**: Subjective fun factor evaluation
- **Control Responsiveness**: Input lag measurement (<50ms)
- **Visual Clarity**: UI readability at different resolutions

This design provides a solid foundation for building Lithobrake while maintaining the strict performance requirements and ensuring accurate orbital mechanics simulation.