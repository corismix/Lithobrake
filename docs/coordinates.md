# Coordinate System Specifications

## Overview

Lithobrake uses a unified coordinate system designed for orbital mechanics simulation with floating origin support for precision maintenance. This document specifies the coordinate transformations and precision boundaries between different subsystems.

## Coordinate Systems

### 1. World Coordinates (Godot Vector3)
- **Type**: `Vector3` (32-bit float)
- **Origin**: Floating origin that shifts at 20km threshold
- **Scale**: 1 unit = 1 meter
- **Usage**: Physics simulation, rendering, collision detection
- **Precision**: ~7 significant digits (float32)

### 2. Orbital Coordinates (Double3)
- **Type**: `Double3` (64-bit double precision)
- **Origin**: Fixed at planetary center (0, 0, 0)
- **Scale**: 1 unit = 1 meter
- **Usage**: Orbital mechanics, Kepler solving, position propagation
- **Precision**: ~15 significant digits (float64)

### 3. UI Coordinates (Godot Vector2)
- **Type**: `Vector2` (32-bit float)
- **Origin**: Screen space (0, 0) at top-left
- **Scale**: 1 unit = 1 pixel
- **Usage**: User interface, HUD elements, navigation displays

## Coordinate Transformations

### World ↔ Orbital Conversion
```csharp
// World to Orbital (add floating origin offset)
Double3 orbitalPos = worldPos.ToDouble3() + FloatingOrigin.CurrentOffset;

// Orbital to World (subtract floating origin offset)
Vector3 worldPos = (orbitalPos - FloatingOrigin.CurrentOffset).ToVector3();
```

### Floating Origin Management
- **Threshold**: 20,000m (20km) from current origin
- **Shift Conditions**: Only during coast periods (no active physics)
- **Update Order**: Orbital → Physics → Rendering → UI
- **Precision Target**: Maintain <1mm precision at all distances

### Performance Boundaries
- **Conversion Limit**: <0.01ms per 1000 conversions
- **Maximum Distance**: 2.1 billion meters (float32 limit)
- **Orbital Accuracy**: <1m error at 100km orbit altitude

## Reference Frames

### 1. Kerbin-Centered Inertial (KCI)
- **Origin**: Kerbin center of mass
- **Axes**: X-right, Y-up, Z-forward (right-handed)
- **Rotation**: Fixed inertial frame (non-rotating)
- **Usage**: Orbital mechanics calculations

### 2. Kerbin-Centered Fixed (KCF)
- **Origin**: Kerbin center of mass
- **Axes**: X-right, Y-up, Z-forward (right-handed)  
- **Rotation**: Rotates with Kerbin (sidereal day)
- **Usage**: Surface navigation, landing coordinates

### 3. Vessel Local Frame
- **Origin**: Vessel center of mass
- **Axes**: X-right, Y-up, Z-forward relative to vessel orientation
- **Rotation**: Follows vessel attitude
- **Usage**: Control inputs, part placement, physics forces

## Precision Requirements

### Orbital Mechanics
- **Position**: ±1m accuracy at 100km altitude
- **Velocity**: ±0.1m/s accuracy for orbital calculations
- **Time**: ±0.01s accuracy over 10-orbit periods
- **Energy Conservation**: <1% drift over extended simulation

### Physics Simulation
- **Joint Stability**: <1cm wobble under maximum thrust
- **Mass Conservation**: Exact during staging operations
- **Force Application**: ±1N accuracy for control forces
- **Collision Detection**: 1cm minimum detection threshold

### Rendering and UI
- **Visual Accuracy**: 1 pixel = 1m at 1km distance
- **UI Updates**: 60Hz refresh rate for critical displays
- **Camera Smoothing**: <100ms latency for view changes
- **Distance Display**: Appropriate unit scaling (m, km, Mm)

## Validation and Testing

### Coordinate Accuracy Tests
- Round-trip conversion accuracy (World ↔ Orbital)
- Floating origin shift precision maintenance
- Cross-system coordinate consistency validation
- Numerical precision degradation over time

### Performance Benchmarks
- Conversion performance under load (1000+ conversions/frame)
- Memory allocation during coordinate operations
- Floating origin shift performance impact
- UI coordinate update efficiency at 60Hz

## Error Handling

### Precision Loss Detection
- Monitor for coordinate drift during long simulations
- Detect floating point precision degradation
- Alert on coordinate system inconsistencies
- Automatic precision recovery mechanisms

### Boundary Conditions
- Handle extreme distances gracefully (>1 million km)
- Manage coordinate overflow conditions
- Validate all coordinate transformations
- Fallback to double precision when needed

## Dependencies

- **Double3.cs**: Custom double-precision vector implementation
- **FloatingOrigin**: Origin management system (to be implemented)
- **Godot Vector3/Vector2**: Built-in coordinate types
- **Physics System**: Jolt physics integration
- **Orbital System**: Kepler solver and propagation