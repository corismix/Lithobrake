# C#/GDScript Performance Boundaries

**Performance-critical integration guidelines for maintaining 60 FPS target on MacBook Air M4**

## Core Performance Rule

**C# aggregates state, emits one packed signal per frame to GDScript**

This fundamental pattern ensures minimal marshaling overhead while maintaining responsive UI updates.

## Performance Budgets

### Frame Time Allocation
- **Total Frame Time**: ≤16.6ms (60 FPS)
- **Physics Time**: ≤5ms 
- **Script Time**: ≤3ms
- **Signal Marshaling**: <0.5ms overhead for packed signals
- **Memory Allocation**: No GC pressure causing frame drops

### Conversion Performance Targets
- **Double3 ↔ Vector3**: <0.1ms per 1000 operations
- **Batch Conversions**: Use `FastConvertToVector3Array()` for >10 conversions
- **Object Pooling**: Mandatory for arrays >100 elements

## Signal Communication Patterns

### ✅ Recommended: Packed Signal Pattern

**C# Side (Producer)**:
```csharp
// Aggregate multiple values into single signal per frame
var packedData = MemoryManager.GetDictionary();
packedData["position"] = currentPosition.ToVector3();
packedData["velocity"] = currentVelocity.ToVector3();
packedData["fuel_remaining"] = fuelMass;
packedData["altitude"] = altitude;

EmitSignal(SignalName.VesselDataUpdate, packedData);
MemoryManager.ReturnDictionary(packedData);
```

**GDScript Side (Consumer)**:
```gdscript
func _on_vessel_data_update(packed_data: Dictionary):
    # Process all UI updates in single call
    update_position_display(packed_data.get("position", Vector3.ZERO))
    update_velocity_display(packed_data.get("velocity", Vector3.ZERO))
    update_fuel_display(packed_data.get("fuel_remaining", 0.0))
    update_altitude_display(packed_data.get("altitude", 0.0))
```

### ❌ Anti-Pattern: High-Frequency Individual Signals

```csharp
// DON'T: Multiple signals per frame
EmitSignal(SignalName.PositionChanged, position);      // 0.1ms
EmitSignal(SignalName.VelocityChanged, velocity);      // 0.1ms  
EmitSignal(SignalName.FuelChanged, fuel);              // 0.1ms
EmitSignal(SignalName.AltitudeChanged, altitude);      // 0.1ms
// Total: 0.4ms+ marshaling overhead per frame
```

## Memory Management Patterns

### Object Pooling (Mandatory)

**For Frequent Allocations**:
```csharp
// Use pooled arrays for physics calculations
var positions = MemoryManager.GetDouble3Array(partCount);
var velocities = MemoryManager.GetDouble3Array(partCount);

// Perform calculations...
UpdateOrbitalMechanics(positions, velocities, partCount);

// Return to pool when done
MemoryManager.ReturnDouble3Array(positions);
MemoryManager.ReturnDouble3Array(velocities);
```

**For Signal Data**:
```csharp
// Pool dictionaries to avoid GC pressure
var signalData = MemoryManager.GetDictionary();
signalData["thrust_vector"] = thrustVector.ToVector3();
signalData["mass"] = currentMass;

EmitSignal(SignalName.VesselUpdate, signalData);
MemoryManager.ReturnDictionary(signalData);
```

## Type Conversion Boundaries

### Double Precision Domain (C#)
- **Orbital mechanics calculations**
- **Position/velocity state vectors** 
- **Long-term numerical integration**
- **Celestial body parameters**

### Single Precision Domain (GDScript)
- **UI display values**
- **Visual effects parameters**
- **Audio system values**
- **Input handling**

### Conversion Points
```csharp
// Convert only at system boundaries
public void UpdateUI(Double3 orbitalPosition, Double3 orbitalVelocity)
{
    var uiData = MemoryManager.GetDictionary();
    
    // Single conversion per frame for UI
    uiData["display_position"] = orbitalPosition.ToVector3();
    uiData["display_velocity"] = orbitalVelocity.ToVector3();
    
    EmitSignal(SignalName.UIUpdate, uiData);
    MemoryManager.ReturnDictionary(uiData);
}
```

## Performance Validation Examples

### Signal Marshaling Test
```csharp
// Validate packed signal performance
var monitor = PerformanceMonitor.Instance;
double marshalingTime = monitor.MeasureSignalMarshalingPerformance(1000);

// Target: <0.5ms for 1000 operations
Debug.Assert(marshalingTime < 0.5, $"Signal marshaling too slow: {marshalingTime}ms");
```

### Memory Allocation Test
```csharp
// Validate memory management
var memResult = MemoryManager.ValidateMemoryPerformance();

Debug.Assert(memResult.MemoryWithinLimits, "Memory allocation exceeds frame budget");
Debug.Assert(memResult.GcWithinLimits, "Garbage collection during frame");
Debug.Assert(memResult.PoolEfficiency > 0.5, "Object pools underutilized");
```

