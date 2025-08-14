# determinism.md - Lithobrake Deterministic Simulation Contract

This document defines the determinism requirements for reproducible rocket simulation across all platforms and sessions.

## Core Determinism Principles

**Fixed Execution Order**: Every frame follows identical system update sequence
**Consistent Timestep**: Physics operates at exactly 60Hz with no variation
**Precision Boundaries**: Clear separation between double-precision orbital and float-precision physics
**Controlled Randomness**: All random number generation uses managed seeds for reproducibility

## Fixed Update Order Contract

The simulation enforces this execution order every frame:

```
1. Input Processing (GDScript)
   - Collect player input events
   - Process UI interactions
   - Queue control commands

2. Physics Update (C#)
   - Jolt physics simulation at 60Hz
   - Joint force calculations
   - Collision detection and response
   - Anti-wobble system updates

3. Orbital Update (C#)
   - Gravity force application
   - Trajectory calculations
   - Time warp propagation
   - Floating origin management

4. Render Update (GDScript)
   - Camera positioning and movement
   - Visual effects updates
   - Particle system management

5. UI Update (GDScript)
   - HUD information display
   - Navball orientation
   - Control input visualization
```

**Violation Prevention**: This order never varies and is enforced by system architecture.

## Time Ownership and Advancement

### Primary Time Owner
**PhysicsManager.cs** owns simulation time advancement:
- Advances time at fixed 16.67ms intervals (60Hz)
- Provides authoritative time reference for all systems
- Manages time warp state transitions

### Time Warp Behavior
- **1x Speed**: Real-time physics simulation with full force calculations
- **>1x Speed**: On-rails orbital propagation using centralized Kepler solver
- **Transition**: Seamless state save/restore without time drift

### Time Precision
- **Universal Time (UT)**: Double precision for orbital calculations and save files
- **Frame Delta**: Float precision for rendering and UI (16.67ms fixed)
- **Epoch Management**: All orbital states reference common UT epoch

## Precision Boundaries and Conversions

### Domain Separation
```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Orbital Math  │    │  Physics Sim    │    │   Rendering     │
│  (Double/C#)    │◄──►│  (Float/C#)     │◄──►│ (Float/GDScript)│
│                 │    │                 │    │                 │
│ • OrbitalState  │    │ • RigidBody3D   │    │ • Camera3D      │
│ • Kepler solver │    │ • Joint forces  │    │ • Visual FX     │
│ • Time warp     │    │ • Collisions    │    │ • UI rendering  │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

### Conversion Rules
- **Orbital → Physics**: Double3 → Vector3 at gravity calculation boundary
- **Physics → Rendering**: Vector3 direct pass-through (no conversion)
- **Frequency**: Conversions occur only at defined system boundaries
- **Performance**: <0.1ms overhead per 1000 conversions (validated requirement)

### Floating Origin Coordination
- **Threshold**: 20km distance from origin triggers shift consideration
- **Timing**: Shifts only during coast periods (no thrust, Q < 1kPa)
- **Synchronization**: All systems receive shift events in deterministic order
- **Precision**: Relative positions preserved with double-precision accuracy

## Input Sampling and Processing

### Input Collection
- **Frequency**: Input sampled once per frame at start of update cycle
- **Buffering**: Input events queued and processed in arrival order
- **Timing**: No input polling outside of designated input phase

### Control Response
- **Latency**: Control inputs applied in same frame they are received
- **Interpolation**: No input smoothing or prediction that could vary
- **State**: Control state persists until explicitly changed

## Random Number Generation

### Seed Management
```csharp
public static class DeterministicRandom {
    private static System.Random physics_rng = new(12345);
    private static System.Random gameplay_rng = new(67890);
    
