# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Lithobrake is a simplified, performance-focused rocket simulation game built in Godot 4.4.1 with C# support. The project prioritizes smooth 60 FPS gameplay on MacBook Air M4 hardware while delivering engaging orbital mechanics simulation.

## Core Constraints & Performance Targets

**Non-negotiable performance requirements:**
- 60 FPS on MacBook Air M4
- Physics budget: ≤5ms per frame
- Rendering budget: ≤8ms per frame  
- Scripts budget: ≤3ms per frame
- 75 part limit per vessel
- Maximum 16.6ms total frame time

## Development Commands

### Godot Project Setup
```bash
# Open project in Godot editor
godot --editor

# Run the project
godot

# Build C# solution
dotnet build
```

### Testing & Performance Monitoring
```bash
# Run performance tests (when implemented)
godot --headless --script res://tests/performance_test.gd

# Monitor frame timing
# Use in-game PerformanceMonitor overlay (to be implemented)
```

## Architecture Overview

### Language Strategy
- **C#**: Core physics, orbital mechanics, performance-critical systems
- **GDScript**: UI logic, simple gameplay scripts, non-performance-critical code
- **Boundary Rule**: No heavy per-frame math in GDScript; C# aggregates state and emits signals

### Core Systems Architecture

#### Physics System (C#)
- **PhysicsManager**: Singleton managing Jolt physics at fixed 60Hz
- **PhysicsVessel**: Multi-part vessels with joints and anti-wobble system
- **Floating Origin**: Shifts at 20km threshold during coast periods
- **Anti-Wobble**: Progressive joint stiffening based on dynamic pressure (Q > 12kPa)

#### Orbital Mechanics (C#)
- **Double Precision**: All orbital calculations use custom Double3 struct
- **Kepler Solver**: Centralized in Orbital/Kepler.cs (Newton-Raphson, 1e-10 tolerance)
- **OrbitalState**: Keplerian elements with vis-viva velocity calculations
- **Time Warp**: On-rails propagation using centralized solver

#### Universe Constants
All celestial parameters locked in UNIVERSE_CONSTANTS.cs:
- Kerbin scale: 600km radius, μ=3.5316e12 m³/s²
- Standard gravity: 9.81 m/s²
- Atmosphere: 70km height, 7.5km scale height

### Determinism Contract
- **Fixed Update Order**: Input → Physics → Orbital → Render → UI
- **Physics Tick**: Fixed 60Hz (16.67ms)
- **Precision Boundaries**: Double for orbital, float for rendering
- **Conversions**: Only at system boundaries

## Critical Implementation Notes

### Type System Management
- Godot uses Vector3 (float precision)
- Orbital math requires Double3 (custom struct)
- Convert only at physics/orbital boundary
- Target: <0.01ms per 1000 conversions

### Joint Stability
```csharp
// Anti-wobble tuning parameters
const float Q_ENABLE = 12000;   // 12 kPa - enable stiffening
const float Q_DISABLE = 8000;   // 8 kPa - disable (hysteresis)
const float TAU = 0.3f;         // Smoothing time constant
```

### Staging System
- Separation impulse: 500 N·s (applied once at decoupler)
- Mass recalculation: Instant, same frame
- Joint removal: Atomic operation

### Performance CI Gates
Every commit must pass:
- Physics average time < 5ms
- Scripts average time < 3ms  
- Orbital drift < 1% over 10 orbits at 100km
- 75-part stress test maintains 60 FPS

## Project Structure

```
src/Core/           # C# physics and orbital systems
src/Scripts/        # GDScript UI and gameplay
scenes/             # Godot scene files (.tscn)
resources/parts/    # Part definitions and configs
resources/materials/# Shaders and materials
docs/IMPLEMENTATION/# Task execution and progress tracking
docs/REFERENCE/     # Technical specifications and design
docs/GUIDES/        # Usage instructions and workflows
```

## Documentation Map

