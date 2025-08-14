# Lithobrake Development Tasks

A comprehensive task list for building a simplified, performance-focused rocket simulation game in Godot 4.4.1.

**Core Constraints:**
- 60 FPS on MacBook Air M4 (non-negotiable)
- 75 part limit per vessel
- Physics budget: ≤5ms
- Rendering budget: ≤8ms
- Scripts budget: ≤3ms

**Universe Parameters:** 
- **LOCKED TO KERBIN SCALE**: 600km radius, μ=3.5316e12 m³/s²
- All constants in `UNIVERSE_CONSTANTS.cs`:
  - Radius: 600 km
  - GM: 3.5316e12 m³/s²
  - g0: 9.81 m/s² (standard gravity)
  - Atmosphere height: 70 km
  - Scale height: 7.5 km
  - Sea level density: 1.225 kg/m³

**Determinism Contract:**
- Fixed update order: Input → Physics → Orbital → Render → UI
- Physics tick: Fixed 60Hz (16.67ms)
- Double precision: Orbital calculations and time
- Float precision: Rendering and UI
- Conversions: Only at system boundaries

---

## Phase 0: Foundation & Validation

### ⬜ Task 0: C#/GDScript Integration Validation [NEW - CRITICAL]
**Priority:** CRITICAL | **Effort:** S | **Dependencies:** None

Verify cross-language performance characteristics before building complex systems.

#### Subtasks:
1. **⬜ Create simple C# test node**
   - Inherit from Node3D in C#
   - Add basic Update() method with counter
   - Attach to test scene
   - Verify compilation and execution

2. **⬜ Test Vector3 type conversions**
   - Create Double3 struct for orbital calculations
   - Create conversion utilities between Double3 and Godot.Vector3
   - Benchmark conversion overhead (target: <0.01ms per 1000 conversions)
   - **Policy:** All orbital math uses double/Double3, convert only at boundaries

3. **⬜ Implement performance counter system**
   - Create PerformanceMonitor.cs singleton
   - Track frame time, physics time, script time
   - Display overlay with ms timing
   - Verify stays within budget (16.6ms total)

4. **⬜ Test signal marshaling overhead**
   - Create C# signals connected to GDScript
   - Measure marshaling cost per signal
   - Test with high-frequency updates (60Hz)
   - Document acceptable signal frequency limits

5. **⬜ Validate memory management**
   - Test C# garbage collection impact
   - Monitor memory allocation patterns
   - Implement object pooling if needed
   - Document memory best practices

6. **⬜ Establish UI performance boundary**
   - Document rule: No per-frame heavy math in GDScript
   - C# aggregates state → emit one packed signal per frame
   - Test signal batching for HUD/Map updates
   - Keep within 3ms script budget

**Test Strategy:** Run stress test with 100 C# nodes updating at 60Hz, verify <3ms script time

---

### ⬜ Task 1: Setup C# Support and Project Structure
**Priority:** HIGH | **Effort:** M | **Dependencies:** Task 0

Configure Godot 4.4.1 project with C# support and establish folder structure.

#### Subtasks:
1. **⬜ Verify Godot 4.4.1 and .NET 8.0 SDK installation**
   - Confirm Godot has Mono/C# support enabled
   - Check `dotnet --version` returns 8.0+
   - Test simple C# script compilation
   - Document macOS-specific workarounds

2. **⬜ Create project directory structure**
   ```
   src/Core/        # C# physics and orbital systems
   src/Scripts/     # GDScript gameplay logic  
   scenes/          # Godot scene files
   resources/parts/ # Part definitions
   resources/materials/ # Shaders and materials
   ```

3. **⬜ Configure project.godot settings**
   - Set assembly name to 'Lithobrake'
   - Configure Metal renderer for macOS
   - Set `physics/common/physics_ticks_per_second = 60`
   - Enable Jolt physics engine
   - Configure default gravity

4. **⬜ Create and configure .csproj file**
   ```xml
   <Project Sdk="Godot.NET.Sdk/4.4.1">
     <PropertyGroup>
       <TargetFramework>net8.0</TargetFramework>
       <AssemblyName>Lithobrake</AssemblyName>
     </PropertyGroup>
     <!-- No System.Numerics needed - using Double3 for orbital math -->
   </Project>
   ```

5. **⬜ Setup .gitignore for Godot/C# artifacts**
   - Add .godot/, *.tmp, .import/
   - Add bin/, obj/, *.user
   - Add mono_crash.* patterns
   - Test with `git status`

