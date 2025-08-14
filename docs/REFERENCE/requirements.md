# Requirements Document

## Introduction

Lithobrake is a simplified, performance-focused rocket simulation game built in Godot 4.4.1 with C# support. The game emphasizes realistic orbital mechanics while maintaining strict performance constraints to ensure smooth 60 FPS gameplay on MacBook Air M4. Players will design rockets in a Vehicle Assembly Building (VAB), launch them, and achieve orbital flight with accurate physics simulation.

The project prioritizes fun gameplay over complete realism, with a focus on the core gameplay loop of building, launching, and flying rockets to orbit. The simulation uses double-precision orbital mechanics for accuracy while maintaining performance through careful system design and floating-origin techniques.

## Requirements

### Requirement 1: Performance and Technical Foundation

**User Story:** As a player, I want the game to run smoothly at 60 FPS on my MacBook Air M4, so that I can enjoy fluid gameplay without stuttering or lag.

#### Acceptance Criteria

1. WHEN the game is running with a 75-part vessel THEN the total frame time SHALL be ≤16.6ms (60 FPS)
2. WHEN physics calculations are performed THEN physics time SHALL be ≤5ms per frame
3. WHEN rendering is performed THEN rendering time SHALL be ≤8ms per frame
4. WHEN scripts are executed THEN script execution time SHALL be ≤3ms per frame
5. WHEN C# and GDScript systems interact THEN type conversion overhead SHALL be <0.1ms per 1000 conversions
6. WHEN the performance monitoring system is active THEN it SHALL display real-time frame timing with ms precision
7. WHEN memory allocation occurs THEN garbage collection impact SHALL not cause frame drops below 60 FPS

### Requirement 2: Physics and Vessel Simulation

**User Story:** As a player, I want to build multi-part rockets that behave realistically under physics simulation, so that I can experience authentic rocket flight dynamics.

#### Acceptance Criteria

1. WHEN a vessel has multiple parts connected by joints THEN the joints SHALL maintain structural integrity under 50kN thrust
2. WHEN a rocket stack exceeds 5 parts in length OR dynamic pressure exceeds 12 kPa THEN the anti-wobble system SHALL automatically increase joint stiffness up to 5x baseline
3. WHEN joint stiffness changes occur THEN transitions SHALL be smooth using time constant τ = 0.3s with hysteresis (enable at 12 kPa, disable at 8 kPa)
4. WHEN staging occurs THEN separation SHALL apply exactly 500 N·s impulse at the decoupler position in a single frame
5. WHEN parts are separated THEN vessel mass, center of mass, and moment of inertia SHALL be recalculated atomically in the same frame
6. WHEN the vessel part count reaches 75 parts (performance target limit) THEN physics simulation SHALL still maintain ≤5ms per frame
7. WHEN Jolt physics engine is used THEN it SHALL operate at exactly 60Hz fixed timestep

### Requirement 3: Orbital Mechanics and Coordinate Systems

**User Story:** As a player, I want accurate orbital mechanics that allow me to plan and execute realistic space missions, so that I can learn real orbital dynamics while playing.

#### Acceptance Criteria

1. WHEN orbital calculations are performed THEN they SHALL use double precision with GM = 3.5316e12 m³/s² for Kerbin-scale planet
2. WHEN a vessel is in a 100km circular orbit THEN orbital drift SHALL be <1% over 10 complete orbits
3. WHEN Kepler orbit solving is required THEN Newton-Raphson method SHALL converge with tolerance 1e-10 and maximum 16 iterations
4. WHEN floating origin shifts occur THEN they SHALL only happen during coast periods (no thrust, Q < 1 kPa) with 20km threshold
5. WHEN coordinate transformations occur THEN conversions between orbital elements and Cartesian coordinates SHALL handle singularities gracefully
6. WHEN time warp is active THEN orbital propagation SHALL use centralized Kepler solver with no duplicate implementations
7. WHEN hyperbolic trajectories are calculated THEN the system SHALL support both elliptical (e < 1) and hyperbolic (e ≥ 1) orbits

### Requirement 4: Propulsion and Flight Systems

**User Story:** As a player, I want realistic rocket engines with proper thrust, fuel consumption, and atmospheric effects, so that I can experience authentic rocket performance characteristics.

#### Acceptance Criteria

1. WHEN an engine is firing THEN thrust SHALL be calculated using either thrust curves or mass flow rate with Isp
2. WHEN fuel is consumed THEN consumption rate SHALL equal thrust / (Isp * g0) where g0 = 9.81 m/s²
3. WHEN fuel tanks are connected THEN fuel flow SHALL follow tree traversal from bottom to top with crossfeed blocking at decouplers
4. WHEN atmospheric drag is calculated THEN force SHALL equal 0.5 * ρ * v² * Cd * A with exponential atmosphere model
5. WHEN altitude exceeds 70km THEN atmospheric density SHALL be zero (space environment)
6. WHEN dynamic pressure is calculated THEN Q = 0.5 * ρ * v² SHALL trigger structural reinforcement systems
7. WHEN exhaust effects are displayed THEN CPUParticles3D SHALL be managed by C# systems and scaled with throttle and atmospheric pressure

### Requirement 5: Vehicle Assembly and Part System

**User Story:** As a player, I want to build custom rockets from a variety of parts in an intuitive assembly interface, so that I can design vehicles suited to my mission requirements.

#### Acceptance Criteria

