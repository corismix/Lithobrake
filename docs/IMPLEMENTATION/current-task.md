# Current Task: Implement core physics system with Jolt integration

## Task Overview

**Task 3 from implementation plan**: Implement core physics system with Jolt integration

This task establishes the core physics foundation using Jolt physics engine with 60Hz fixed timestep, creating the PhysicsManager singleton and PhysicsVessel class that will handle all vehicle physics simulation.

## Implementation Steps

### 1. Configure Jolt Physics Engine
- Configure Jolt physics engine in project settings with 60Hz fixed timestep and continuous collision detection
- Verify Jolt physics is available and properly configured in Godot 4.4.1
- Set up collision detection parameters and performance settings

### 2. Create PhysicsManager Singleton
- Create `PhysicsManager.cs` singleton with FixedDelta constant (1/60)
- Add PhysicsServer3D reference and vessel registration system
- Implement physics tick management and timing validation
- Add performance monitoring integration with existing PerformanceMonitor

### 3. Implement PhysicsVessel Class
- Create `PhysicsVessel.cs` class with parts list and joints list
- Implement mass properties calculation methods (total mass, center of mass, moment of inertia)
- Add vessel lifecycle management (creation, update, destruction)
- Integrate with existing Double3 and coordinate system

### 4. Create Physics Test Scene
- Create test scene with single rigid body to verify 60Hz physics tick
- Measure physics overhead (target <1ms per tick)
- Add collision layers configuration
- Validate physics determinism and consistency

### 5. Basic Rigid Body Performance Testing
- Test single rigid body performance under various conditions
- Validate 60Hz tick rate consistency
- Measure memory allocation and GC impact
- Ensure compatibility with existing performance monitoring

## Files to Create/Modify

- **Core Physics Classes**:
  - `src/Core/PhysicsManager.cs` - Singleton physics manager
  - `src/Core/PhysicsVessel.cs` - Multi-part vessel physics handler
  
- **Test Scene**:
  - `scenes/physics_test.tscn` - Physics system validation scene
  
- **Configuration**:
  - `project.godot` - Additional Jolt physics configuration if needed

## Success Criteria

- [ ] Jolt physics engine properly configured with 60Hz fixed timestep
- [ ] PhysicsManager singleton created with FixedDelta constant and vessel registration
- [ ] PhysicsVessel class implemented with parts list, joints list, and mass calculation
- [ ] Test scene demonstrates 60Hz physics tick with <1ms overhead
- [ ] Collision layers properly configured
- [ ] Physics performance measured and documented
- [ ] Integration with existing PerformanceMonitor validated
- [ ] No regression in existing performance benchmarks

## Performance Targets

- Physics overhead: <1ms per 60Hz tick
- Total physics budget: ≤5ms per frame
- Memory allocation: <1KB per physics tick
- Deterministic behavior: 100% reproducible results
- Integration overhead: <0.1ms with existing systems

## Requirements Fulfilled

- **Requirement 2.1**: Physics engine integration (Jolt)
- **Requirement 2.2**: 60Hz fixed timestep configuration
- **Requirement 2.3**: Performance monitoring and validation
- **Requirement 2.4**: Vessel physics management system
- **Requirement 2.5**: Mass properties calculation
- **Requirement 2.6**: Collision detection configuration
- **Requirement 2.7**: Physics determinism validation

## Technical Notes

From tasks.md:
- Use Jolt physics engine for improved performance and stability over default Godot physics
- Maintain 60Hz fixed timestep for deterministic simulation
- Implement vessel registration system for managing multiple physics objects
- Ensure physics overhead stays under performance budget (≤5ms total)
- Integrate with existing coordinate system and Double3 precision management

## Validation Commands

After implementation:
- "Test the physics system and show me the performance metrics"
- "Verify the 60Hz physics tick is working correctly"
- "Run the physics test scene and measure overhead"
- "Validate physics determinism with repeated tests"

## Post-Completion Actions

After successful validation, Claude Code should:

1. **Update completed-tasks.md** with:
   ```markdown
   ### Task 3: Implement core physics system with Jolt integration
   **Date**: [today's date]
   **Status**: ✅ Complete
   **Files Created**: [list files created]
   **Performance Results**: [physics timing measurements]
   **Notes**: [any important observations]
   ```

2. **Update current-task.md** with Task 4 from tasks.md:
   - Copy "Task 4: Build multi-part vessel system with joint connections" from tasks.md
   - Add relevant technical details
   - Include success criteria and performance targets

3. **Report to user**: "Task 3 complete. Next task is: Build multi-part vessel system with joint connections"