6. **⬜ Task 1.6: Coordinate System Documentation** [NEW]
   - Define world space vs vessel space
   - Document inertial vs rotating frames
   - Specify orbital element conventions
   - Create COORDINATES.md reference

7. **⬜ Task 1.7: Determinism Contract Documentation** [NEW]
   - Fixed execution order specification
   - Time ownership and advancement rules
   - Double/float boundary policies
   - Create determinism.md (1 page)

**Test Strategy:** Project compiles, runs at 60 FPS, C# scripts execute correctly

---

## Phase 1: Core Systems (Parallel Development)

### ⬜ Task 2A: Basic Jolt Physics Integration [SPLIT FROM TASK 2]
**Priority:** HIGH | **Effort:** M | **Dependencies:** Task 1

Set up Jolt physics engine with basic configuration.

#### Subtasks:
1. **⬜ Configure Jolt physics in Godot**
   - Enable Jolt in project settings
   - Set 60Hz fixed timestep (16.67ms)
   - Configure continuous collision detection
   - Set up collision layers

2. **⬜ Create PhysicsManager.cs singleton**
   ```csharp
   public class PhysicsManager : Node {
       public const float FixedDelta = 1.0f / 60.0f;
       private PhysicsServer3D physicsServer;
       
       public override void _Ready() {
           physicsServer = PhysicsServer3D.Singleton;
           // Configure conservative settings
       }
   }
   ```

3. **⬜ Test single rigid body performance**
   - Create test scene with single part
   - Verify physics tick at exactly 60Hz
   - Measure physics time (<1ms for single body)
   - Test collision response

**Test Strategy:** Single rigid body maintains stable physics at 60Hz with <1ms overhead

---

### ⬜ Task 2B: Multi-part Vessels & Joints [SPLIT FROM TASK 2]
**Priority:** HIGH | **Effort:** L | **Dependencies:** Task 2A

Implement vessel system with multiple connected parts.

#### Subtasks:
1. **⬜ Create PhysicsVessel.cs class**
   ```csharp
   public class PhysicsVessel : Node3D {
       private List<RigidBody3D> parts;
       private List<Joint3D> joints;
       public Vector3 CenterOfMass { get; private set; }
       public float TotalMass { get; private set; }
   }
   ```

2. **⬜ Implement joint system for part connections**
   - Use Generic6DOFJoint3D for connections
   - **Data-driven JointTuning struct:**
     - Default: stiffness=10000, damping=100, pos_iters=12, vel_iters=10
     - High stress (Q>12kPa or chain>5): stiffness=30000, damping=300, pos_iters=20, vel_iters=16
   - Consider 120Hz substep only when high stress detected
   - Wire into anti-wobble system for automatic adjustment
   - Test joint stability with tall stacks

3. **⬜ Add mass aggregation and CoM tracking**
   - Calculate total mass from all parts
   - Update center of mass each physics tick
   - Track moment of inertia tensor
   - Handle part addition/removal

**Test Strategy:** 10-part vessel maintains structural integrity under 50kN thrust

---

### ⬜ Task 2C: Anti-Wobble System [SPLIT FROM TASK 2]
**Priority:** HIGH | **Effort:** M | **Dependencies:** Task 2B

Prevent physics instability in tall rocket stacks.

#### Subtasks:
1. **⬜ Implement wobble detection**
   ```csharp
   // Specific thresholds with hysteresis
   const float Q_ENABLE = 12000;  // 12 kPa - enable stiffening
   const float Q_DISABLE = 8000;   // 8 kPa - disable (hysteresis)
   const float TAU = 0.3f;         // Time constant for smooth transitions
   
   if (chainLength > 5 || dynamicPressure > Q_ENABLE) {
       targetStiffnessMultiplier = 5.0f;  // Max 5x baseline
   } else if (dynamicPressure < Q_DISABLE) {
       targetStiffnessMultiplier = 1.0f;  // Return to baseline
   }
   
   // Smooth transition using time constant
   float alpha = 1.0f - Mathf.Exp(-dt / TAU);
   currentStiffness += (targetStiffnessMultiplier - currentStiffness) * alpha;
   ```

2. **⬜ Create progressive stiffening system**
   - Smooth stiffness transitions (no sudden jumps)
   - Hysteresis band: 12 kPa enable, 8 kPa disable
   - Time-constant based smoothing: τ = 0.3s (not 20% per tick)
   - Max multiplier: 5x baseline stiffness
   - Higher solver iterations only when Q > 12 kPa or chainLength > 5

