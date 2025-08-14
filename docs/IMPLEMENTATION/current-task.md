# Current Task: Build thrust and fuel consumption system with realistic physics

## Task Overview

**Task 11 from implementation plan**: Build thrust and fuel consumption system with realistic physics

This task implements realistic thrust generation and fuel consumption mechanics with proper physics calculations, fuel flow systems, throttle control, and visual feedback. The system integrates with existing Engine and FuelTank parts to provide accurate rocket propulsion simulation.

## Implementation Steps

### 1. Enhance Engine Thrust System
- Update `src/Core/Parts/Engine.cs` with GetThrust() method using mass flow rate and Isp calculations
- Implement thrust curves vs constant Isp approach for realistic engine performance
- Add engine state management (ignited/shutdown/flameout) with proper startup/shutdown sequences
- Implement gimbal control for thrust vectoring and attitude control
- Calculate thrust efficiency based on atmospheric pressure and altitude

### 2. Create Fuel Flow System
- Implement fuel consumption calculation using formula: consumption = thrust / (Isp * g0)
- Add fuel depletion tracking with real-time fuel level monitoring
- Create fuel transfer system between connected tanks with crossfeed capability
- Handle fuel starvation scenarios with automatic engine shutdown
- Implement fuel priority system for optimal fuel management

### 3. Build Fuel Tank Drainage System
- Create tree traversal algorithm for fuel flow from engines to tanks (bottom to top)
- Implement crossfeed blocking at decouplers and structural separations
- Add fuel line management with automatic routing and manual override capabilities
- Handle fuel tank emptying with center of mass shifts and mass property updates
- Validate fuel accessibility and prevent isolated tank scenarios

### 4. Implement Throttle Control System
- Add throttle input handling with Z/X keys for 5% incremental steps
- Implement Shift+Z for full throttle (100%) and Ctrl+X for complete cutoff
- Create smooth throttle transitions to prevent sudden thrust changes
- Add throttle response curves for different engine types
- Integrate throttle with fuel consumption and thrust calculations

### 5. Create Thrust Visualization System
- Implement thrust vector visualization with arrows scaled by thrust magnitude
- Color-code thrust vectors by engine efficiency (atmospheric vs vacuum performance)
- Add real-time thrust display showing current thrust, maximum thrust, and efficiency
- Create visual feedback for engine state changes and throttle adjustments
- Ensure visualization performance meets <0.5ms per engine rendering budget

### 6. Build Exhaust Effects System
- Create EffectsManager.cs for managing particle systems and visual effects
- Implement exhaust particle effects using CPUParticles3D for engine plumes
- Add object pooling system for performance optimization and memory management
- Scale exhaust effects based on thrust level, atmospheric density, and engine efficiency
- Implement exhaust light emission and heat distortion effects for atmospheric flight

### 7. Physics Integration and Performance
- Integrate thrust forces with existing PhysicsVessel system for realistic physics
- Apply thrust forces at engine mount points with proper force direction
- Handle engine failure scenarios and asymmetric thrust situations
- Optimize fuel consumption calculations for <0.2ms per frame per engine
- Ensure thrust system maintains 60 FPS with multiple engines active

## Files to Create/Modify

- **Engine System Updates**:
  - Update `src/Core/Parts/Engine.cs` - Add GetThrust(), fuel consumption, and engine state management
  - Update `src/Core/Parts/FuelTank.cs` - Add fuel transfer, drainage, and crossfeed systems
  
- **New Systems**:
  - Create `src/Core/ThrustSystem.cs` - Centralized thrust calculations and force application (new file)
  - Create `src/Core/FuelFlowSystem.cs` - Fuel routing, consumption, and tank management (new file)
  - Create `src/Core/ThrottleController.cs` - Input handling and throttle management (new file)
  - Create `src/Core/EffectsManager.cs` - Particle systems and visual effects management (new file)
  
- **Integration Updates**:
  - Update `src/Core/PhysicsVessel.cs` - Integrate thrust forces with physics simulation
  - Update `src/Core/TestRocketAssembly.cs` - Add thrust and fuel consumption testing
  
- **Test Infrastructure**:
  - Create `src/Core/ThrustTests.cs` - Unit tests for thrust and fuel systems (new file)
  - Update `scenes/parts_test.tscn` - Add thrust system validation and performance testing
  - Create `scenes/thrust_test.tscn` - Specialized thrust system testing scene (new file)

