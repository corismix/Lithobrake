# Implementation Plan

- [ ] 0.5. Verify Godot 4.4.1 C# API Surface and Performance Characteristics
  - Verify Engine.TimeScale exists and behavior for time warp functionality
  - Verify Engine.PhysicsTicksPerSecond exists for 60Hz physics configuration
  - Test Vector3/Transform3D marshaling performance and document actual overhead
  - Benchmark Double3 ↔ Vector3 conversion performance (target: <0.1ms per 1000 conversions)
  - Document any API differences from Godot documentation or version changes
  - Create performance baseline measurements on target hardware (MacBook Air M4)
  - _Requirements: 1.5, 7.1, 10.6_

- [ ] 1. Setup C#/GDScript integration foundation and validate performance characteristics
  - Create C# test node inheriting from Node3D with basic Update() method
  - Implement Double3 struct for orbital calculations with conversion utilities to Godot.Vector3
  - Build PerformanceMonitor.cs singleton to track frame time, physics time, script time with overlay display
  - Test signal marshaling overhead between C# and GDScript at 60Hz frequency
  - Validate memory management patterns and implement object pooling if needed
  - Document UI performance boundary rules: C# aggregates state, emits one packed signal per frame to GDScript
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7_

- [ ] 2. Configure Godot project structure and C# compilation environment
  - Verify Godot 4.4.1 and .NET 8.0 SDK installation with C# support enabled
  - Create directory structure: src/Core/, src/Scripts/, scenes/, resources/parts/, resources/materials/
  - Configure project.godot settings for Metal renderer, 60Hz physics, Jolt engine, assembly name 'Lithobrake'
  - Create and configure .csproj file with Godot.NET.Sdk/4.4.1 and net8.0 target framework
  - Setup .gitignore for Godot/C# artifacts including .godot/, bin/, obj/, mono_crash patterns
  - Create COORDINATES.md and DETERMINISM.md documentation files
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7_

- [ ] 3. Implement core physics system with Jolt integration
  - Configure Jolt physics engine in project settings with 60Hz fixed timestep and continuous collision detection
  - Create PhysicsManager.cs singleton with FixedDelta constant, PhysicsServer3D reference, and vessel registration
  - Implement PhysicsVessel.cs class with parts list, joints list, mass properties calculation methods
  - Create test scene with single rigid body to verify 60Hz physics tick and measure <1ms overhead
  - Add collision layers configuration and basic rigid body performance testing
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7_

- [ ] 4. Build multi-part vessel system with joint connections
  - Extend PhysicsVessel.cs to handle multiple RigidBody3D parts connected by Joint3D
  - Implement joint system using Generic6DOFJoint3D with JointTuning struct for stiffness/damping parameters
  - Create mass aggregation system to calculate total mass, center of mass, and moment of inertia tensor
  - Add part addition/removal handling with atomic mass property updates
  - Test 10-part vessel structural integrity under 50kN thrust loading
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7_

- [ ] 5. Implement anti-wobble system with dynamic joint stiffening
  - Create AntiWobbleSystem class with Q_ENABLE=12kPa, Q_DISABLE=8kPa thresholds and TAU=0.3s time constant
  - Implement wobble detection based on dynamic pressure and chain length with hysteresis band
  - Build progressive stiffening system with smooth transitions using exponential smoothing, max 5x multiplier
  - Add virtual struts to root for long chains (>5 parts) with automatic removal when conditions improve
  - Test 30-part rocket stack shows no wobble at maximum thrust
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7_

- [ ] 6. Create physics joint separation mechanics (low-level)
  - Implement atomic joint removal between vessel parts at physics system level
  - Apply exactly 500 N·s separation impulse at decoupler position in single frame
  - Handle instant mass redistribution with vessel mass, center of mass, and moment of inertia recalculation
  - Create low-level separation event system with physics state validation
  - Test joint separation 10 times in succession maintains physics stability without glitches
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7_