3. **⬜ Add virtual struts to root**
   - Auto-strut long chains to root part
   - Invisible constraints for stability
   - Remove when conditions improve

**Test Strategy:** 30-part rocket shows no wobble at max thrust

---

### ⬜ Task 2D: Staging Physics & Separation [SPLIT FROM TASK 2]
**Priority:** HIGH | **Effort:** M | **Dependencies:** Task 2B

Handle part separation and staging events.

#### Subtasks:
1. **⬜ Implement clean separation logic**
   - Remove joints between stages
   - Apply separation impulse: 500 N·s (impulse, not force)
   - Applied at decoupler position as single impulse
   - Update vessel properties atomically same frame

2. **⬜ Handle mass redistribution**
   - Recalculate vessel mass instantly
   - Update CoM and MoI
   - Maintain physics stability

3. **⬜ Create staging event system**
   - Pre-staging validation
   - Atomic staging execution  
   - Post-staging cleanup
   - Event notifications

**Test Strategy:** Staging 10 times in succession maintains physics stability

---

### ⬜ Task 2.5: Floating Origin Threshold Testing [NEW]
**Priority:** MEDIUM | **Effort:** S | **Dependencies:** Task 2D

Determine optimal floating origin threshold.

#### Subtasks:
1. **⬜ Test 5km threshold**
   - Measure shift frequency during ascent
   - Check for visual artifacts
   - Monitor performance impact

2. **⬜ Test 20km threshold**
   - Compare precision at distance
   - Verify physics stability
   - Check particle system behavior

3. **⬜ Document final decision**
   - Choose threshold based on testing (recommend 20km)
   - Update FloatingOrigin.cs
   - Add shift gating logic if needed

4. **⬜ Implement floating origin event system**
   ```csharp
   public class FloatingOriginManager : Node {
       public event Action OnPreOriginShift;   // Freeze/prepare
       public event Action<Vector3> OnOriginShift;  // Execute shift
       public event Action OnPostOriginShift;  // Thaw/resume
       
       void ShiftOrigin(Vector3 offset) {
           // Only shift during coast (no thrust, Q < 1 kPa)
           if (vessel.IsThrusting || dynamicPressure > 1000) return;
           
           OnPreOriginShift?.Invoke();     // Systems freeze state
           OnOriginShift?.Invoke(offset);  // Execute shift
           OnPostOriginShift?.Invoke();    // Systems resume
       }
   }
   ```

---

### ⬜ Task 3: Implement Gravity and Basic Orbital Mechanics
**Priority:** HIGH | **Effort:** L | **Dependencies:** Task 1 (NOT Task 2!)

Create double-precision orbital physics system.

#### Subtasks:
1. **⬜ Create OrbitalState.cs with double precision**
   ```csharp
   public class OrbitalState {
       public double SemiMajorAxis;
       public double Eccentricity;
       public double Inclination;
       public double LongitudeOfAscendingNode;
       public double ArgumentOfPeriapsis;
       public double TrueAnomaly;
   }
   ```

2. **⬜ Implement gravity calculation**
   - Point gravity at planet center
   - F = GMm/r² with GM = 3.5316e12 (Kerbin)
   - **Off-rails (atmosphere/low alt):** Apply as physics force
   - **On-rails (time warp):** No rigidbody forces, use orbital propagation

3. **⬜ Build Kepler orbit solver**
   - Create Orbital/Kepler.cs with ALL propagation logic
   - Newton-Raphson with tolerance 1e-10 and max 16 iterations
   - SolveElliptic() for e < 1, SolveHyperbolic() for e >= 1
   - ALL other systems call this - NO DUPLICATES
   - Include unit tests for both branches

4. **⬜ Implement orbit propagation**
   - GetPositionAtTime(t) method
   - Vis-viva equation for velocity
   - Cache frequently accessed positions
   - **Adaptive sampling:** Dense near Pe/Ap, sparse elsewhere

5. **⬜ Create floating origin system**
   - **FloatingOriginManager is SINGLE OWNER** - all others subscribe
   - Monitor distance from origin (20km threshold)
   - Preserve velocities during shift
   - Implement IOriginShiftAware interface for subscribers
   - Gate shifts to coast periods (no thrust, Q < 1 kPa)

6. **⬜ Build CelestialBody.cs**
   - Homeworld: 600km radius (Kerbin)
   - Calculate surface gravity
   - SOI calculation for future
   - All constants from UNIVERSE_CONSTANTS.cs