1. WHEN the VAB is opened THEN it SHALL display part palette, build area, and info panel in organized layout
2. WHEN parts are attached THEN they SHALL snap to attachment nodes with green/red visual indicators
3. WHEN a part is selected THEN its mass, fuel capacity, and performance characteristics SHALL be displayed
4. WHEN a vessel is saved THEN it SHALL use JSON format with schema versioning for future compatibility
5. WHEN the initial part catalog is loaded THEN it SHALL include exactly 3 parts: CommandPod (100kg), FuelTank (500kg dry + 4500kg fuel), Engine (250kg, 200kN thrust, 280s Isp)
6. WHEN the part catalog is expanded THEN it SHALL grow to 10 parts, then 20 parts in subsequent phases
7. WHEN a vessel is validated for launch THEN it SHALL check for fuel flow paths and warn about orphaned engines

### Requirement 6: Flight Interface and Controls

**User Story:** As a player, I want intuitive flight controls and clear information displays, so that I can effectively pilot my rockets and monitor their status.

#### Acceptance Criteria

1. WHEN flying a rocket THEN the camera SHALL smoothly follow the vessel with configurable zoom (5m-1000m range)
2. WHEN the HUD is displayed THEN it SHALL show altitude, velocity, fuel percentage, and mission timer with <3ms script execution time
3. WHEN the navball is displayed THEN it SHALL show 3D attitude with prograde/retrograde markers and horizon indicator
4. WHEN throttle controls are used THEN Z/X keys SHALL adjust throttle in 5% steps with Shift+Z for full throttle
5. WHEN staging is triggered THEN spacebar SHALL activate current stage with optional auto-staging on fuel depletion
6. WHEN SAS is enabled THEN it SHALL provide stability assist, prograde/retrograde, and normal/antinormal modes
7. WHEN map view is activated THEN M key SHALL toggle orbital display with real-time trajectory visualization

### Requirement 7: Time Management and Orbital Transfers

**User Story:** As a player, I want time acceleration capabilities for long orbital transfers and maneuver planning tools, so that I can efficiently execute complex space missions.

#### Acceptance Criteria

1. WHEN time warp is activated THEN it SHALL provide levels: 1x, 10x, 100x, 1000x, 10000x using verified Godot 4.4.1 C# time scaling API
2. WHEN on-rails time warp is active THEN physics forces SHALL be disabled and orbital propagation SHALL use Kepler solver
3. WHEN time warp restrictions apply THEN warp SHALL be disabled during thrust, low altitude, or SOI transitions
4. WHEN maneuver nodes are placed THEN they SHALL show delta-v components with 6-directional gizmos for adjustment
5. WHEN maneuver execution occurs THEN auto-burn SHALL point to node and cut thrust when delta-v is achieved
6. WHEN orbit prediction is calculated THEN it SHALL show trajectory changes in real-time as nodes are adjusted
7. WHEN circularization maneuvers are executed THEN resulting eccentricity SHALL be <5% of target

### Requirement 8: Save System and Persistence

**User Story:** As a player, I want to save my progress and resume flights later, so that I can continue complex missions across multiple play sessions.

#### Acceptance Criteria

1. WHEN a game is saved THEN it SHALL use JSON format with schema versioning and optional gzip compression
2. WHEN orbital state is saved THEN it SHALL include both orbital elements AND state vectors with Universal Time epoch
3. WHEN vessel state is saved THEN it SHALL preserve part tree, resource states, and physics state completely
4. WHEN quicksave is used THEN F5 key SHALL save current state with automatic naming
5. WHEN a save is loaded THEN orbital position SHALL be restored with exact accuracy (no drift)
6. WHEN save validation occurs THEN corrupted files SHALL be detected with graceful error recovery
7. WHEN autosave is enabled THEN it SHALL occur at regular intervals without interrupting gameplay

### Requirement 9: Advanced Gameplay Features

**User Story:** As a player, I want advanced features like moon transfers and achievement systems, so that I have long-term goals and challenging missions to pursue.

#### Acceptance Criteria

1. WHEN the moon is implemented THEN it SHALL have gravity well, 2000km SOI, and visual surface representation
2. WHEN patched conics are calculated THEN SOI transitions SHALL smoothly switch reference frames
3. WHEN encounter detection occurs THEN closest approach and SOI entry/exit times SHALL be calculated accurately
4. WHEN transfer windows are displayed THEN phase angles and delta-v requirements SHALL be shown
5. WHEN achievements are earned THEN they SHALL include first orbit, moon encounter, and speed records
6. WHEN tutorial is provided THEN new players SHALL reach orbit within 30 minutes of starting
7. WHEN the complete gameplay loop is tested THEN players SHALL find it engaging and fun ("smile test")

### Requirement 10: Quality and Polish

**User Story:** As a player, I want a polished, bug-free experience with good audio-visual feedback, so that the game feels professional and enjoyable to play.

#### Acceptance Criteria

1. WHEN sound effects are played THEN they SHALL include engine audio, staging sounds, and UI feedback
2. WHEN graphics settings are adjusted THEN they SHALL maintain 60 FPS performance target
3. WHEN the final test suite runs THEN all features SHALL work without crashes or data loss
4. WHEN builds are created THEN they SHALL support macOS (primary), Windows, and Linux platforms
5. WHEN documentation is provided THEN it SHALL include README, controls guide, and known issues
6. WHEN the performance CI gate runs THEN it SHALL fail if physics >5ms OR scripts >3ms on 75-part test with Godot 4.4.1 C# API verification
7. WHEN version 1.0 is released THEN it SHALL be distributed via GitHub release and itch.io page