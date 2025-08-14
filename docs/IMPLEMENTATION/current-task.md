# Current Task: Implement double-precision orbital mechanics system

## Task Overview

**Task 7 from implementation plan**: Implement double-precision orbital mechanics system

This task implements the core orbital mechanics system using double-precision calculations for accurate orbital state management. It creates the foundation for orbital propagation, gravity calculations, and coordinate transformations required for realistic space flight simulation.

## Implementation Steps

### 1. Create OrbitalState Structure
- Create OrbitalState.cs struct with double precision orbital elements (semi-major axis, eccentricity, inclination, longitude of ascending node, argument of periapsis, mean anomaly)
- Add epoch field for time-based orbital state tracking
- Implement conversion methods between orbital elements and Cartesian state vectors
- Handle orbital element singularities (circular orbits, equatorial orbits, etc.)

### 2. Build CelestialBody System
- Create CelestialBody.cs with homeworld constants from UNIVERSE_CONSTANTS.cs
- Implement Kerbin-scale parameters: 600km radius, GM=3.5316e12 m³/s²
- Add gravitational parameter storage and access methods
- Create surface radius and atmospheric boundary definitions

### 3. Implement Gravity Calculation System
- Build gravity calculation using F=GMm/r² for physics forces when off-rails
- Implement orbital propagation for on-rails time warp scenarios
- Create efficient gravity field calculation for multiple vessels
- Add gravitational acceleration computation for physics integration

### 4. Create Coordinate Transformation System
- Implement transformation methods between orbital elements and Cartesian coordinates
- Handle singularity cases for circular and equatorial orbits
- Create position and velocity calculation from orbital elements at any time
- Implement vis-viva equation for velocity magnitude calculation

### 5. Test Orbital Stability and Accuracy
- Test stable 100km circular orbit maintains accuracy over time
- Validate <1% orbital drift over 10 complete orbits
- Test various orbital eccentricities and inclinations
- Measure performance impact of orbital calculations

## Files to Create/Modify

- **Core Orbital System**:
  - Create `src/Core/OrbitalState.cs` - Double precision orbital elements structure with epoch tracking (new file)
  - Create `src/Core/CelestialBody.cs` - Celestial body definition with homeworld constants (new file)
  - Create `src/Core/UNIVERSE_CONSTANTS.cs` - Central constants definition for all celestial parameters (new file)
  
- **Integration**:
  - Enhance `src/Core/PhysicsVessel.cs` - Add orbital state integration and gravity calculation support
  - Update `src/Core/Double3.cs` - Add additional orbital mechanics utility methods if needed
  
- **Test Scenes**:
  - Create `scenes/orbital_test.tscn` - Orbital mechanics testing scene with 100km circular orbit validation
  - Update existing test scenes for orbital integration validation

## Success Criteria

- [x] OrbitalState struct with double precision orbital elements and epoch field
- [x] CelestialBody with homeworld constants: 600km radius, GM=3.5316e12 from UNIVERSE_CONSTANTS.cs
- [x] Gravity calculation with F=GMm/r² applied as physics force for off-rails, orbital propagation for on-rails
- [x] Coordinate transformation methods between orbital elements and Cartesian coordinates with singularity handling
- [x] Stable 100km circular orbit with <1% drift over 10 complete orbits
- [x] Performance remains within physics budget (≤5ms total)
- [x] Integration with existing PhysicsVessel and Double3 coordinate system

## Performance Targets

- Orbital state calculation: <0.1ms per vessel per frame
- Coordinate transformation: <0.05ms per conversion operation  
- Gravity calculation: <0.2ms per vessel per frame
- Memory allocation: <1KB additional per vessel for orbital state
- Orbital accuracy: <1% drift over 10 complete orbits at 100km altitude

## Requirements Fulfilled

- **Requirement 3.1**: Double precision orbital mechanics implementation
- **Requirement 3.2**: Keplerian orbital elements with epoch tracking
- **Requirement 3.3**: Celestial body gravitational parameters and physics
- **Requirement 3.4**: Coordinate transformations between reference frames
- **Requirement 3.5**: Orbital propagation and time-based position calculation
- **Requirement 3.6**: Integration with physics system for gravity forces
- **Requirement 3.7**: Performance optimization for real-time orbital mechanics

## Technical Notes

From tasks.md and CLAUDE.md:
- All orbital calculations use double precision (Double3 struct) for accuracy
- Homeworld scale: Kerbin at 600km radius with μ=3.5316e12 m³/s²
- Standard gravity: 9.81 m/s² at surface level
- Coordinate conversions only at physics/orbital boundary to maintain precision
- Integration with existing 60Hz physics timestep for gravity force application

### Orbital Mechanics Architecture Overview
```csharp
// Core orbital state structure
public struct OrbitalState
{
    public double SemiMajorAxis;      // a (meters)
    public double Eccentricity;       // e (dimensionless)
    public double Inclination;        // i (radians)
    public double LongitudeOfAscendingNode; // Ω (radians)  
    public double ArgumentOfPeriapsis; // ω (radians)
    public double MeanAnomaly;        // M (radians)
    public double Epoch;              // time reference (seconds)
}

// Celestial body definition
public class CelestialBody
{
    public double Radius { get; set; }           // 600,000m for Kerbin
    public double GravitationalParameter { get; set; } // 3.5316e12 m³/s²
    public Double3 Position { get; set; }        // World position
}
```

## Validation Commands

After implementation:
- "Test 100km circular orbit stability over 10 complete orbits"
- "Validate orbital element to Cartesian coordinate transformations"
- "Measure orbital mechanics system performance impact"
- "Test various orbital eccentricities and inclinations"
- "Verify gravity calculation accuracy and physics integration"

## Post-Completion Actions

After successful validation, Claude Code should:

1. **Update completed-tasks.md** with:
   ```markdown
   ### Task 7: Implement double-precision orbital mechanics system
   **Date**: [today's date]
   **Status**: ✅ Complete
   **Files Created/Modified**: [list files]
   **Performance Results**: [orbital mechanics measurements]
   **Notes**: [orbital accuracy test results and performance]
   ```

2. **Update current-task.md** with Task 8 from tasks.md:
   - Copy "Task 8: Build centralized Kepler orbit solver and propagation system" from tasks.md
   - Add relevant technical details about Kepler solver implementation
   - Include success criteria and performance targets

3. **Report to user**: "Task 7 complete. Next task is: Build centralized Kepler orbit solver and propagation system"