- [ ] 7. Implement double-precision orbital mechanics system
  - Create OrbitalState.cs struct with double precision orbital elements and epoch field
  - Build CelestialBody.cs with homeworld constants: 600km radius, GM=3.5316e12, from UNIVERSE_CONSTANTS.cs
  - Implement gravity calculation with F=GMm/r² applied as physics force for off-rails, orbital propagation for on-rails
  - Create coordinate transformation methods between orbital elements and Cartesian coordinates with singularity handling
  - Test stable 100km circular orbit with <1% drift over 10 complete orbits
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7_

- [ ] 8. Build centralized Kepler orbit solver and propagation system
  - Create Orbital/Kepler.cs with SolveElliptic() and SolveHyperbolic() methods using Newton-Raphson
  - Implement tolerance 1e-10 and max 16 iterations for both elliptical (e<1) and hyperbolic (e>=1) orbits
  - Add GetPositionAtTime() and GetVelocityAtTime() methods with vis-viva equation for velocity calculation
  - Implement adaptive sampling: dense near Pe/Ap, sparse elsewhere for trajectory visualization
  - Create unit tests for both elliptical and hyperbolic branches with energy conservation validation
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7_

- [ ] 9. Implement floating origin system for precision preservation
  - Create FloatingOriginManager.cs as single owner with OnPreOriginShift, OnOriginShift, OnPostOriginShift events
  - Monitor distance from origin with 20km threshold, preserve velocities during shift
  - Implement IOriginShiftAware interface for subscriber systems to handle origin changes
  - Gate shifts to coast periods only (no thrust, Q < 1 kPa) to prevent physics disruption
  - Test floating origin maintains precision across multiple shifts without visual artifacts
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7_

- [ ] 10. Create basic rocket parts system with initial 3-part catalog
  - Design PartCatalog resource system using JSON format loaded from resources/parts/ with hot-reload support
  - Create Part.cs base class with DryMass, FuelMass, AttachTop, AttachBottom properties and Initialize() method
  - Implement three initial parts: CommandPod (100kg), FuelTank (500kg dry + 4500kg fuel), Engine (250kg, 200kN thrust, 280s Isp)
  - Create mesh loading system for .obj/.glb files with primitive fallback and material application
  - Build test rocket assembly with hardcoded 3-part stack, verify total mass = 5350kg
  - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7_

- [ ] 11. Implement thrust and fuel consumption system
  - Create Engine.cs with GetThrust() method using either thrust curves or mass flow rate with Isp calculation
  - Implement fuel flow system with consumption = thrust / (Isp * g0) and fuel depletion tracking
  - Build fuel tank drainage with tree traversal from bottom to top, crossfeed blocking at decouplers
  - Add throttle control with Z/X keys for 5% steps, Shift+Z for full throttle, Ctrl+X for cutoff
  - Create thrust visualization with vector arrows scaled by thrust and colored by efficiency
  - Implement exhaust effects using CPUParticles3D managed by C# EffectsManager with object pooling for performance
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7_

- [ ] 12. Build atmospheric model and aerodynamic drag system
  - Create Atmosphere class with exponential density model: ρ = 1.225 * exp(-h/7500) with altitude clamps
  - Implement drag calculations: Force = 0.5 * ρ * v² * Cd * A applied opposite to velocity vector
  - Add drag coefficients per part type: streamlined=0.3, blunt=1.0, with fairings=0.2
  - Track dynamic pressure Q = 0.5 * ρ * v² to trigger auto-struts and display in UI
  - Add heating visualization with red glow effect scaled by Q * velocity without damage mechanics
  - Test rocket reaches terminal velocity and drag decreases correctly with altitude
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7_

- [ ] 13. Create flight camera system with smooth following
  - Create FlightCamera.gd in GDScript with smooth follow using lerp, relative offset storage, floating origin handling
  - Implement zoom controls with mouse wheel (5m-1000m range) using exponential scaling and smooth transitions
  - Add rotation controls with right-click drag orbit, maintain during shifts, clamp pitch limits
  - Create camera modes: Chase (behind rocket), Free (mouse control), Orbital (top-down)
  - Enforce zero allocations per frame rule, port to C# if script time exceeds 3ms
  - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7_