### For Implementation Work
- **Current Task**: `docs/IMPLEMENTATION/current-task.md` - What to work on now
- **Task List**: `docs/IMPLEMENTATION/tasks.md` - Complete development roadmap
- **Progress**: `docs/IMPLEMENTATION/completed-tasks.md` - Track what's been done

### For Technical Reference  
- **Requirements**: `docs/requirements.md` - Acceptance criteria and user stories
- **Architecture**: `docs/design.md` - System design and component structure
- **Task Details**: `docs/tasks-og.md` - Detailed implementation notes
- **Determinism**: `docs/determinism.md` - Simulation consistency contract

### For Usage Instructions
- **Claude Code Guide**: `docs/GUIDES/claude-code-guide.md` - How to work with tasks

## Claude Code Workflow

### Starting Each Session
1. Check `docs/IMPLEMENTATION/current-task.md` for active task
2. Implement the task following the specifications
3. Run tests and verify performance requirements
4. When task is complete, automatically:
   - Update `docs/IMPLEMENTATION/completed-tasks.md` with completion details
   - Update `docs/IMPLEMENTATION/current-task.md` with the next task from tasks.md
   - Report what the next task will be

### Common Commands for Non-Coders
- "Please implement the current task in docs/IMPLEMENTATION/current-task.md"
- "Run the project and show me the performance metrics"
- "Verify this meets the requirements listed in the task"
- "Check if the implementation passes the success criteria"
- "Show me what files were created or modified"

### Validation Steps
1. Code compiles without errors
2. Performance targets met (physics <5ms, scripts <3ms)
3. Tests pass as specified in task
4. No regression in existing functionality

### Task Completion Protocol
After successful validation, always:
1. Update `docs/IMPLEMENTATION/completed-tasks.md` with:
   - Task name and completion date
   - Status (✅ Complete / ⚠️ Complete with issues)
   - Files created/modified
   - Performance metrics achieved
   - Any notes or issues encountered
2. Find the next task in `docs/IMPLEMENTATION/tasks.md`
3. Update `docs/IMPLEMENTATION/current-task.md` with next task details
4. Report to user what the next task will be

## Development Workflow

1. **Performance First**: Profile before optimizing, measure after changes
2. **Test Physics**: Use 75-part stress test scene for validation
3. **Orbital Accuracy**: Verify <1% drift over extended time periods
4. **Memory Management**: Monitor C# GC impact, implement pooling where needed

## Common Development Tasks

### Adding New Parts
1. Create part definition in resources/parts/
2. Inherit from Part.cs base class
3. Define mass, attachment nodes, and behavior
4. Add to VAB part catalog
5. Test physics integration with 75-part limit

### Modifying Orbital Mechanics
1. ALL orbital math goes through Orbital/Kepler.cs
2. Use Double3 for calculations, convert to Vector3 only for rendering
3. Test with both elliptical (e < 1) and hyperbolic (e >= 1) orbits
4. Verify energy conservation over 10+ orbits

### UI Development
1. Keep GDScript UI code under 3ms budget
2. C# performs calculations, emits aggregated signals
3. Use signal batching for high-frequency updates
4. Monitor with PerformanceMonitor overlay

## Risk Areas Requiring Attention

1. **Type Conversions**: Double3 ↔ Vector3 boundary management
2. **Physics Wobble**: Joint stiffness tuning for tall stacks
3. **Floating Origin**: Coordinate shifts during flight
4. **Time Warp Accuracy**: Orbital propagation precision
5. **C#/GDScript Marshaling**: Signal overhead at 60Hz

## Testing Requirements

- Single rigid body: <1ms physics time
- 10-part vessel: Stable under 50kN thrust
- 30-part rocket: No wobble at maximum thrust
- Time warp: <1% orbital drift over 10 orbits
- Staging: Clean separation without physics glitches
- Terminal velocity: Correct atmospheric drag behavior