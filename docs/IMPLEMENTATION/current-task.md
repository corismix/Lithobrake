# Current Task: Build centralized Kepler orbit solver and propagation system

## Task Overview

**Task 8 from implementation plan**: Build centralized Kepler orbit solver and propagation system

This task creates a centralized Kepler equation solver and orbital propagation system that handles both elliptical (e<1) and hyperbolic (e>=1) orbits. The system provides position and velocity calculations at any time using Newton-Raphson methods with high precision tolerance for accurate orbital mechanics simulation.

## Implementation Steps

### 1. Create Kepler Solver Module
- Create `src/Core/Orbital/Kepler.cs` as centralized Kepler equation solver
- Implement SolveElliptic() method for elliptical orbits (e<1) using Newton-Raphson with 1e-10 tolerance
- Implement SolveHyperbolic() method for hyperbolic orbits (e>=1) using Newton-Raphson with 1e-10 tolerance  
- Add maximum 16 iterations limit for both methods with convergence validation
- Handle edge cases for circular orbits (e≈0) and parabolic trajectories (e≈1)

### 2. Implement Position and Velocity Calculation
- Add GetPositionAtTime() method that solves Kepler's equation and converts to Cartesian coordinates
- Create GetVelocityAtTime() method using vis-viva equation for velocity magnitude calculation
- Implement proper coordinate transformations from orbital plane to inertial frame
- Handle time propagation with mean motion calculations and epoch management

### 3. Create Trajectory Sampling System
- Implement adaptive sampling for trajectory visualization: dense points near Pe/Ap, sparse elsewhere
- Create efficient sampling algorithm that adjusts density based on true anomaly and eccentricity
- Add trajectory point generation for rendering orbital paths and maneuver planning
- Optimize sampling performance for real-time trajectory updates

### 4. Add Energy Conservation Validation
- Implement energy conservation checks for orbital propagation accuracy
- Create validation methods to verify total energy remains constant during propagation
- Add debugging utilities for orbital mechanics validation and troubleshooting
- Include performance monitoring for Kepler solver execution times

### 5. Create Comprehensive Unit Tests
- Build unit tests for elliptical orbit branch with various eccentricities (0 to 0.99)
- Create hyperbolic orbit tests with eccentricities from 1.01 to 5.0
- Test edge cases: circular orbits, parabolic trajectories, high eccentricity ellipses
- Validate energy conservation over extended time periods

## Files to Create/Modify

- **Core Kepler System**:
  - Create `src/Core/Orbital/` directory for orbital mechanics modules (new directory)
  - Create `src/Core/Orbital/Kepler.cs` - Centralized Kepler equation solver with Newton-Raphson methods (new file)
  
- **Integration**:
  - Enhance `src/Core/OrbitalState.cs` - Integrate with centralized Kepler solver instead of embedded solver
  - Update `src/Core/CelestialBody.cs` - Add trajectory sampling and visualization support if needed
  
- **Test Infrastructure**:
  - Create `src/Core/Orbital/KeplerTests.cs` - Unit tests for Kepler solver validation (new file)
  - Update `scenes/orbital_test.tscn` - Add Kepler solver validation to existing orbital tests
  - Create `scenes/kepler_test.tscn` - Specialized Kepler solver testing scene (new file)

## Success Criteria

- [ ] Centralized Kepler.cs with SolveElliptic() and SolveHyperbolic() methods using Newton-Raphson
- [ ] 1e-10 tolerance and maximum 16 iterations for both elliptical and hyperbolic orbit solving
- [ ] GetPositionAtTime() and GetVelocityAtTime() methods with vis-viva equation implementation
- [ ] Adaptive sampling system: dense near Pe/Ap, sparse elsewhere for trajectory visualization
- [ ] Comprehensive unit tests for both elliptical (e<1) and hyperbolic (e>=1) branches
- [ ] Energy conservation validation over extended propagation periods
- [ ] Integration with existing OrbitalState system and performance within budget
- [ ] Kepler solver performance: <0.05ms per solve operation target

## Performance Targets

- Kepler equation solving: <0.05ms per solve operation
- Position calculation: <0.1ms per GetPositionAtTime() call
- Velocity calculation: <0.1ms per GetVelocityAtTime() call  
- Trajectory sampling: <1ms for typical orbital path generation
- Memory allocation: <500 bytes per solver operation
- Energy conservation: <1e-12 relative error over 100 orbital periods

## Requirements Fulfilled

- **Requirement 3.1**: Enhanced orbital mechanics with centralized high-precision solver
- **Requirement 3.2**: Advanced Keplerian element handling with both elliptical and hyperbolic support  
- **Requirement 3.3**: Improved celestial body integration with trajectory generation capabilities
- **Requirement 3.4**: Optimized coordinate transformations using centralized solver
- **Requirement 3.5**: Enhanced orbital propagation with Newton-Raphson precision
- **Requirement 3.6**: Maintained physics system integration with improved accuracy
- **Requirement 3.7**: Performance optimization with specialized solver algorithms

## Technical Notes

From tasks.md and CLAUDE.md:
- Centralize all Kepler solving in Orbital/Kepler.cs for consistency and performance
- Newton-Raphson method with 1e-10 tolerance ensures high precision orbital calculations
- Maximum 16 iterations prevents infinite loops while maintaining accuracy
- Adaptive sampling optimizes trajectory rendering performance
- Energy conservation validation ensures orbital mechanics accuracy
- Integration with existing Double3 and OrbitalState systems maintained

### Kepler Solver Architecture Overview
```csharp
// Centralized Kepler equation solver
public static class Kepler
{
    // Elliptical orbit solver (e < 1)
    public static double SolveElliptic(double meanAnomaly, double eccentricity, double tolerance = 1e-10, int maxIterations = 16);
    
    // Hyperbolic orbit solver (e >= 1)  
    public static double SolveHyperbolic(double meanAnomaly, double eccentricity, double tolerance = 1e-10, int maxIterations = 16);
    
    // Position calculation at specified time
    public static Double3 GetPositionAtTime(OrbitalState state, double time);
    
    // Velocity calculation using vis-viva equation
    public static Double3 GetVelocityAtTime(OrbitalState state, double time);
    
    // Adaptive trajectory sampling for visualization
    public static Double3[] SampleTrajectory(OrbitalState state, int sampleCount, bool adaptiveDensity = true);
}
```

## Validation Commands

After implementation:
- "Test elliptical Kepler solver accuracy with various eccentricities"
- "Validate hyperbolic orbit solver for escape trajectories"
- "Measure Kepler solver performance and convergence"
- "Test energy conservation over extended propagation periods"
- "Verify integration with existing orbital mechanics system"

## Post-Completion Actions

After successful validation, Claude Code should:

1. **Update completed-tasks.md** with:
   ```markdown
   ### Task 8: Build centralized Kepler orbit solver and propagation system
   **Date**: [today's date]
   **Status**: ✅ Complete
   **Files Created/Modified**: [list files]
   **Performance Results**: [Kepler solver measurements and accuracy]
   **Notes**: [solver convergence results and energy conservation validation]
   ```

2. **Update current-task.md** with Task 9 from tasks.md:
   - Copy "Task 9: Implement floating origin system for precision preservation" from tasks.md
   - Add relevant technical details about floating origin implementation
   - Include success criteria and performance targets

3. **Report to user**: "Task 8 complete. Next task is: Implement floating origin system for precision preservation"