- [ ] 14. Build HUD and flight information display system
  - Create FlightHUD.gd with altitude, velocity, fuel percentage, and mission timer displays
  - Implement navball as 3D sphere with texture, prograde/retrograde markers, and horizon indicator
  - Add velocity readouts for surface velocity, orbital velocity, and vertical speed
  - Create resource gauges for fuel percentage, electric charge, and monopropellant
  - Implement staging display showing current stage, next action, and delta-v remaining
  - Add assertion Debug.Assert(scripts_ms <= 3) in HUD update to enforce performance budget
  - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7_

- [ ] 15. Implement staging management and UI system (high-level)
  - Create StageManager.cs with stage list structure, current stage tracking, and activation queue
  - Implement Decoupler part class that coordinates with Task 6 physics separation mechanics
  - Build staging UI with vertical stage list, current stage highlight, and part icons per stage
  - Add activation controls with spacebar trigger, safety lock toggle, no warp restriction
  - Implement auto-staging with fuel depletion detection, 0.5s delay timer, toggle enable/disable
  - Coordinate high-level staging logic with low-level physics separation from Task 6
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7_
  - _Dependencies: Task 6 (physics joint separation mechanics)_

- [ ] 16. Create Vehicle Assembly Building (VAB) interface
  - Create VAB scene layout with part palette (left), build area (center), info panel (right)
  - Build part palette with category tabs, scrollable lists, and part icons
  - Implement attachment system with node detection, green/red indicators, snap to nodes
  - Add grid snapping with 0.1m position snap, 15° rotation snap, toggle with G key
  - Implement save/load with JSON vessel format, named saves, quicksave (⌘S/Ctrl+S)
  - Add rotation controls Q/E, W/S, A/D axes with 15° snap default, reset with R
  - Build launch transition with vessel validation, conversion to PhysicsVessel, load flight scene
  - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7_

- [ ] 17. Implement time warp system with on-rails orbital propagation
  - Create TimeWarpManager.cs with warp levels: 1x, 10x, 100x, 1000x, 10000x using verified Godot 4.4.1 time scaling API
  - Use centralized Kepler solver from Orbital/Kepler.cs for on-rails propagation (NO DUPLICATION)
  - Build state transitions to save physics state, switch to rails, restore on exit
  - Add warp restrictions: no thrust allowed, altitude limits, SOI boundaries
  - Create UI controls with comma/period keys, visual indicator, warning messages
  - Handle edge cases: SOI transitions, hyperbolic orbits, ground collision prevention, freeze FX on rails entry
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7_
  - _Dependencies: Task 0.5 (API verification)_

- [ ] 18. Build map view and orbital trajectory visualization
  - Create MapView scene with 3D viewport setup, camera pivot system, transparent background
  - Implement orbit rendering with blue current orbit, dotted predictions, Ap/Pe markers
  - Add celestial bodies: homeworld sphere, moon at 10Mm, atmosphere edge visualization
  - Build camera controls with scroll zoom, click-drag rotate, focus switching
  - Create info panel with orbital parameters, real-time updates, color coding
  - Implement view switching with M key toggle, smooth transition, state preservation
  - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7_

- [ ] 19. Create maneuver node system for trajectory planning
  - Create ManeuverNode.cs with time of burn, delta-v components, predicted orbit calculation
  - Build placement UI with click orbit to place, time calculation, visual marker
  - Implement delta-v handles with 6 directional gizmos, drag sensitivity, numerical display
  - Create orbit prediction that applies impulse, recalculates elements, renders prediction
  - Add navball integration with maneuver marker, burn direction, time to node
  - Implement auto-execute: point to node, auto-burn at T-0, cut at delta-v completion
  - Handle node editing with fine adjustment, delete nodes, multiple nodes support
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7_

