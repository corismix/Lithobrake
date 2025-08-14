# Current Task: Create physics joint separation mechanics (low-level)

## Task Overview

**Task 6 from implementation plan**: Create physics joint separation mechanics (low-level)

This task implements low-level physics joint separation mechanics for staging operations. It provides atomic joint removal between vessel parts with precise separation impulses and instant mass redistribution, ensuring physics stability during critical staging events.

## Implementation Steps

### 1. Implement Atomic Joint Removal System
- Create atomic joint removal between vessel parts at physics system level
- Ensure separation operates in single physics frame with no intermediate states
- Handle joint removal without causing physics instability or glitches
- Implement proper cleanup of joint references and physics connections

### 2. Apply Precise Separation Impulse
- Apply exactly 500 N·s separation impulse at decoupler position in single frame
- Calculate impulse direction based on separation axis or vessel configuration  
- Ensure impulse is applied to correct parts during separation event
- Validate impulse magnitude and direction consistency across separations

### 3. Handle Instant Mass Redistribution
- Implement instant mass redistribution with vessel mass recalculation
- Update center of mass calculation immediately after separation
- Recalculate moment of inertia tensor for both resulting vessel fragments
- Ensure mass properties are consistent and physically accurate post-separation

### 4. Create Low-Level Separation Event System
- Build separation event system with physics state validation
- Implement pre-separation and post-separation state verification
- Add separation event callbacks for higher-level systems
- Create separation validation to prevent invalid separation attempts

### 5. Joint Separation Testing and Validation
- Test joint separation 10 times in succession maintains physics stability
- Validate no physics glitches or instabilities occur during separation
- Test separation under various load conditions and vessel configurations
- Measure performance impact of separation operations

## Files to Create/Modify

- **Core Separation System**:
  - Enhance `src/Core/PhysicsVessel.cs` - Add advanced separation mechanics and validation
  - Create `src/Core/SeparationEvent.cs` - Separation event data structure and validation (new file)
  
- **Integration**:
  - Update `src/Core/PhysicsManager.cs` - Add separation event coordination if needed
  - Enhance existing joint removal methods with atomic separation mechanics
  
- **Test Scenes**:
  - `scenes/separation_test.tscn` - Joint separation testing scene with repeated separation tests
  - Update existing test scenes for separation validation

## Success Criteria

- [ ] Atomic joint removal between vessel parts at physics system level
- [ ] Exactly 500 N·s separation impulse applied at decoupler position in single frame
- [ ] Instant mass redistribution with accurate mass, center of mass, and moment of inertia recalculation  
- [ ] Low-level separation event system with physics state validation
- [ ] Joint separation 10 times in succession maintains physics stability without glitches
- [ ] Performance remains within physics budget (≤5ms total)
- [ ] Integration with existing PhysicsManager and vessel systems

## Performance Targets

- Separation operation: <0.2ms per separation event
- Mass recalculation: <0.1ms for post-separation mass properties
- Joint cleanup: <0.05ms per removed joint
- Memory allocation: <500 bytes additional per separation event
- Physics stability: No instabilities or glitches in 10 successive separations

## Requirements Fulfilled

- **Requirement 2.1**: Physics engine integration (separation mechanics)
- **Requirement 2.2**: 60Hz fixed timestep with atomic separation operations  
- **Requirement 2.3**: Performance monitoring for separation system
- **Requirement 2.4**: Advanced vessel physics management with separation
- **Requirement 2.5**: Complex joint parameter management during separation
- **Requirement 2.6**: Multi-body physics stability during staging
- **Requirement 2.7**: Deterministic separation behavior

## Technical Notes

From tasks.md and CLAUDE.md:
- Separation impulse: 500 N·s (applied once at decoupler) - this is a critical specification
- Mass recalculation: Instant, same frame - no delayed updates allowed
- Joint removal: Atomic operation - all or nothing, no partial states  
- Separation must maintain physics stability without introducing wobble or instabilities
- Operation must be deterministic and repeatable for consistent gameplay

### Separation Algorithm Overview
```csharp
// Atomic separation operation
public bool SeparateAtJoint(int jointId, bool applySeparationImpulse = true)
{
    // 1. Validate separation can occur
    // 2. Calculate separation impulse vector (500 N·s)
    // 3. Remove joint atomically
    // 4. Apply impulse in single frame
    // 5. Recalculate mass properties instantly
    // 6. Validate post-separation physics state
    // 7. Trigger separation event callbacks
}
```

## Validation Commands

After implementation:
- "Test joint separation mechanics with 10 successive separations"
- "Validate mass redistribution accuracy after separation events"
- "Measure separation operation performance impact"
- "Test separation under load conditions and high thrust"
- "Verify physics stability maintenance during rapid separations"

## Post-Completion Actions

After successful validation, Claude Code should:

1. **Update completed-tasks.md** with:
   ```markdown
   ### Task 6: Create physics joint separation mechanics (low-level)
   **Date**: [today's date]
   **Status**: ✅ Complete
   **Files Created/Modified**: [list files]
   **Performance Results**: [separation system measurements]
   **Notes**: [separation stability test results and performance]
   ```

2. **Update current-task.md** with Task 7 from tasks.md:
   - Copy "Task 7: Implement double-precision orbital mechanics system" from tasks.md
   - Add relevant technical details about orbital state and celestial body implementation
   - Include success criteria and performance targets

3. **Report to user**: "Task 6 complete. Next task is: Implement double-precision orbital mechanics system"