# Current Task: Build atmospheric model and aerodynamic drag system

## Task Overview

**Task 12 from implementation plan**: Build atmospheric model and aerodynamic drag system

This task implements realistic atmospheric physics with exponential density models, aerodynamic drag calculations, dynamic pressure tracking, and heating visualization. The system integrates with existing physics systems to provide accurate atmospheric effects during flight.

## Implementation Steps

### 1. Create Atmosphere Class
- Create `src/Core/Atmosphere.cs` with exponential density model: ρ = 1.225 * exp(-h/7500) with altitude clamps
- Implement atmospheric constants for Kerbin-scale atmosphere (70km height, 7.5km scale height)
- Add temperature gradient modeling for realistic atmospheric physics
- Provide efficient density lookup with caching for performance optimization
- Handle altitude boundaries and edge cases (negative altitude, space transitions)

### 2. Implement Aerodynamic Drag Calculations
- Calculate drag force using: Force = 0.5 * ρ * v² * Cd * A applied opposite to velocity vector
- Add drag coefficient system per part type: streamlined=0.3, blunt=1.0, with fairings=0.2
- Implement effective cross-sectional area calculation based on velocity direction
- Apply drag forces to individual parts with proper force application points
- Optimize drag calculations for multiple parts per vessel

### 3. Build Dynamic Pressure System
- Track dynamic pressure Q = 0.5 * ρ * v² for structural and visual effects
- Trigger auto-struts integration with AntiWobbleSystem based on Q thresholds
- Display dynamic pressure in flight UI for pilot awareness
- Use Q for heating effects scaling and atmospheric flight feedback
- Monitor Max Q events and provide warnings for structural limits

### 4. Add Heating Visualization System
- Implement heating visualization with red glow effect scaled by Q * velocity
- Use particle effects or shader-based glow without damage mechanics implementation
- Scale heating effects based on atmospheric density and velocity
- Add heating intensity calculation for different part materials and shapes
- Ensure heating effects performance meets <0.5ms per part rendering budget

### 5. Implement Terminal Velocity Physics
- Calculate terminal velocity where thrust + drag = weight for accurate physics
- Test rocket reaches correct terminal velocity in different atmospheric layers
- Verify drag decreases correctly with altitude as atmosphere thins
- Handle supersonic vs subsonic drag coefficient transitions
- Validate realistic ascent profiles with atmospheric drag effects

### 6. Physics System Integration
- Integrate drag forces with existing PhysicsVessel system for realistic flight
- Apply drag forces at part center of pressure with proper torque effects
- Handle atmospheric flight stability and control authority changes
- Optimize atmospheric calculations for <0.3ms per frame per vessel
- Ensure atmospheric system maintains 60 FPS with multiple vessels

### 7. Performance Optimization and Validation
- Implement atmospheric calculation caching and efficient lookup tables
- Optimize drag force application for vessels with many parts
- Monitor performance impact on physics budget and script timing
- Test atmospheric effects with 30+ part vessels maintaining 60 FPS
- Validate realistic atmospheric flight behavior and terminal velocity

## Files to Create/Modify

- **Atmospheric Physics**:
  - Create `src/Core/Atmosphere.cs` - Exponential density model and atmospheric constants (new file)
  - Create `src/Core/AerodynamicDrag.cs` - Drag force calculations and part coefficients (new file)
  - Create `src/Core/DynamicPressure.cs` - Q calculations and structural effects integration (new file)
  
- **Visual Effects**:
  - Create `src/Core/HeatingEffects.cs` - Heating visualization and particle effects management (new file)
  - Update `src/Core/EffectsManager.cs` - Integrate heating effects with existing visual system
  
- **Physics Integration**:
  - Update `src/Core/PhysicsVessel.cs` - Integrate atmospheric forces with physics simulation
  - Update `src/Core/Parts/Part.cs` - Add drag coefficient and cross-sectional area properties
  
- **Testing Infrastructure**:
  - Create `src/Core/AtmosphericTests.cs` - Unit tests for atmospheric physics and drag systems (new file)
  - Create `scenes/atmospheric_test.tscn` - Atmospheric physics testing scene (new file)
  - Update existing test scenes - Add atmospheric effects validation

