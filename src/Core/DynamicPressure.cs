using Godot;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Lithobrake.Core
{
    /// <summary>
    /// Dynamic pressure system implementing Q = 0.5 * ρ * v² calculations.
    /// Provides structural load tracking, auto-struts integration with AntiWobbleSystem,
    /// Max Q event monitoring, and atmospheric flight feedback for pilot awareness.
    /// Integrates with heating effects and atmospheric physics simulation.
    /// </summary>
    public partial class DynamicPressure : Node
    {
        // Singleton pattern for global access
        private static DynamicPressure? _instance;
        public static DynamicPressure Instance => _instance ?? throw new InvalidOperationException("DynamicPressure not initialized");
        
        // Dynamic pressure thresholds and constants
        public const double MAX_Q_WARNING_THRESHOLD = 20000.0; // 20 kPa - typical rocket structural limit
        public const double HIGH_Q_THRESHOLD = 15000.0; // 15 kPa - start warnings
        public const double MEDIUM_Q_THRESHOLD = 10000.0; // 10 kPa - moderate loads
        public const double LOW_Q_THRESHOLD = 5000.0; // 5 kPa - light loads
        
        // Integration with AntiWobbleSystem thresholds (from AntiWobbleSystem.cs)
        public const double AUTO_STRUTS_ENABLE_Q = 12000.0; // 12 kPa - enable anti-wobble
        public const double AUTO_STRUTS_DISABLE_Q = 8000.0; // 8 kPa - disable (hysteresis)
        
        // Max Q tracking
        private readonly Dictionary<PhysicsVessel, MaxQTracker> _vesselMaxQ = new();
        
        // Performance tracking
        private double _lastCalculationTime = 0.0;
        private int _qCalculationsThisFrame = 0;
        
        // Performance targets from current-task.md
        private const double Q_CALCULATION_BUDGET = 0.05; // ms per frame per vessel
        
        // Events for UI and other systems
        public static event Action<PhysicsVessel, double>? MaxQEvent;
        public static event Action<PhysicsVessel, double>? HighQWarning;
        public static event Action<PhysicsVessel, double>? QThresholdCrossed;
        
        public override void _Ready()
        {
            // Thread-safe singleton assignment
            if (Interlocked.CompareExchange(ref _instance, this, null) == null)
            {
                GD.Print("DynamicPressure: Initialized with Q tracking and auto-struts integration");
            }
            else
            {
                GD.PrintErr("DynamicPressure: Multiple instances detected!");
                QueueFree();
            }
        }
        
        /// <summary>
        /// Calculate dynamic pressure Q = 0.5 * ρ * v² for given conditions
        /// </summary>
        /// <param name="velocity">Velocity vector in m/s</param>
        /// <param name="density">Atmospheric density in kg/m³</param>
        /// <returns>Dynamic pressure in Pa</returns>
        public static double CalculateQ(Vector3 velocity, double density)
        {
            var startTime = Time.GetTicksMsec();
            
            if (density <= 0.0)
            {
                return 0.0; // No Q in vacuum
            }
            
            var velocitySquared = velocity.LengthSquared();
            var q = 0.5 * density * velocitySquared;
            
            // Performance monitoring
            if (Instance != null)
            {
                Instance._qCalculationsThisFrame++;
                Instance._lastCalculationTime = Time.GetTicksMsec() - startTime;
            }
            
            return q;
        }
        
        /// <summary>
        /// Calculate dynamic pressure for a vessel at its current position and velocity
        /// </summary>
        /// <param name="vessel">Vessel to calculate Q for</param>
        /// <returns>Dynamic pressure in Pa</returns>
        public static double CalculateVesselQ(PhysicsVessel vessel)
        {
            if (vessel?.RootPart?.RigidBody == null)
                return 0.0;
            
            // Get atmospheric properties at vessel position
            var atmosphericProperties = Atmosphere.GetVesselAtmosphericProperties(vessel);
            
            // Get vessel velocity
            var velocity = vessel.RootPart.RigidBody.LinearVelocity;
            
            return CalculateQ(velocity, atmosphericProperties.Density);
        }
        
        /// <summary>
        /// Update dynamic pressure tracking for vessel with event generation
        /// </summary>
        /// <param name="vessel">Vessel to update tracking for</param>
        public static void UpdateVesselQ(PhysicsVessel vessel)
        {
            if (vessel?.RootPart?.RigidBody == null || Instance == null)
                return;
            
            var currentQ = CalculateVesselQ(vessel);
            
            // Get or create Max Q tracker
            if (!Instance._vesselMaxQ.TryGetValue(vessel, out var tracker))
            {
                tracker = new MaxQTracker();
                Instance._vesselMaxQ[vessel] = tracker;
            }
            
            // Update Max Q tracking
            var previousMaxQ = tracker.MaxQ;
            tracker.Update(currentQ);
            
            // Generate Max Q event if new maximum reached
            if (tracker.MaxQ > previousMaxQ && tracker.MaxQ > MAX_Q_WARNING_THRESHOLD * 0.5)
            {
                MaxQEvent?.Invoke(vessel, tracker.MaxQ);
            }
            
            // Generate high Q warning
            if (currentQ > HIGH_Q_THRESHOLD && tracker.LastQ <= HIGH_Q_THRESHOLD)
            {
                HighQWarning?.Invoke(vessel, currentQ);
            }
            
            // Generate threshold crossing events for auto-struts integration
            CheckAutoStrutsThresholds(vessel, currentQ, tracker.LastQ);
            
            // Check structural limit warnings
            CheckStructuralLimits(vessel, currentQ);
        }
        
        /// <summary>
        /// Check auto-struts thresholds and trigger AntiWobbleSystem integration
        /// </summary>
        /// <param name="vessel">Vessel to check</param>
        /// <param name="currentQ">Current dynamic pressure</param>
        /// <param name="previousQ">Previous dynamic pressure</param>
        private static void CheckAutoStrutsThresholds(PhysicsVessel vessel, double currentQ, double previousQ)
        {
            // Enable auto-struts threshold crossed (upward)
            if (currentQ > AUTO_STRUTS_ENABLE_Q && previousQ <= AUTO_STRUTS_ENABLE_Q)
            {
                QThresholdCrossed?.Invoke(vessel, currentQ);
                GD.Print($"DynamicPressure: Auto-struts enable threshold crossed - Q={currentQ:F0} Pa");
            }
            
            // Disable auto-struts threshold crossed (downward) with hysteresis
            if (currentQ < AUTO_STRUTS_DISABLE_Q && previousQ >= AUTO_STRUTS_DISABLE_Q)
            {
                QThresholdCrossed?.Invoke(vessel, currentQ);
                GD.Print($"DynamicPressure: Auto-struts disable threshold crossed - Q={currentQ:F0} Pa");
            }
        }
        
        /// <summary>
        /// Check structural limits and generate warnings
        /// </summary>
        /// <param name="vessel">Vessel to check</param>
        /// <param name="currentQ">Current dynamic pressure</param>
        private static void CheckStructuralLimits(PhysicsVessel vessel, double currentQ)
        {
            if (currentQ > MAX_Q_WARNING_THRESHOLD)
            {
                GD.PrintErr($"DynamicPressure: STRUCTURAL LIMIT WARNING - Q={currentQ:F0} Pa (limit: {MAX_Q_WARNING_THRESHOLD:F0} Pa)");
            }
        }
        
        /// <summary>
        /// Get dynamic pressure category for UI display
        /// </summary>
        /// <param name="q">Dynamic pressure in Pa</param>
        /// <returns>Pressure category for display</returns>
        public static string GetQCategory(double q)
        {
            if (q >= MAX_Q_WARNING_THRESHOLD)
                return "CRITICAL";
            else if (q >= HIGH_Q_THRESHOLD)
                return "HIGH";
            else if (q >= MEDIUM_Q_THRESHOLD)
                return "MEDIUM";
            else if (q >= LOW_Q_THRESHOLD)
                return "LOW";
            else
                return "MINIMAL";
        }
        
        /// <summary>
        /// Get dynamic pressure color for UI visualization
        /// </summary>
        /// <param name="q">Dynamic pressure in Pa</param>
        /// <returns>Color representing pressure level</returns>
        public static Color GetQColor(double q)
        {
            if (q >= MAX_Q_WARNING_THRESHOLD)
                return new Color(1.0f, 0.0f, 0.0f); // Red - Critical
            else if (q >= HIGH_Q_THRESHOLD)
                return new Color(1.0f, 0.5f, 0.0f); // Orange - High
            else if (q >= MEDIUM_Q_THRESHOLD)
                return new Color(1.0f, 1.0f, 0.0f); // Yellow - Medium
            else if (q >= LOW_Q_THRESHOLD)
                return new Color(0.5f, 1.0f, 0.5f); // Light Green - Low
            else
                return new Color(0.0f, 1.0f, 0.0f); // Green - Minimal
        }
        
        /// <summary>
        /// Update heating effects based on dynamic pressure and velocity
        /// Called by HeatingEffects system
        /// </summary>
        /// <param name="vessel">Vessel to update heating for</param>
        public static void UpdateHeatingEffects(PhysicsVessel vessel)
        {
            if (vessel?.RootPart?.RigidBody == null)
                return;
            
            var currentQ = CalculateVesselQ(vessel);
            var velocity = vessel.RootPart.RigidBody.LinearVelocity;
            
            // Heating intensity scales with Q * velocity for realistic atmospheric entry effects
            var heatingIntensity = currentQ * velocity.Length();
            
            // Notify heating effects system (will be implemented in HeatingEffects.cs)
            HeatingEffects.UpdateVesselHeating(vessel, heatingIntensity);
        }
        
        /// <summary>
        /// Get vessel Max Q tracking data
        /// </summary>
        /// <param name="vessel">Vessel to get data for</param>
        /// <returns>Max Q tracker data</returns>
        public static MaxQData GetVesselMaxQData(PhysicsVessel vessel)
        {
            if (Instance == null || !Instance._vesselMaxQ.TryGetValue(vessel, out var tracker))
            {
                return new MaxQData
                {
                    CurrentQ = CalculateVesselQ(vessel),
                    MaxQ = 0.0,
                    MaxQAltitude = 0.0,
                    MaxQTime = 0.0
                };
            }
            
            return new MaxQData
            {
                CurrentQ = tracker.CurrentQ,
                MaxQ = tracker.MaxQ,
                MaxQAltitude = tracker.MaxQAltitude,
                MaxQTime = tracker.MaxQTime
            };
        }
        
        /// <summary>
        /// Clear vessel tracking data (for vessel destruction or reset)
        /// </summary>
        /// <param name="vessel">Vessel to clear tracking for</param>
        public static void ClearVesselTracking(PhysicsVessel vessel)
        {
            if (Instance != null)
            {
                Instance._vesselMaxQ.Remove(vessel);
            }
        }
        
        /// <summary>
        /// Get dynamic pressure system performance metrics
        /// </summary>
        /// <returns>Performance statistics</returns>
        public DynamicPressurePerformanceMetrics GetPerformanceMetrics()
        {
            return new DynamicPressurePerformanceMetrics
            {
                LastCalculationTime = _lastCalculationTime,
                CalculationsThisFrame = _qCalculationsThisFrame,
                CalculationBudget = Q_CALCULATION_BUDGET,
                IsWithinBudget = _lastCalculationTime <= Q_CALCULATION_BUDGET,
                TrackedVessels = _vesselMaxQ.Count,
                AverageTimePerCalculation = _qCalculationsThisFrame > 0 ? _lastCalculationTime / _qCalculationsThisFrame : 0.0
            };
        }
        
        /// <summary>
        /// Reset per-frame performance counters
        /// </summary>
        public override void _Process(double delta)
        {
            _qCalculationsThisFrame = 0;
        }
        
        /// <summary>
        /// Cleanup on exit
        /// </summary>
        public override void _ExitTree()
        {
            if (_instance == this)
            {
                // Clean up static events to prevent memory leaks
                MaxQEvent = null;
                HighQWarning = null;
                QThresholdCrossed = null;
                
                _instance = null;
            }
            
            base._ExitTree();
        }
    }
    
    /// <summary>
    /// Max Q tracking for individual vessels
    /// </summary>
    public class MaxQTracker
    {
        public double CurrentQ { get; private set; } = 0.0;
        public double MaxQ { get; private set; } = 0.0;
        public double MaxQAltitude { get; private set; } = 0.0;
        public double MaxQTime { get; private set; } = 0.0;
        public double LastQ { get; private set; } = 0.0;
        
        public void Update(double currentQ)
        {
            LastQ = CurrentQ;
            CurrentQ = currentQ;
            
            if (currentQ > MaxQ)
            {
                MaxQ = currentQ;
                MaxQTime = Time.GetUnixTimeFromSystem();
                // MaxQAltitude would need vessel position - simplified for now
            }
        }
    }
    
    /// <summary>
    /// Dynamic pressure data for vessel display
    /// </summary>
    public struct MaxQData
    {
        public double CurrentQ;
        public double MaxQ;
        public double MaxQAltitude;
        public double MaxQTime;
        
        /// <summary>
        /// Get Q in kPa for display
        /// </summary>
        public readonly double CurrentQInKPa => CurrentQ / 1000.0;
        public readonly double MaxQInKPa => MaxQ / 1000.0;
    }
    
    /// <summary>
    /// Performance metrics for dynamic pressure calculations
    /// </summary>
    public struct DynamicPressurePerformanceMetrics
    {
        public double LastCalculationTime;
        public int CalculationsThisFrame;
        public double CalculationBudget;
        public bool IsWithinBudget;
        public int TrackedVessels;
        public double AverageTimePerCalculation;
    }
}