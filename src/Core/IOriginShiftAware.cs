using System;

namespace Lithobrake.Core
{
    /// <summary>
    /// Interface for systems that need to be notified of floating origin shifts.
    /// Implementers will receive coordinate update notifications when the floating origin system
    /// shifts the world coordinate system to maintain numerical precision.
    /// </summary>
    public interface IOriginShiftAware
    {
        /// <summary>
        /// Handle a floating origin shift by updating internal coordinates.
        /// This method is called during origin shift operations to maintain coordinate consistency.
        /// </summary>
        /// <param name="deltaPosition">The coordinate shift amount to apply to positions</param>
        void HandleOriginShift(Double3 deltaPosition);
        
        /// <summary>
        /// Whether this object is currently registered with the FloatingOriginManager.
        /// Used for automatic cleanup and preventing duplicate registrations.
        /// </summary>
        bool IsRegistered { get; set; }
        
        /// <summary>
        /// Priority order for origin shift notifications.
        /// Lower values are processed first. Critical systems should use negative values.
        /// Default priority is 0.
        /// </summary>
        int ShiftPriority => 0;
        
        /// <summary>
        /// Whether this object should receive origin shift notifications.
        /// Can be used to temporarily disable updates without unregistering.
        /// </summary>
        bool ShouldReceiveOriginShifts => IsRegistered;
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
}