### Conversion Performance Test
```csharp
// Validate Double3 conversion performance
double conversionTime = Double3.BenchmarkConversions(1000);

// Target: <0.1ms per 1000 operations  
Debug.Assert(conversionTime < 0.1, $"Conversion too slow: {conversionTime}ms");
```

## System Integration Patterns

### Physics System Boundary
```csharp
public partial class PhysicsManager : Node
{
    // C# handles all calculations
    private void FixedUpdate()
    {
        // High-precision orbital mechanics
        UpdateOrbitalStates(vessels, deltaTime);
        
        // Pack results for UI once per physics frame
        if (frameCount % UI_UPDATE_INTERVAL == 0)
        {
            EmitUIUpdateSignal();
        }
    }
    
    private void EmitUIUpdateSignal()
    {
        var uiData = MemoryManager.GetDictionary();
        
        foreach (var vessel in activeVessels)
        {
            var vesselData = MemoryManager.GetDictionary();
            vesselData["position"] = vessel.position.ToVector3();
            vesselData["velocity"] = vessel.velocity.ToVector3();
            vesselData["mass"] = vessel.mass;
            
            uiData[vessel.id] = vesselData;
        }
        
        EmitSignal(SignalName.PhysicsUpdate, uiData);
        
        // Clean up pooled objects
        foreach (var key in uiData.Keys)
        {
            MemoryManager.ReturnDictionary(uiData[key].AsGodotDictionary());
        }
        MemoryManager.ReturnDictionary(uiData);
    }
}
```

### UI System Boundary
```gdscript
extends Control
class_name VesselUI

var _vessel_displays: Dictionary = {}
var _update_queue: Array[Dictionary] = []

func _on_physics_update(vessel_data: Dictionary):
    # Queue UI updates for processing during _process()
    _update_queue.append(vessel_data)

func _process(delta):
    # Process all queued updates in single frame
    while not _update_queue.is_empty():
        var vessel_data = _update_queue.pop_front()
        _process_vessel_updates(vessel_data)

func _process_vessel_updates(vessel_data: Dictionary):
    for vessel_id in vessel_data.keys():
        var data = vessel_data[vessel_id]
        _update_vessel_display(vessel_id, data)

func _update_vessel_display(vessel_id: String, data: Dictionary):
    # Efficient UI update with batched operations
    var display = _vessel_displays.get(vessel_id)
    if display:
        display.update_all_values(data)  # Single update call
```

## Performance Monitoring Integration

### Continuous Validation
```csharp
// Integrate with PerformanceMonitor singleton
public override void _Ready()
{
    var monitor = PerformanceMonitor.Instance;
    monitor.SetApiValidationStatus("Integration Testing");
    
    // Initialize memory management
    MemoryManager.Initialize();
    
    // Validate initial performance
    ValidatePerformanceBoundaries();
}

private void ValidatePerformanceBoundaries()
{
    // Test conversion performance
    double conversionTime = Double3.BenchmarkConversions();
    Debug.Assert(conversionTime < 0.1, "Conversion performance failed");
    
    // Test signal marshaling
    var monitor = PerformanceMonitor.Instance;
    double signalingTime = monitor.MeasureSignalMarshalingPerformance();
    Debug.Assert(signalingTime < 0.5, "Signal marshaling performance failed");
    
    // Test memory management
    var memResult = MemoryManager.ValidateMemoryPerformance();
    Debug.Assert(memResult.MemoryWithinLimits && memResult.GcWithinLimits, 
                 "Memory management performance failed");
    
    monitor.SetApiValidationStatus("✅ Performance Boundaries Validated");
}
```

## Troubleshooting Common Issues

### Issue: Frame Drops During Physics Updates
**Cause**: Too many individual signals per frame
**Solution**: Implement packed signal pattern with update batching

### Issue: GC Pressure Causing Stutters  
**Cause**: Frequent allocation of temporary objects
**Solution**: Use MemoryManager object pools for all temporary allocations

### Issue: High Signal Marshaling Overhead
**Cause**: Complex nested data structures in signals
**Solution**: Flatten data into primitive types, use packed dictionaries

### Issue: Conversion Bottlenecks
**Cause**: Individual Double3 ↔ Vector3 conversions in tight loops
**Solution**: Use batch conversion methods and object pooling

## Performance Validation Checklist

- [ ] Signal frequency ≤1 packed signal per frame per system
- [ ] Memory allocation <1MB per frame
- [ ] Zero GC collections during normal operation
- [ ] Double3 conversion <0.1ms per 1000 operations  
- [ ] Signal marshaling <0.5ms per packed signal
- [ ] Object pools maintain >50% efficiency
- [ ] Frame time consistently ≤16.6ms
- [ ] Physics time ≤5ms, Script time ≤3ms

## Example Working Implementation

See `src/Core/CSharpTestNode.cs` and `src/Scripts/UIBridge.gd` for reference implementations demonstrating these patterns in practice.