7. **⬜ Implement coordinate transformations**
   - OrbitalToCartesian conversions
   - CartesianToOrbital conversions
   - Handle singularities

8. **⬜ Test orbital accuracy**
   - Verify 1% accuracy over 10 orbits
   - Test circular and elliptical orbits
   - Validate energy conservation

**Test Strategy:** Achieve stable 100km circular orbit with <1% drift over 10 orbits

---

## Phase 2: Flight Mechanics

### ⬜ Task 4: Create Basic Rocket Parts System
**Priority:** HIGH | **Effort:** M | **Dependencies:** Task 2B

Implement initial 3-part rocket for proof of concept.

#### Subtasks:
1. **⬜ Design PartCatalog resource system**
   - JSON/tres format for definitions
   - Load from resources/parts/
   - Validate part data
   - Support hot-reload

2. **⬜ Create Part.cs base class**
   ```csharp
   public abstract class Part : Node3D {
       public float DryMass;
       public float FuelMass;
       public Vector3 AttachTop;
       public Vector3 AttachBottom;
       
       public abstract void Initialize(PartDefinition def);
   }
   ```

3. **⬜ Implement three initial parts**
   - CommandPod: 100kg, no thrust
   - FuelTank: 500kg dry + 4500kg fuel
   - Engine: 250kg, 200kN thrust, 280s Isp

4. **⬜ Create mesh loading system**
   - Load .obj/.glb from resources
   - Generate primitives as fallback
   - Apply materials by type

5. **⬜ Build test rocket assembly**
   - Hardcode 3-part stack
   - Verify mass = 5350kg total
   - Test attachment logic
   - **Note:** 200kN thrust gives TWR ≈ 3.8 (chaos mode)
   - **Realistic preset:** 85kN for TWR ≈ 1.5 (good ascent profile)

**Test Strategy:** 3-part rocket assembles correctly with proper mass calculations

---

### ⬜ Task 5: Implement Thrust and Fuel System
**Priority:** HIGH | **Effort:** M | **Dependencies:** Task 4

Create propulsion system with realistic physics.

#### Subtasks:
1. **⬜ Create Engine.cs with thrust calculation**
   ```csharp
   // FIXED: Use proper thrust model
   public float GetThrust(float throttle, float pressure) {
       // Option A: Thrust curve
       float thrust = ThrustCurve.Evaluate(pressure) * throttle;
       
       // Option B: Mass flow
       float mdot = MaxMassFlow * throttle;
       float isp = GetIsp(pressure);
       float thrust = mdot * g0 * isp;
       
       return thrust;
   }
   ```

2. **⬜ Implement fuel flow system**
   - Consumption = thrust / (Isp * g0)
   - Track fuel consumed
   - Stop thrust when empty

3. **⬜ Build fuel tank drainage**
   - Find connected tanks through tree traversal
   - Drain in priority order (bottom to top)
   - Handle crossfeed with decoupler blocking
   - Enforce tree-based flow (no cycles)
   - Log warning if stage has orphaned engines

4. **⬜ Add throttle control**
   - Z/X for up/down (5% steps)
   - Shift+Z for full throttle
   - Ctrl+X for cutoff

5. **⬜ Create thrust visualization**
   - Vector arrows scaled by thrust
   - Color by efficiency
   - Gimbal range display

6. **⬜ Implement exhaust effects**
   - ⚠️ **FIXED**: Use CPUParticles3D (not 2D!)
   - Scale with throttle
   - Adjust for pressure

**Test Strategy:** Fuel consumption matches Tsiolkovsky equation predictions

---

### ⬜ Task 6: Build Simple Flight Camera System
**Priority:** MEDIUM | **Effort:** M | **Dependencies:** Task 2A

Camera system that follows rocket smoothly.

#### Subtasks:
1. **⬜ Create FlightCamera.gd**
   - Smooth follow with lerp
   - Store relative offset
   - Handle floating origin
   - **ENFORCE:** Zero allocations per frame
   - **If >3ms script time:** Port to C#

2. **⬜ Implement zoom controls**
   - Mouse wheel zoom (5m-1000m)
   - Exponential scaling
   - Smooth transitions

3. **⬜ Add rotation controls**
   - Right-click drag orbit
   - Maintain during shifts
   - Clamp pitch limits

4. **⬜ Create camera modes**
   - Chase (behind rocket)
   - Free (mouse control)
   - Orbital (top-down)

5. **⬜ Build HUD overlay**
   - Altitude display
   - Velocity readout
   - Fuel percentage