## Success Criteria

- [ ] Atmosphere class with exponential density model ρ = 1.225 * exp(-h/7500) and altitude boundaries
- [ ] Drag force calculation using Force = 0.5 * ρ * v² * Cd * A applied opposite to velocity
- [ ] Drag coefficients per part type: streamlined=0.3, blunt=1.0, fairings=0.2 with area calculations
- [ ] Dynamic pressure Q = 0.5 * ρ * v² tracking with auto-struts integration and UI display
- [ ] Heating visualization with red glow effects scaled by Q * velocity without damage mechanics
- [ ] Terminal velocity physics where rocket reaches correct terminal velocity and drag decreases with altitude
- [ ] Performance targets: <0.3ms atmospheric calculations, <0.5ms heating effects, 60 FPS maintained

## Performance Targets

- Atmospheric density calculation: <0.1ms per frame per vessel
- Drag force calculation: <0.2ms per frame for all parts in vessel
- Dynamic pressure tracking: <0.05ms per frame per vessel
- Heating effects rendering: <0.5ms per frame for all parts
- Physics integration: <0.3ms additional to existing physics budget
- Memory allocation: <0.5KB per frame for atmospheric systems

## Requirements Fulfilled

- **Requirement 4.1**: Atmospheric physics with realistic density models
- **Requirement 4.2**: Aerodynamic drag forces affecting flight dynamics
- **Requirement 4.3**: Dynamic pressure tracking for structural and visual effects
- **Requirement 4.4**: Visual feedback through heating effects and atmospheric indicators
- **Requirement 4.5**: Terminal velocity physics and altitude-based drag changes
- **Requirement 4.6**: Performance optimization maintaining 60 FPS flight experience
- **Requirement 4.7**: Integration with existing physics and effects systems

## Technical Notes

From tasks.md and atmospheric physics principles:
- Exponential atmosphere model: ρ = ρ₀ * exp(-h/H) where H = 7500m scale height
- Drag equation: F_drag = ½ρv²CdA where Cd varies by part shape and flow regime
- Dynamic pressure Q = ½ρv² drives structural loads and heating effects
- Terminal velocity occurs when drag + buoyancy = weight (F_drag = mg)
- Heating intensity scales with Q * velocity for realistic atmospheric entry effects
- Integration with AntiWobbleSystem for Q-based auto-struts activation

### Atmospheric System Architecture Overview
```csharp
// Atmospheric physics system
public class Atmosphere : Node
{
    // Atmospheric density and pressure calculations
    public static double GetDensity(double altitude);
    public static double GetPressure(double altitude);
    public static double GetTemperature(double altitude);
}

// Aerodynamic drag system
public class AerodynamicDrag : Node
{
    // Drag force calculations and application
    public static Vector3 CalculateDragForce(Part part, Vector3 velocity, double density);
    public static void ApplyDragForces(PhysicsVessel vessel);
}

// Dynamic pressure and heating
public class DynamicPressure : Node
{
    // Q calculations and structural effects
    public static double CalculateQ(Vector3 velocity, double density);
    public static void UpdateHeatingEffects(PhysicsVessel vessel);
}
```

## Validation Commands

After implementation:
- "Test atmospheric density calculation at different altitudes"
- "Validate drag forces produce correct terminal velocity"
- "Test dynamic pressure tracking and auto-struts integration"
- "Verify heating effects scale correctly with atmospheric conditions"
- "Measure atmospheric system performance impact"

## Post-Completion Actions

After successful validation, Claude Code should:

1. **Update completed-tasks.md** with:
   ```markdown
   ### Task 12: Build atmospheric model and aerodynamic drag system
   **Date**: [today's date]
   **Status**: ✅ Complete
   **Files Created/Modified**: [list files]
   **Performance Results**: [atmospheric calculation timing and drag force accuracy]
   **Notes**: [atmospheric physics validation results and terminal velocity testing]
   ```

2. **Update current-task.md** with Task 13 from tasks.md:
   - Copy "Task 13: Create flight camera system with smooth following" from tasks.md
   - Add relevant technical details about camera system implementation
   - Include success criteria and performance targets

3. **Report to user**: "Task 12 complete. Next task is: Create flight camera system with smooth following"