## Success Criteria

- [ ] GetThrust() method in Engine.cs with mass flow rate and Isp calculations (thrust = mass_flow * exhaust_velocity)
- [ ] Fuel consumption system using formula: consumption = thrust / (Isp * g0) with accurate fuel depletion
- [ ] Fuel tank drainage system with tree traversal from engines to tanks, crossfeed support
- [ ] Throttle control with Z/X keys (5% steps), Shift+Z (full), Ctrl+X (cutoff), smooth transitions
- [ ] Thrust visualization with vector arrows scaled by thrust, colored by efficiency
- [ ] Exhaust effects using CPUParticles3D with EffectsManager and object pooling
- [ ] Physics integration applying thrust forces at engine mount points with proper direction
- [ ] Performance targets: <0.2ms fuel calculations, <0.5ms thrust visualization, 60 FPS maintained

## Performance Targets

- Thrust calculation: <0.2ms per frame per engine
- Fuel consumption update: <0.1ms per frame per fuel tank
- Thrust visualization: <0.5ms per frame for all engines
- Exhaust effects: <1ms per frame using object pooling
- Physics integration: <0.3ms additional to existing physics budget
- Memory allocation: <1KB per frame for thrust/fuel systems

## Requirements Fulfilled

- **Requirement 4.1**: Engine thrust generation with realistic physics calculations
- **Requirement 4.2**: Fuel consumption system based on specific impulse and mass flow
- **Requirement 4.3**: Fuel routing and crossfeed management between tanks
- **Requirement 4.4**: User throttle control with keyboard input and smooth response
- **Requirement 4.5**: Visual thrust feedback with magnitude and efficiency indication
- **Requirement 4.6**: Exhaust particle effects with performance optimization
- **Requirement 4.7**: Physics integration maintaining simulation accuracy and performance

## Technical Notes

From tasks.md and CLAUDE.md:
- Engine performance based on realistic rocket equation: F = mass_flow * exhaust_velocity
- Specific impulse (Isp) determines fuel efficiency: consumption = thrust / (Isp * g0)
- Fuel flow follows vessel structure from engines up through tanks
- Crossfeed capability allows fuel sharing between connected tanks
- Throttle control provides smooth thrust adjustment for precise maneuvering
- Visual feedback essential for understanding engine performance and efficiency

### Thrust System Architecture Overview
```csharp
// Thrust system integration
public class ThrustSystem : Node
{
    // Engine management and thrust calculations
    public static double CalculateThrust(Engine engine, double throttle, double atmosphericPressure);
    public static void ApplyThrustForces(PhysicsVessel vessel, List<Engine> engines);
    
    // Fuel consumption and flow management
    public static double CalculateFuelConsumption(Engine engine, double thrust);
    public static void UpdateFuelFlow(PhysicsVessel vessel);
}

// Throttle control system
public class ThrottleController : Node
{
    // Input handling and throttle management
    public static void ProcessThrottleInput(InputEvent input);
    public static void SetThrottle(double throttleLevel);
    public static double GetCurrentThrottle();
}
```

## Validation Commands

After implementation:
- "Test engine thrust generation with different throttle levels"
- "Validate fuel consumption rates match theoretical calculations"
- "Test fuel flow system with multiple tanks and crossfeed scenarios"
- "Verify throttle control responsiveness and smooth transitions"
- "Measure thrust visualization and exhaust effects performance"

## Post-Completion Actions

After successful validation, Claude Code should:

1. **Update completed-tasks.md** with:
   ```markdown
   ### Task 11: Build thrust and fuel consumption system with realistic physics
   **Date**: [today's date]
   **Status**: ✅ Complete
   **Files Created/Modified**: [list files]
   **Performance Results**: [thrust calculation timing and fuel consumption accuracy]
   **Notes**: [thrust system validation results and physics integration success]
   ```

2. **Update current-task.md** with Task 12 from tasks.md:
   - Copy "Task 12: Build atmospheric model and aerodynamic drag system" from tasks.md
   - Add relevant technical details about atmospheric physics implementation
   - Include success criteria and performance targets

3. **Report to user**: "Task 11 complete. Next task is: Build atmospheric model and aerodynamic drag system"