- [ ] 20. Implement SAS and automated attitude control system
  - Create SAS modes: stability assist, prograde/retrograde, normal/antinormal
  - Implement PID controller with derivative filtering, torque saturation scaled with MoI, configurable gains per part type
  - Add reaction wheels with torque generation, power consumption, saturation mechanics
  - Create control indicators with SAS mode display, input visualization, lock indicators
  - Implement RCS system with thruster blocks, translation control, fuel consumption
  - Add pilot input with WASD rotation, IJKL translation, override SAS capability
  - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7_

- [ ] 21. Build comprehensive save/load system with orbital state persistence
  - Design save format with JSON structure, schema versioning, version field for migration support
  - Implement vessel serialization with part tree, resource states, physics state
  - Save orbital state with both orbital elements AND state vectors, Universal Time epoch, reference frame tag
  - Handle game settings: difficulty, UI preferences, key bindings
  - Create save UI with named saves, quicksave (F5), autosave functionality
  - Implement load system with file browser, validation, error recovery
  - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.7_

- [ ] 22. Expand part catalog to 10 parts for design variety
  - Create part configs: Small tank (100kg + 900kg fuel), Large tank (1000kg + 9000kg fuel), Vacuum engine (60kN, 350s Isp), SRB (150kN, 30s burn)
  - Implement special behaviors: SRB timed burn, vacuum Isp curves, fin control authority
  - Create 3D models using primitives or simple meshes with consistent scale and attachment nodes
  - Update VAB palette to add parts to categories, create icons, test instantiation
  - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7_

- [ ] 23. Implement moon transfer and patched conics system
  - Add Moon physics with gravity well, 2000km SOI, surface visual representation
  - Implement patched conics with SOI transitions, multi-body prediction, transfer planning
  - Create encounter detection with closest approach calculation, SOI entry/exit, time to encounter
  - Add transfer UI showing phase angle, transfer window, delta-v required
  - Handle reference switching with parent body change, coordinate transform, smooth transition
  - Test Moon encounters: flyby trajectories, capture orbits, return transfers
  - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7_

- [ ] 24. Performance optimization and CI validation system
  - Profile performance to identify bottlenecks, memory analysis, draw call count
  - Optimize physics with LOD for distant parts, sleep optimization, batch updates
  - Improve rendering with mesh instancing, texture atlasing, culling
  - Optimize scripts by caching calculations, reducing allocations, pooling objects
  - Implement Performance CI Gate: automated 75-part test scene for 60 seconds, fail if Physics >5ms OR Scripts >3ms
  - Add orbital drift test: <1% error over 10 orbits at 100km, hyperbolic test for escape trajectories
  - Validate conversion performance meets <0.1ms per 1000 operations target from Task 0.5
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7_
  - _Dependencies: Task 0.5 (API verification and performance baseline)_

- [ ] 25. Polish gameplay experience and add quality features
  - Tune physics parameters: joint stiffness, drag coefficients, thrust curves
  - Polish VAB experience with part descriptions, build aids, validation
  - Improve flight feel with camera smoothing, control response, audio feedback
  - Add quality features: undo/redo, part search, preset loads
  - Create achievements: first orbit, moon encounter, speed records
  - Add sound effects: engine audio, staging sounds, UI feedback
  - Implement settings: graphics options, control config, difficulty
  - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 10.6, 10.7_

- [ ] 26. Create minimal tutorial and help system
  - Design tutorial flow: VAB basics, launch sequence, orbit achievement
  - Create hint system with contextual tips, progress tracking, dismissible
  - Build help screens: control reference, part guide, orbital basics
  - Add tooltips for UI elements, part info, navball
  - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7_

- [ ] 27. Final testing and release preparation
  - Complete test suite: all features work, no crashes, save compatibility
  - Fix critical bugs: physics glitches, UI issues, data loss
  - Create builds for macOS (primary), Windows, Linux
  - Write documentation: README, controls guide, known issues
  - Prepare release: version 1.0, GitHub release, itch.io page
  - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 10.6, 10.7_