**Test Strategy:** Camera follows smoothly with no jumps during origin shifts

---

### ⬜ Task 7: Create Staging System
**Priority:** HIGH | **Effort:** M | **Dependencies:** Task 5

Implement rocket staging with clean separation.

#### Subtasks:
1. **⬜ Create StageManager.cs**
   - Stage list structure
   - Current stage tracking
   - Activation queue

2. **⬜ Implement Decoupler part**
   - Break joint on activation
   - Apply 500 N·s separation impulse (same as Task 2D)
   - Applied at joint anchor as single impulse
   - Disconnect crossfeed

3. **⬜ Build staging UI**
   - Vertical stage list
   - Current stage highlight
   - Part icons per stage

4. **⬜ Add activation controls**
   - Spacebar triggers stage
   - Safety lock toggle
   - No warp restriction

5. **⬜ Implement auto-staging**
   - Detect fuel depletion
   - 0.5s delay timer
   - Toggle enable/disable

6. **⬜ Handle physics updates**
   - Instant mass recalc
   - Update CoM and MoI
   - Smooth transitions

**Test Strategy:** 5-stage rocket separates cleanly without physics glitches

---

### ⬜ Task 8: Implement Simple Aerodynamics
**Priority:** MEDIUM | **Effort:** M | **Dependencies:** Task 3

Add atmospheric drag for realistic flight.

#### Subtasks:
1. **⬜ Create atmosphere model**
   ```csharp
   public class Atmosphere {
       // Exponential density model with safety clamps
       public float GetDensity(float altitude) {
           if (altitude > 70000) return 0f;  // Clamp to avoid denormals
           return 1.225f * Mathf.Exp(-altitude / 7500f);
       }
       
       // Terminal velocity check for CI
       public float GetTerminalVelocity(float mass, float dragCoef, float area, float altitude) {
           float density = GetDensity(altitude);
           return Mathf.Sqrt((2 * mass * 9.81f) / (density * dragCoef * area));
       }
   }
   ```

2. **⬜ Implement drag calculations**
   - Force = 0.5 * ρ * v² * Cd * A
   - Apply opposite to velocity
   - Per-part calculation

3. **⬜ Add drag coefficients**
   - Streamlined: 0.3
   - Blunt: 1.0  
   - With fairings: 0.2

4. **⬜ Track dynamic pressure**
   - Q = 0.5 * ρ * v²
   - Trigger auto-struts at threshold
   - Display in UI

5. **⬜ Add heating visualization**
   - Simple red glow effect
   - Scale with Q * velocity
   - No damage mechanics

**Test Strategy:** Rocket reaches terminal velocity, drag decreases with altitude

---

## Phase 3: User Interface

### ⬜ Task 9: Build Basic VAB (Simplified)
**Priority:** HIGH | **Effort:** L | **Dependencies:** Task 4

Simple rocket assembly interface (stack-only for MVP).

#### Subtasks:
1. **⬜ Create VAB scene layout**
   - Part palette (left)
   - Build area (center)
   - Info panel (right)

2. **⬜ Build part palette**
   - Category tabs
   - Scrollable lists
   - Part icons

3. **⬜ Implement attachment system**
   - Node detection
   - Green/red indicators
   - Snap to nodes

4. **⬜ Add grid snapping**
   - 0.1m position snap
   - 15° rotation snap
   - Toggle with G key

5. **⬜ ~~Symmetry modes~~ [REMOVED - Post-MVP]**
   - Stack-only assembly for now
   - No radial/mirror symmetry

6. **⬜ Implement save/load**
   - JSON vessel format
   - Named saves
   - Quicksave (⌘S on macOS, Ctrl+S on Windows/Linux)

7. **⬜ Add rotation controls**
   - Q/E, W/S, A/D axes
   - 15° snap default
   - Reset with R

8. **⬜ Build launch transition**
   - Validate vessel
   - Convert to PhysicsVessel
   - Load flight scene

**Test Strategy:** Build and save 10-part rocket, launch successfully

---

### ⬜ Task 12: Create Map View and Orbital Display
**Priority:** HIGH | **Effort:** L | **Dependencies:** Task 11

Orbital visualization for trajectory planning.

#### Subtasks:
1. **⬜ Create MapView scene**
   - 3D viewport setup
   - Camera pivot system
   - Transparent background

2. **⬜ Implement orbit rendering**
   - Blue current orbit
   - Dotted predictions
   - Ap/Pe markers