    public static void SetSeed(int physicsSeed, int gameplaySeed) {
        physics_rng = new System.Random(physicsSeed);
        gameplay_rng = new System.Random(gameplaySeed);
    }
}
```

### Usage Rules
- **Physics RNG**: Joint noise, collision variation, micro-perturbations
- **Gameplay RNG**: Part failure rates, random events, cosmetic effects
- **Prohibited**: No usage of global Random.Shared or time-based seeds
- **Save State**: RNG seeds saved with game state for replay consistency

## Floating Origin System

### Shift Triggers
- **Distance Check**: Player position exceeds 20km from origin
- **Safety Gates**: No thrust active AND dynamic pressure < 1kPa
- **Frequency Limit**: Maximum one shift per second to prevent thrashing

### Shift Execution
```csharp
public void ExecuteOriginShift(Vector3 offset) {
    OnPreOriginShift?.Invoke();     // Systems prepare for shift
    
    // Atomic position updates in deterministic order
    UpdatePhysicsBodies(offset);
    UpdateOrbitalPositions(offset);
    UpdateCameraPositions(offset);
    UpdateParticleEffects(offset);
    
    OnPostOriginShift?.Invoke();    // Systems resume normal operation
}
```

### Coordinate Preservation
- **Physics**: All RigidBody3D positions shifted by exact offset
- **Orbital**: State vectors updated with double-precision arithmetic
- **Visual**: Camera and effect positions maintain relative accuracy
- **Validation**: Post-shift verification ensures no precision loss

## Physics Determinism

### Solver Configuration
- **Engine**: Jolt physics with fixed configuration parameters
- **Timestep**: Exactly 1/60 second (16.67ms) with no variation
- **Iterations**: Variable solver iterations for stability (higher under stress)
- **Threading**: Single-threaded physics for deterministic results

### Joint Stability
- **Anti-Wobble**: Progressive stiffening based on dynamic pressure and chain length
- **Hysteresis**: Enable at 12kPa, disable at 8kPa with 0.3s time constant
- **Transitions**: Smooth interpolation prevents sudden behavior changes

### Collision Handling
- **Layers**: Fixed collision layer configuration
- **Response**: Consistent contact resolution parameters
- **Callbacks**: Deterministic collision event order

## Save File Determinism

### State Persistence
```json
{
  "determinism": {
    "physics_seed": 12345,
    "gameplay_seed": 67890,
    "universal_time": 12345.67890123,
    "physics_tick_count": 740740,
    "floating_origin": [0, 0, 0]
  },
  "vessels": [...],
  "orbital_states": [...]
}
```

### Load Validation
- **Seed Restoration**: RNG states restored to exact save values
- **Time Synchronization**: UT and tick count validated for consistency
- **State Verification**: Orbital elements and physics state cross-checked
- **Precision Check**: Double-precision values compared with epsilon tolerance

## Testing and Validation

### Determinism Tests
- **Replay Test**: Save state, advance 1000 frames, compare with loaded equivalent
- **Platform Test**: Identical simulation results across macOS/Windows/Linux
- **Precision Test**: 10-orbit simulation maintains <1% orbital drift
- **Performance Test**: All determinism overhead fits within performance budgets

### CI Integration
- **Automated**: Determinism tests run on every commit
- **Cross-Platform**: Tests execute on all target platforms
- **Regression Detection**: Any determinism break fails CI pipeline
- **Performance Monitoring**: Determinism overhead measured and tracked

## Error Handling

### Determinism Violations
- **Detection**: Automated detection of non-deterministic behavior
- **Logging**: Detailed logging of violation source and context
- **Recovery**: Fallback to last known good state when possible
- **Reporting**: Clear error messages for debugging determinism issues

### Common Pitfalls
- **Avoided**: Time.time, DateTime.Now, threading, uncontrolled Random usage
- **Monitored**: Physics parameter drift, precision loss, order violations
- **Tested**: Edge cases like floating origin shifts, time warp transitions
- **Documented**: All potential sources of non-determinism identified

---

This determinism contract ensures Lithobrake provides consistent, reproducible simulation behavior essential for reliable physics simulation and gameplay experience.