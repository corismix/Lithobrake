using System;

namespace Lithobrake.Core.Events;

/// <summary>
/// Core interface for systems that can handle floating origin shifts.
/// Separated from management concerns following Interface Segregation Principle.
/// Addresses audit issue #52.
/// </summary>
public interface IOriginShiftable
{
    /// <summary>
    /// Handle a floating origin shift by updating internal coordinates.
    /// This method is called during origin shift operations to maintain coordinate consistency.
    /// </summary>
    /// <param name="deltaPosition">The coordinate shift amount to apply to positions</param>
    void HandleOriginShift(Double3 deltaPosition);
}

/// <summary>
/// Interface for systems that need priority ordering during origin shifts.
/// Separate from shift handling to allow independent implementation.
/// </summary>
public interface IOriginShiftPriority
{
    /// <summary>
    /// Priority order for origin shift notifications.
    /// Lower values are processed first. Critical systems should use negative values.
    /// Default priority is 0.
    /// </summary>
    int OriginShiftPriority { get; }
}

/// <summary>
/// Interface for systems that can be enabled/disabled for origin shift notifications.
/// Allows temporal control without affecting registration state.
/// </summary>
public interface IOriginShiftNotifiable
{
    /// <summary>
    /// Whether this object should receive origin shift notifications.
    /// Can be used to temporarily disable updates without unregistering.
    /// </summary>
    bool ShouldReceiveOriginShifts { get; }
}

/// <summary>
/// Interface for systems that track their registration state.
/// Separated from core functionality for optional use.
/// </summary>
public interface IOriginShiftRegistration
{
    /// <summary>
    /// Whether this object is currently registered with the FloatingOriginManager.
    /// Used for automatic cleanup and preventing duplicate registrations.
    /// </summary>
    bool IsRegistered { get; set; }
}

/// <summary>
/// Composite interface for systems that need all origin shift capabilities.
/// Convenience interface that combines all separated interfaces.
/// Use this for systems that need full origin shift functionality.
/// </summary>
public interface IFullOriginShiftAware : IOriginShiftable, IOriginShiftPriority, IOriginShiftNotifiable, IOriginShiftRegistration
{
}

/// <summary>
/// Priority levels for origin shift notifications.
/// Systems with higher priority (lower values) are updated first.
/// </summary>
public static class OriginShiftPriority
{
    /// <summary>
    /// Critical systems that must be updated first (physics, orbital mechanics)
    /// </summary>
    public const int Critical = -1000;
    
    /// <summary>
    /// Important systems updated after critical ones (vessels, celestial bodies)
    /// </summary>
    public const int Important = -500;
    
    /// <summary>
    /// Normal priority systems (cameras, UI elements)
    /// </summary>
    public const int Normal = 0;
    
    /// <summary>
    /// Low priority systems (effects, non-critical visualization)
    /// </summary>
    public const int Low = 500;
    
    /// <summary>
    /// Background systems updated last (logging, analytics)
    /// </summary>
    public const int Background = 1000;
}