3. **⬜ Add celestial bodies**
   - Homeworld sphere
   - Moon at 10Mm
   - Atmosphere edge

4. **⬜ Build camera controls**
   - Scroll zoom
   - Click-drag rotate
   - Focus switching

5. **⬜ Create info panel**
   - Orbital parameters
   - Real-time updates
   - Color coding

6. **⬜ Implement view switching**
   - M key toggle
   - Smooth transition
   - State preservation

**Test Strategy:** Orbit display matches actual trajectory within 1%

---

### ⬜ Task 14: Build HUD and Flight UI
**Priority:** MEDIUM | **Effort:** M | **Dependencies:** Task 6

Essential flight information display.

#### Subtasks:
1. **⬜ Create navball**
   - 3D sphere with texture
   - Prograde/retrograde markers
   - Horizon indicator

2. **⬜ Build altitude displays**
   - ASL altitude
   - Terrain altitude  
   - Apoapsis/Periapsis

3. **⬜ Add velocity readouts**
   - Surface velocity
   - Orbital velocity
   - Vertical speed

4. **⬜ Create resource gauges**
   - Fuel percentage
   - Electric charge
   - Monopropellant

5. **⬜ Implement staging display**
   - Current stage
   - Next action
   - Delta-v remaining (POST-MVP: VAB delta-v/TWR readout)

6. **⬜ Add mission timer**
   - T+ from launch
   - Universal time
   - Time to node
   - **Add assertion:** `Debug.Assert(scripts_ms <= 3)` in HUD update

**Test Strategy:** All displays update correctly at 60 FPS

---

## Phase 4: Gameplay Systems

### ⬜ Task 10: Expand Part Catalog to 10 Parts
**Priority:** MEDIUM | **Effort:** M | **Dependencies:** Task 9

Add variety for different rocket designs.

#### Subtasks:
1. **⬜ Create part configs**
   - Small tank: 100kg + 900kg fuel
   - Large tank: 1000kg + 9000kg fuel
   - Vacuum engine: 60kN, 350s Isp
   - SRB: 150kN, 30s burn

2. **⬜ Implement special behaviors**
   - SRB timed burn
   - Vacuum Isp curves
   - Fin control authority

3. **⬜ Create 3D models**
   - Primitives or simple meshes
   - Consistent scale
   - Attachment nodes

4. **⬜ Update VAB palette**
   - Add to categories
   - Create icons
   - Test instantiation

**Test Strategy:** Each part works correctly with appropriate physics

---

### ⬜ Task 11: Implement Time Warp System
**Priority:** HIGH | **Effort:** L | **Dependencies:** Task 3, Task 7

Time acceleration for orbital transfers.

#### Subtasks:
1. **⬜ Create TimeWarpManager.cs**
   - Warp levels: 1x, 10x, 100x, 1000x, 10000x
   - ⚠️ **FIXED**: Use Engine.TimeScale (not Time.timeScale)
   - State management

2. **⬜ Use Kepler solver from Orbital system**
   ```csharp
   // Call the centralized solver from Orbital/Kepler.cs
   // DO NOT duplicate solver code here
   public void PropagateOnRails(double deltaTime) {
       var (E, iters) = Kepler.SolveElliptic(meanAnomaly, eccentricity);
       // Use solution for position calculation
   }
   ```

3. **⬜ Build state transitions**
   - Save physics state
   - Switch to rails
   - Restore on exit

4. **⬜ Add warp restrictions**
   - No thrust allowed
   - Altitude limits
   - SOI boundaries

5. **⬜ Create UI controls**
   - Comma/period keys
   - Visual indicator
   - Warning messages

6. **⬜ Handle edge cases and freeze checklist**
   - SOI transitions with reference frame swap
   - Hyperbolic orbits for escape trajectories
   - Ground collision prevention
   - **Time-warp freeze list:** 
     - Disable exhaust/heat FX on rails entry
     - Freeze particle trails
     - Switch navball data source
     - Re-enable all on rails exit

**Test Strategy:** Warp through 10 orbits with <1% position error

---

### ⬜ Task 13: Implement Maneuver Node System
**Priority:** MEDIUM | **Effort:** L | **Dependencies:** Task 12

Orbital maneuver planning tools.

#### Subtasks:
1. **⬜ Create ManeuverNode.cs**
   - Time of burn
   - Delta-v components
   - Predicted orbit

2. **⬜ Build placement UI**
   - Click orbit to place
   - Time calculation
   - Visual marker

