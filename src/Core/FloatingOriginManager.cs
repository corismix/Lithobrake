using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lithobrake.Core
{
    /// <summary>
    /// Floating origin system for maintaining numerical precision during long-distance spaceflight.
    /// Monitors distance from origin and performs coordinate shifts during safe coast periods
    /// to prevent physics disruption while preserving velocities and maintaining visual continuity.
    /// </summary>
    public partial class FloatingOriginManager : Node
    {
        // Singleton instance
        private static FloatingOriginManager? _instance;
        
        // Distance monitoring and threshold management
        private const double OriginShiftThreshold = 20000.0; // 20km threshold from task requirements
        private const double SafetyBuffer = 5000.0; // 5km buffer to prevent frequent shifts
        private Double3 _currentWorldOrigin = Double3.Zero;
        private double _lastShiftTime = 0.0;
        private const double MinShiftInterval = 5.0; // Minimum 5 seconds between shifts
        
        // Coast period gating requirements
        private const double MaxDynamicPressureForShift = 1000.0; // 1 kPa max Q for safe shifts
        private const double MaxThrustForShift = 1.0; // 1 N max thrust for safe shifts
        private bool _lastShiftWasSafe = true;
        
        // Origin shift aware systems registry
        private readonly List<WeakReference<IOriginShiftAware>> _registeredSystems = new();
        private readonly object _registryLock = new object();
        private bool _registryNeedsCleanup = false;
        
        // Performance tracking
        private double _lastShiftDuration = 0.0;
        private int _totalShifts = 0;
        private double _totalShiftTime = 0.0;
        private const double MaxShiftDuration = 2.0; // 2ms performance target
        
        // Event system for coordinate shift coordination
        public static event Action<Double3>? OnPreOriginShift;
        public static event Action<Double3>? OnOriginShift; 
        public static event Action<Double3>? OnPostOriginShift;
        
        // Precision validation
        private Double3 _precisionTestPosition = Double3.Zero;
        private double _cumulativePrecisionError = 0.0;
        private const double MaxAcceptablePrecisionError = 1e-12; // From task requirements
        
        public override void _Ready()
        {
            if (_instance == null)
            {
                _instance = this;
                GD.Print("FloatingOriginManager: Singleton initialized");
            }
            else
            {
                GD.PrintErr("FloatingOriginManager: Multiple instances detected! This should be a singleton.");
                QueueFree();
                return;
            }
            
            // Initialize precision validation
            _precisionTestPosition = new Double3(1000000.0, 1000000.0, 1000000.0); // Large test coordinates
            _lastShiftTime = Time.GetUnixTimeFromSystem();
        }
        
        public override void _ExitTree()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
        
        /// <summary>
        /// Get the singleton instance of the FloatingOriginManager
        /// </summary>
        public static FloatingOriginManager? Instance => _instance;
        
        /// <summary>
        /// Monitor distance from origin and trigger shift if needed
        /// Called by physics systems to check if origin shift is required
        /// </summary>
        /// <param name="position">World position to check distance from origin</param>
        public static void MonitorOriginDistance(Double3 position)
        {
            if (_instance == null) return;
            
            double distanceFromOrigin = position.Length;
            
            if (distanceFromOrigin > OriginShiftThreshold + SafetyBuffer)
            {
                // Check if enough time has passed since last shift
                double currentTime = Time.GetUnixTimeFromSystem();
                if (currentTime - _instance._lastShiftTime > MinShiftInterval)
                {
                    // Check coast period conditions
                    if (_instance.IsInCoastPeriod())
                    {
                        Double3 shiftAmount = -position.Normalized * OriginShiftThreshold;
                        _instance.RequestOriginShift(shiftAmount);
                    }
                    else
                    {
                        GD.Print($"FloatingOriginManager: Origin shift needed but not in coast period (Q or thrust too high)");
                    }
                }
            }
        }
        
        /// <summary>
        /// Check if origin shift should be performed based on position and flight conditions
        /// </summary>
        /// <param name="position">Position to check</param>
        /// <param name="inCoastPeriod">Whether vessel is in safe coast period</param>
        /// <returns>True if shift should be performed</returns>
        public static bool ShouldPerformShift(Double3 position, bool inCoastPeriod)
        {
            if (_instance == null) return false;
            
            double distanceFromOrigin = position.Length;
            double currentTime = Time.GetUnixTimeFromSystem();
            
            // Check distance threshold
            if (distanceFromOrigin <= OriginShiftThreshold)
                return false;
            
            // Check time interval
            if (currentTime - _instance._lastShiftTime <= MinShiftInterval)
                return false;
            
            // Check coast period
            if (!inCoastPeriod)
                return false;
            
            return true;
        }
        
        /// <summary>
        /// Register a system to receive origin shift notifications
        /// </summary>
        /// <param name="system">System implementing IOriginShiftAware interface</param>
        public static void RegisterOriginShiftAware(IOriginShiftAware system)
        {
            if (_instance == null || system == null) return;
            
            lock (_instance._registryLock)
            {
                // Check if already registered
                if (system.IsRegistered) return;
                
                // Add to registry with weak reference to prevent memory leaks
                _instance._registeredSystems.Add(new WeakReference<IOriginShiftAware>(system));
                system.IsRegistered = true;
                
                GD.Print($"FloatingOriginManager: Registered {system.GetType().Name} for origin shift notifications");
            }
        }
        
        /// <summary>
        /// Unregister a system from origin shift notifications
        /// </summary>
        /// <param name="system">System to unregister</param>
        public static void UnregisterOriginShiftAware(IOriginShiftAware system)
        {
            if (_instance == null || system == null) return;
            
            lock (_instance._registryLock)
            {
                system.IsRegistered = false;
                _instance._registryNeedsCleanup = true; // Mark for cleanup on next shift
                
                GD.Print($"FloatingOriginManager: Unregistered {system.GetType().Name} from origin shift notifications");
            }
        }
        
        /// <summary>
        /// Perform an origin shift by the specified amount
        /// This is the main entry point for origin shift operations
        /// </summary>
        /// <param name="shiftAmount">Amount to shift the coordinate system</param>
        public static void PerformOriginShift(Double3 shiftAmount)
        {
            if (_instance == null) return;
            
            _instance.RequestOriginShift(shiftAmount);
        }
        
        /// <summary>
        /// Internal method to request and execute origin shift
        /// </summary>
        private void RequestOriginShift(Double3 shiftAmount)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                GD.Print($"FloatingOriginManager: Starting origin shift by {shiftAmount} (distance: {shiftAmount.Length:F1}m)");
                
                // Update world origin
                _currentWorldOrigin += shiftAmount;
                
                // Phase 1: Pre-shift notifications
                OnPreOriginShift?.Invoke(shiftAmount);
                
                // Phase 2: Main shift notifications (sorted by priority)
                NotifyRegisteredSystems(shiftAmount);
                
                // Phase 3: Post-shift notifications
                OnPostOriginShift?.Invoke(shiftAmount);
                
                // Update precision validation
                UpdatePrecisionValidation(shiftAmount);
                
                // Update statistics
                _totalShifts++;
                _lastShiftTime = Time.GetUnixTimeFromSystem();
                _lastShiftWasSafe = true;
                
                stopwatch.Stop();
                _lastShiftDuration = stopwatch.Elapsed.TotalMilliseconds;
                _totalShiftTime += _lastShiftDuration;
                
                GD.Print($"FloatingOriginManager: Origin shift completed in {_lastShiftDuration:F3}ms (total shifts: {_totalShifts})");
                
                // Check performance target
                if (_lastShiftDuration > MaxShiftDuration)
                {
                    GD.PrintErr($"FloatingOriginManager: Shift exceeded performance target ({_lastShiftDuration:F3}ms > {MaxShiftDuration:F1}ms)");
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _lastShiftWasSafe = false;
                GD.PrintErr($"FloatingOriginManager: Origin shift failed with exception: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Notify all registered systems of the origin shift
        /// </summary>
        private void NotifyRegisteredSystems(Double3 shiftAmount)
        {
            lock (_registryLock)
            {
                // Clean up dead references if needed
                if (_registryNeedsCleanup)
                {
                    CleanupRegisteredSystems();
                    _registryNeedsCleanup = false;
                }
                
                // Get all valid systems and sort by priority
                var validSystems = new List<IOriginShiftAware>();
                
                foreach (var weakRef in _registeredSystems)
                {
                    if (weakRef.TryGetTarget(out var system) && system.ShouldReceiveOriginShifts)
                    {
                        validSystems.Add(system);
                    }
                }
                
                // Sort by priority (lower values first)
                validSystems.Sort((a, b) => a.ShiftPriority.CompareTo(b.ShiftPriority));
                
                // Notify systems in priority order
                int notificationCount = 0;
                var notificationStopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                foreach (var system in validSystems)
                {
                    try
                    {
                        system.HandleOriginShift(shiftAmount);
                        notificationCount++;
                    }
                    catch (Exception ex)
                    {
                        GD.PrintErr($"FloatingOriginManager: Error notifying {system.GetType().Name}: {ex.Message}");
                    }
                }
                
                notificationStopwatch.Stop();
                double notificationTime = notificationStopwatch.Elapsed.TotalMilliseconds;
                
                if (notificationTime > 0.5) // 0.5ms target for notifications
                {
                    GD.PrintErr($"FloatingOriginManager: System notifications took {notificationTime:F3}ms (target: 0.5ms)");
                }
                
                GD.Print($"FloatingOriginManager: Notified {notificationCount} systems in {notificationTime:F3}ms");
            }
        }
        
        /// <summary>
        /// Clean up dead weak references from the registry
        /// </summary>
        private void CleanupRegisteredSystems()
        {
            int originalCount = _registeredSystems.Count;
            
            for (int i = _registeredSystems.Count - 1; i >= 0; i--)
            {
                if (!_registeredSystems[i].TryGetTarget(out var _))
                {
                    _registeredSystems.RemoveAt(i);
                }
            }
            
            int cleanedUp = originalCount - _registeredSystems.Count;
            if (cleanedUp > 0)
            {
                GD.Print($"FloatingOriginManager: Cleaned up {cleanedUp} dead references from registry");
            }
        }
        
        /// <summary>
        /// Check if the system is currently in a safe coast period for origin shifts
        /// </summary>
        private bool IsInCoastPeriod()
        {
            // For this implementation, we'll use simplified logic
            // In a full implementation, this would check actual vessel states
            
            // TODO: Check dynamic pressure from atmospheric conditions
            // TODO: Check thrust levels from active vessels
            // TODO: Check if any critical operations are in progress
            
            // For now, assume safe coast period if no high dynamic pressure detected
            // This would need to be integrated with vessel systems
            return true; // Simplified for initial implementation
        }
        
        /// <summary>
        /// Update precision validation after origin shift
        /// </summary>
        private void UpdatePrecisionValidation(Double3 shiftAmount)
        {
            // Apply shift to test position
            _precisionTestPosition += shiftAmount;
            
            // Calculate expected position after shift
            var expectedPosition = new Double3(1000000.0, 1000000.0, 1000000.0) + (_currentWorldOrigin);
            
            // Calculate precision error
            double currentError = Double3.Distance(_precisionTestPosition, expectedPosition);
            _cumulativePrecisionError += currentError;
            
            if (currentError > MaxAcceptablePrecisionError)
            {
                GD.PrintErr($"FloatingOriginManager: Precision error exceeds threshold: {currentError:E} > {MaxAcceptablePrecisionError:E}");
            }
            
            // Reset precision test if error gets too large
            if (_cumulativePrecisionError > MaxAcceptablePrecisionError * 100)
            {
                GD.Print("FloatingOriginManager: Resetting precision validation test");
                _precisionTestPosition = new Double3(1000000.0, 1000000.0, 1000000.0);
                _cumulativePrecisionError = 0.0;
            }
        }
        
        /// <summary>
        /// Get current world origin offset
        /// </summary>
        public static Double3 GetWorldOrigin()
        {
            return _instance?._currentWorldOrigin ?? Double3.Zero;
        }
        
        /// <summary>
        /// Get origin shift statistics
        /// </summary>
        public static OriginShiftStats GetStats()
        {
            if (_instance == null)
            {
                return new OriginShiftStats();
            }
            
            return new OriginShiftStats
            {
                TotalShifts = _instance._totalShifts,
                LastShiftDuration = _instance._lastShiftDuration,
                AverageShiftDuration = _instance._totalShifts > 0 ? _instance._totalShiftTime / _instance._totalShifts : 0.0,
                RegisteredSystemCount = _instance._registeredSystems.Count,
                CurrentWorldOrigin = _instance._currentWorldOrigin,
                LastShiftWasSafe = _instance._lastShiftWasSafe,
                CumulativePrecisionError = _instance._cumulativePrecisionError,
                TimeSinceLastShift = Time.GetUnixTimeFromSystem() - _instance._lastShiftTime
            };
        }
        
        /// <summary>
        /// Force an origin shift for testing purposes
        /// Should only be used in test scenarios
        /// </summary>
        public static void ForceOriginShiftForTesting(Double3 shiftAmount)
        {
            if (_instance == null) return;
            
            GD.Print($"FloatingOriginManager: Forcing origin shift for testing: {shiftAmount}");
            _instance.RequestOriginShift(shiftAmount);
        }
        
        /// <summary>
        /// Reset the floating origin system to initial state
        /// Useful for testing or when starting new missions
        /// </summary>
        public static void Reset()
        {
            if (_instance == null) return;
            
            lock (_instance._registryLock)
            {
                // Clear all registered systems
                foreach (var weakRef in _instance._registeredSystems)
                {
                    if (weakRef.TryGetTarget(out var system))
                    {
                        system.IsRegistered = false;
                    }
                }
                _instance._registeredSystems.Clear();
                
                // Reset state
                _instance._currentWorldOrigin = Double3.Zero;
                _instance._totalShifts = 0;
                _instance._totalShiftTime = 0.0;
                _instance._lastShiftDuration = 0.0;
                _instance._lastShiftTime = Time.GetUnixTimeFromSystem();
                _instance._lastShiftWasSafe = true;
                _instance._precisionTestPosition = new Double3(1000000.0, 1000000.0, 1000000.0);
                _instance._cumulativePrecisionError = 0.0;
                
                GD.Print("FloatingOriginManager: System reset to initial state");
            }
        }
    }
    
    /// <summary>
    /// Statistics for origin shift operations
    /// </summary>
    public struct OriginShiftStats
    {
        public int TotalShifts;
        public double LastShiftDuration;
        public double AverageShiftDuration;
        public int RegisteredSystemCount;
        public Double3 CurrentWorldOrigin;
        public bool LastShiftWasSafe;
        public double CumulativePrecisionError;
        public double TimeSinceLastShift;
        
        public override string ToString()
        {
            return $"OriginShiftStats: {TotalShifts} shifts, avg {AverageShiftDuration:F3}ms, " +
                   $"{RegisteredSystemCount} systems, origin offset {CurrentWorldOrigin.Length:F1}m";
        }
    }
}