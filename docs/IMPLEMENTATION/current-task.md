# Current Task: Verify Godot 4.4.1 C# API Surface and Performance Characteristics

## Task Overview

**Task 0.5 from implementation plan**: Verify Godot 4.4.1 C# API Surface and Performance Characteristics

This is a critical validation task that must be completed before building complex systems. We need to verify that the Godot APIs we plan to use actually exist and perform as expected on the target hardware.

## Implementation Steps

### 1. Verify Engine.TimeScale API
- Confirm Engine.TimeScale exists in Godot 4.4.1 C# API
- Test its behavior for time warp functionality
- Document actual API signature and behavior

### 2. Verify Physics Configuration API  
- Confirm Engine.PhysicsTicksPerSecond exists for 60Hz physics configuration
- Test setting physics tick rate to exactly 60Hz
- Verify fixed timestep behavior

### 3. Test Vector3/Transform3D Marshaling Performance
- Create test scene with frequent C#/GDScript vector passing
- Measure actual marshaling overhead between C# and GDScript
- Document performance characteristics vs. documentation claims

### 4. Benchmark Double3 ↔ Vector3 Conversion Performance
- Implement Double3 struct for orbital calculations
- Create conversion utilities to/from Godot.Vector3
- Benchmark conversion performance (target: <0.1ms per 1000 conversions)
- Compare with requirements target of <0.01ms per 1000 conversions

### 5. Document API Differences
- Note any differences from Godot documentation
- Document version-specific behavior changes
- Record macOS-specific considerations

### 6. Create Performance Baseline
- Establish baseline measurements on MacBook Air M4
- Document frame timing with empty scene
- Measure physics overhead with single rigid body
- Record script execution time for minimal operations

## Files to Create

- `src/Core/Double3.cs` - Double precision vector struct
- `src/Core/PerformanceMonitor.cs` - Performance tracking system
- `test_scenes/api_validation.tscn` - Test scene for API verification
- `src/Core/ApiValidation.cs` - C# test node for API verification
- `performance_baseline.md` - Document baseline measurements

## Success Criteria

- [ ] Engine.TimeScale API confirmed working for time warp
- [ ] Physics tick rate successfully set to 60Hz
- [ ] Vector3 marshaling overhead measured and documented
- [ ] Double3 conversion performance meets <0.1ms per 1000 operations
- [ ] Performance baseline established on target hardware
- [ ] All API differences documented
- [ ] Test scene runs at 60 FPS with performance overlay

## Performance Targets

- Frame time: ≤16.6ms (60 FPS)
- Physics time: <1ms for empty scene
- Script time: <1ms for minimal operations
- Type conversions: <0.1ms per 1000 operations

## Requirements Fulfilled

- **Requirement 1.5**: Type conversion overhead <0.1ms per 1000 conversions
- **Requirement 7.1**: Time warp using verified Godot 4.4.1 C# API
- **Requirement 10.6**: Performance CI validation with API verification

## Technical Notes

From tasks-og.md:
- Target hardware: MacBook Air M4
- Physics must run at exactly 60Hz fixed timestep
- Use Jolt physics engine
- Monitor for any macOS-specific API behavior
- Document actual vs expected performance characteristics

## Validation Commands

After implementation:
- "Run the test scene and show me the performance metrics"
- "Verify the APIs work as expected"
- "Show me the conversion performance benchmarks"
- "Confirm 60Hz physics timing"

## Post-Completion Actions

After successful validation, Claude Code should:

1. **Update completed-tasks.md** with:
   ```markdown
   ### Task 0.5: Verify Godot 4.4.1 C# API Surface
   **Date**: [today's date]
   **Status**: ✅ Complete
   **Files Created**: [list files created]
   **Performance**: [actual performance metrics]
   **Notes**: [any important observations]
   ```

2. **Update current-task.md** with Task 1 from tasks.md:
   - Copy "Task 1: Setup C#/GDScript integration foundation" from tasks.md
   - Add relevant technical details from tasks-og.md
   - Include success criteria and performance targets

3. **Report to user**: "Task 0.5 complete. Next task is: Setup C#/GDScript integration foundation"