3. **⬜ Implement delta-v handles**
   - 6 directional gizmos
   - Drag sensitivity
   - Numerical display

4. **⬜ Create orbit prediction**
   - Apply impulse
   - Recalculate elements
   - Render prediction

5. **⬜ Add navball integration**
   - Maneuver marker
   - Burn direction
   - Time to node

6. **⬜ Implement auto-execute**
   - Point to node
   - Auto-burn at T-0
   - Cut at delta-v

7. **⬜ Handle node editing**
   - Fine adjustment
   - Delete nodes
   - Multiple nodes

**Test Strategy:** Circularization node achieves <5% eccentricity

---

### ⬜ Task 15: Add Basic SAS and Control System
**Priority:** MEDIUM | **Effort:** M | **Dependencies:** Task 5, Task 14

Automated attitude control for stability.

#### Subtasks:
1. **⬜ Create SAS modes**
   - Stability assist
   - Prograde/Retrograde
   - Normal/Antinormal

2. **⬜ Implement PID controller**
   - Tuned for rocket control
   - Add derivative filtering (1st-order low-pass on D-term)
   - Torque saturation scaled with MoI
   - Configurable gains per part type (reaction wheels vs RCS)

3. **⬜ Add reaction wheels**
   - Torque generation
   - Power consumption
   - Saturation mechanics

4. **⬜ Create control indicators**
   - SAS mode display
   - Input visualization
   - Lock indicators

5. **⬜ Implement RCS system**
   - Thruster blocks
   - Translation control
   - Fuel consumption

6. **⬜ Add pilot input**
   - WASD rotation
   - IJKL translation
   - Override SAS

**Test Strategy:** SAS holds attitude within 1° for 60 seconds

---

### ⬜ Task 16: Implement Save/Load System
**Priority:** MEDIUM | **Effort:** M | **Dependencies:** Task 3, Task 11

Persistent game state across sessions.

#### Subtasks:
1. **⬜ Design save format**
   - JSON structure with schema versioning
   - Version field for migration support
   - Compression (optional gzip)

2. **⬜ Implement vessel serialization**
   - Part tree
   - Resource states
   - Physics state

3. **⬜ Save orbital state with epoch**
   - Orbital elements AND state vectors
   - **Universal Time (UT) epoch** for time-warp accuracy
   - Reference frame tag (inertial vs planet-fixed)
   - Version save schema from day one

4. **⬜ Handle game settings**
   - Difficulty
   - UI preferences
   - Key bindings

5. **⬜ Create save UI**
   - Named saves
   - Quicksave (F5)
   - Autosave

6. **⬜ Implement load system**
   - File browser
   - Validation
   - Error recovery

**Test Strategy:** Save/load preserves exact orbital state

---

## Phase 5: Content & Polish

### ⬜ Task 17: Expand Parts to 20 (Simplified)
**Priority:** LOW | **Effort:** M | **Dependencies:** Task 10

Final part roster for variety.

#### Subtasks:
1. **⬜ Add structural parts**
   - Adapters
   - Struts
   - Decouplers

2. **⬜ Create utility parts**
   - Solar panels
   - Batteries
   - Antennas

3. **⬜ ~~Advanced engines~~ [SIMPLIFIED]**
   - Keep to 3-4 engine types
   - No complex clusters

4. **⬜ Update VAB**
   - Organize categories
   - Search function
   - Part info

**Test Strategy:** All parts integrate without issues

---

### ⬜ Task 19: Polish Core Gameplay Loop
**Priority:** MEDIUM | **Effort:** L | **Dependencies:** Task 18

Refine the moment-to-moment experience.

#### Subtasks:
1. **⬜ Tune physics parameters**
   - Joint stiffness
   - Drag coefficients
   - Thrust curves

2. **⬜ Polish VAB experience**
   - Part descriptions
   - Build aids
   - Validation

3. **⬜ Improve flight feel**
   - Camera smoothing
   - Control response
   - Audio feedback

4. **⬜ Add quality features**
   - Undo/redo
   - Part search
   - Preset loads

5. **⬜ Create achievements**
   - First orbit
   - Moon encounter
   - Speed records

6. **⬜ Add sound effects**
   - Engine audio
   - Staging sounds
   - UI feedback

7. **⬜ Implement settings**
   - Graphics options
   - Control config
   - Difficulty

**Test Strategy:** "Smile test" - is it fun to play?

---

### ⬜ Task 20: Create Minimal Tutorial
**Priority:** LOW | **Effort:** S | **Dependencies:** Task 19

