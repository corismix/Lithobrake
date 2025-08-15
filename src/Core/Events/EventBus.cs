using System;

namespace Lithobrake.Core.Events;

/// <summary>
/// Simple event bus for decoupling circular dependencies between managers.
/// Addresses audit issues #51 (circular dependencies) and #52 (interface segregation).
/// Follows KISS principle with minimal event coordination.
/// </summary>
public static class EventBus
{
    // Origin shift events - replaces direct PhysicsManager <-> FloatingOriginManager coupling
    public static event Action<Double3, string>? OriginShiftRequested;
    public static event Action<Double3>? OriginShiftCompleted;
    public static event Action<Double3>? PreOriginShift;
    public static event Action<Double3>? PostOriginShift;
    
    // Dynamic pressure events - decouples Q calculations from anti-wobble system
    public static event Action<float>? DynamicPressureChanged;
    public static event Action<float>? QThresholdExceeded;
    public static event Action? QBelowThreshold;
    
    // Part catalog events - removes direct dependencies on PartCatalog changes
    public static event Action<string>? PartCatalogLoaded;
    public static event Action<string, int>? PartCatalogUpdated;
    public static event Action? PartCatalogClearRequested;
    
    // Vessel lifecycle events - simplifies vessel management coordination
    public static event Action<int>? VesselCreated;
    public static event Action<int>? VesselDestroyed;
    public static event Action<int, string>? VesselStateChanged;
    
    // Physics events - reduces PhysicsManager direct coupling
    public static event Action<double>? PhysicsTickCompleted;
    public static event Action<double>? PhysicsBudgetExceeded;
    
    /// <summary>
    /// Reset all event subscriptions. Call during scene transitions or cleanup.
    /// Prevents memory leaks from static event references.
    /// </summary>
    public static void Reset()
    {
        OriginShiftRequested = null;
        OriginShiftCompleted = null;
        PreOriginShift = null;
        PostOriginShift = null;
        
        DynamicPressureChanged = null;
        QThresholdExceeded = null;
        QBelowThreshold = null;
        
        PartCatalogLoaded = null;
        PartCatalogUpdated = null;
        PartCatalogClearRequested = null;
        
        VesselCreated = null;
        VesselDestroyed = null;
        VesselStateChanged = null;
        
        PhysicsTickCompleted = null;
        PhysicsBudgetExceeded = null;
    }
    
    /// <summary>
    /// Get count of total active subscriptions across all events.
    /// Useful for debugging memory leaks.
    /// </summary>
    public static int GetTotalSubscriberCount()
    {
        int count = 0;
        
        count += OriginShiftRequested?.GetInvocationList().Length ?? 0;
        count += OriginShiftCompleted?.GetInvocationList().Length ?? 0;
        count += PreOriginShift?.GetInvocationList().Length ?? 0;
        count += PostOriginShift?.GetInvocationList().Length ?? 0;
        
        count += DynamicPressureChanged?.GetInvocationList().Length ?? 0;
        count += QThresholdExceeded?.GetInvocationList().Length ?? 0;
        count += QBelowThreshold?.GetInvocationList().Length ?? 0;
        
        count += PartCatalogLoaded?.GetInvocationList().Length ?? 0;
        count += PartCatalogUpdated?.GetInvocationList().Length ?? 0;
        count += PartCatalogClearRequested?.GetInvocationList().Length ?? 0;
        
        count += VesselCreated?.GetInvocationList().Length ?? 0;
        count += VesselDestroyed?.GetInvocationList().Length ?? 0;
        count += VesselStateChanged?.GetInvocationList().Length ?? 0;
        
        count += PhysicsTickCompleted?.GetInvocationList().Length ?? 0;
        count += PhysicsBudgetExceeded?.GetInvocationList().Length ?? 0;
        
        return count;
    }
}