Basic onboarding for new players.

#### Subtasks:
1. **⬜ Design tutorial flow**
   - VAB basics
   - Launch sequence
   - Orbit achievement

2. **⬜ Create hint system**
   - Contextual tips
   - Progress tracking
   - Dismissible

3. **⬜ Build help screens**
   - Control reference
   - Part guide
   - Orbital basics

4. **⬜ Add tooltips**
   - UI elements
   - Part info
   - Navball

**Test Strategy:** New player reaches orbit within 30 minutes

---

### ⬜ Task 21: Implement Moon Transfer
**Priority:** LOW | **Effort:** M | **Dependencies:** Task 13

Advanced gameplay goal.

#### Subtasks:
1. **⬜ Add Moon physics**
   - Gravity well
   - SOI at 2000km
   - Surface (visual)

2. **⬜ Implement patched conics**
   - SOI transitions
   - Multi-body prediction
   - Transfer planning

3. **⬜ Create encounter detection**
   - Closest approach
   - SOI entry/exit
   - Time to encounter

4. **⬜ Add transfer UI**
   - Phase angle
   - Transfer window
   - Delta-v required

5. **⬜ Handle reference switching**
   - Parent body change
   - Coordinate transform
   - Smooth transition

6. **⬜ Test Moon encounters**
   - Flyby trajectories
   - Capture orbits
   - Return transfers

**Test Strategy:** Achieve Moon encounter with <10% delta-v overhead

---

## Phase 6: Optimization & Release

### ⬜ Task 18: Performance Optimization Pass
**Priority:** HIGH | **Effort:** M | **Dependencies:** Task 17

Ensure 60 FPS with full features.

#### Subtasks:
1. **⬜ Profile performance**
   - Identify bottlenecks
   - Memory analysis
   - Draw call count

2. **⬜ Optimize physics**
   - LOD for distant parts
   - Sleep optimization
   - Batch updates

3. **⬜ Improve rendering**
   - Mesh instancing
   - Texture atlasing
   - Culling

4. **⬜ Optimize scripts**
   - Cache calculations
   - Reduce allocations
   - Pool objects

5. **⬜ Memory management**
   - Asset streaming
   - Garbage collection
   - Reference cleanup

6. **⬜ Final validation**
   - 75-part test scene
   - All features on
   - Maintain 60 FPS
   - **Performance targets:** ≤2k draw calls, ≤150k tris

7. **⬜ Implement Performance CI Gate**
   - Automated test scene: 75-part stack for 60 seconds
   - **FAIL CI if:** Physics avg > 5ms OR Scripts avg > 3ms
   - **Orbital drift test:** <1% error over 10 orbits at 100km
   - **Hyperbolic test:** Escape trajectory reaches v_infinity correctly
   - Run on every commit to main
   - Generate performance report

**Test Strategy:** 75-part vessel maintains 60 FPS in all scenarios

---

### ⬜ Task 22: Final Testing and Release
**Priority:** MEDIUM | **Effort:** M | **Dependencies:** Task 19, Task 20

Prepare for distribution.

#### Subtasks:
1. **⬜ Complete test suite**
   - All features work
   - No crashes
   - Save compatibility

2. **⬜ Fix critical bugs**
   - Physics glitches
   - UI issues
   - Data loss

3. **⬜ Create builds**
   - macOS (primary)
   - Windows
   - Linux

4. **⬜ Write documentation**
   - README
   - Controls guide
   - Known issues

5. **⬜ Prepare release**
   - Version 1.0
   - GitHub release
   - itch.io page

**Test Strategy:** Complete playthrough without critical issues

---

## Performance Targets

Must maintain across all scenarios:
- **Physics:** ≤5ms per frame
- **Rendering:** ≤8ms per frame  
- **Scripts:** ≤3ms per frame
- **Total:** ≤16.6ms (60 FPS)

## Risk Mitigations

⚠️ **Critical Risks:**
1. **Type friction** (System.Numerics vs Godot) - Address in Task 0
2. **Physics wobble** - Progressive stiffening in Task 2C
3. **Floating origin** - Test thoroughly in Task 2.5
4. **Thrust math** - Use proper model in Task 5

## Success Metrics

- **Week 1:** On-rails orbital loop proven (100 km, <1% drift).
- **Month 1:** Core gameplay loop complete (Phase 1-3)
- **Month 3:** Shippable or valuable learning achieved

---

*Generated from Lithobrake project requirements. Focus on fun > realism, performance > features.*