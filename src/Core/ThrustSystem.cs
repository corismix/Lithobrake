using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lithobrake.Core
{
    /// <summary>
    /// Centralized thrust system for managing engine thrust calculations and force application.
    /// Handles realistic thrust physics, force distribution, and performance monitoring.
    /// Integrates with PhysicsVessel for accurate force application at engine mount points.
    /// </summary>
    public static class ThrustSystem
    {
        // Performance monitoring
        private static double _lastCalculationTime = 0.0;
        private static int _enginesProcessed = 0;
        
        // Performance targets from current-task.md
        private const double ThrustCalculationBudget = 0.2; // ms per frame per engine
        private const double PhysicsIntegrationBudget = 0.3; // ms additional to physics budget
        
        // Physics constants
        private const double StandardGravity = UNIVERSE_CONSTANTS.KERBIN_STANDARD_GRAVITY;
        
        /// <summary>
        /// Calculate total thrust for a vessel's engines
        /// </summary>
        /// <param name="engines">List of engines to calculate thrust for</param>
        /// <param name="throttle">Global throttle setting (0-1)</param>
        /// <param name="atmosphericPressure">Current atmospheric pressure in Pa</param>
        /// <returns>Total thrust in Newtons and thrust vector</returns>
        public static ThrustResult CalculateVesselThrust(IEnumerable<Engine> engines, double throttle, double atmosphericPressure)
        {
            var startTime = Time.GetTicksMsec();
            var result = new ThrustResult();
            
            if (engines == null)
                return result;
            
            // Use foreach loop instead of LINQ to avoid allocation pressure in 60Hz loop
            var activeEngines = new List<Engine>();
            foreach (var engine in engines)
            {
                if (engine != null && !engine.IsQueuedForDeletion())
                {
                    activeEngines.Add(engine);
                }
            }
            _enginesProcessed = activeEngines.Count;
            
            foreach (var engine in activeEngines)
            {
                var engineThrust = CalculateEngineThrust(engine, throttle, atmosphericPressure);
                
                if (engineThrust.Magnitude > 0)
                {
                    result.TotalThrust += engineThrust.Magnitude;
                    result.ThrustVector += engineThrust.ThrustVector;
                    result.ActiveEngines++;
                    
                    // Track individual engine thrust for force application
                    result.EngineThrustData.Add(new EngineThrustData(engine)
                    {
                        Thrust = engineThrust.Magnitude,
                        ThrustVector = engineThrust.ThrustVector,
                        MountPosition = engine.GlobalPosition
                    });
                }
            }
            
            // Performance monitoring
            var duration = Time.GetTicksMsec() - startTime;
            _lastCalculationTime = duration;
            
            if (duration > ThrustCalculationBudget * activeEngines.Count)
            {
                GD.PrintErr($"ThrustSystem: Calculation exceeded budget - {duration:F2}ms for {activeEngines.Count} engines (target: {ThrustCalculationBudget * activeEngines.Count:F2}ms)");
            }
            
            return result;
        }
        
        /// <summary>
        /// Calculate thrust for a single engine
        /// </summary>
        /// <param name="engine">Engine to calculate thrust for</param>
        /// <param name="throttle">Throttle setting (0-1)</param>
        /// <param name="atmosphericPressure">Atmospheric pressure in Pa</param>
        /// <returns>Engine thrust data</returns>
        public static EngineThrust CalculateEngineThrust(Engine engine, double throttle, double atmosphericPressure)
        {
            var result = new EngineThrust();
            
            if (engine == null || engine.IsQueuedForDeletion())
                return result;
            
            // Get thrust magnitude from engine's GetThrust method (uses realistic rocket equation)
            result.Magnitude = engine.GetThrust(throttle, atmosphericPressure);
            
            if (result.Magnitude > 0)
            {
                // Calculate thrust direction with gimbal
                var baseDirection = -engine.Transform.Basis.Y; // Engines point down
                var gimbalRotation = GetEngineGimbalRotation(engine);
                result.ThrustVector = gimbalRotation * baseDirection * (float)result.Magnitude;
                
                // Calculate efficiency for visualization
                result.Efficiency = CalculateThrustEfficiency(atmosphericPressure);
            }
            
            return result;
        }
        
        /// <summary>
        /// Apply thrust forces to a physics vessel
        /// </summary>
        /// <param name="vessel">Physics vessel to apply forces to</param>
        /// <param name="thrustResult">Thrust calculation result</param>
        public static void ApplyThrustForces(PhysicsVessel vessel, ThrustResult thrustResult)
        {
            var startTime = Time.GetTicksMsec();
            
            if (vessel == null || thrustResult.EngineThrustData.Count == 0)
                return;
            
            // Apply forces at each engine mount point for realistic physics
            foreach (var engineThrust in thrustResult.EngineThrustData)
            {
                if (engineThrust.Engine.RigidBody != null && engineThrust.Thrust > 0)
                {
                    // Convert thrust vector to world coordinates
                    var worldThrustVector = engineThrust.ThrustVector;
                    
                    // Apply force at engine mount position
                    var mountPosition = engineThrust.MountPosition - engineThrust.Engine.RigidBody.GlobalPosition;
                    engineThrust.Engine.RigidBody.ApplyForce(worldThrustVector, mountPosition);
                }
            }
            
            // Performance monitoring
            var duration = Time.GetTicksMsec() - startTime;
            
            if (duration > PhysicsIntegrationBudget)
            {
                GD.PrintErr($"ThrustSystem: Physics integration exceeded budget - {duration:F2}ms (target: {PhysicsIntegrationBudget:F2}ms)");
            }
        }
        
        /// <summary>
        /// Calculate fuel consumption for engines based on current thrust
        /// Uses formula: consumption = thrust / (Isp * g0)
        /// </summary>
        /// <param name="engines">Engines to calculate fuel consumption for</param>
        /// <param name="deltaTime">Time step in seconds</param>
        /// <returns>Total fuel consumption in kg</returns>
        public static double CalculateFuelConsumption(IEnumerable<Engine> engines, double deltaTime)
        {
            var totalConsumption = 0.0;
            
            if (engines == null || deltaTime <= 0)
                return totalConsumption;
            
            // Use foreach loop instead of LINQ to avoid allocation pressure
            foreach (var engine in engines)
            {
                if (engine == null || engine.IsQueuedForDeletion())
                    continue;
                if (engine.IsActive && engine.HasFuel && engine.CurrentThrust > 0)
                {
                    // Formula: consumption = thrust / (Isp * g0)
                    var massFlowRate = engine.CurrentThrust / (engine.SpecificImpulse * StandardGravity);
                    var fuelConsumption = massFlowRate * deltaTime;
                    
                    totalConsumption += fuelConsumption;
                }
            }
            
            return totalConsumption;
        }
        
        /// <summary>
        /// Get gimbal rotation for engine thrust vectoring
        /// </summary>
        /// <param name="engine">Engine to get gimbal rotation for</param>
        /// <returns>Gimbal rotation basis</returns>
        private static Basis GetEngineGimbalRotation(Engine engine)
        {
            if (!engine.CanGimbal || engine.GimbalAngle.Length() < 0.001f)
                return Basis.Identity;
            
            var pitchRad = Mathf.DegToRad(engine.GimbalAngle.X);
            var yawRad = Mathf.DegToRad(engine.GimbalAngle.Z);
            
            var pitchBasis = new Basis(Vector3.Right, pitchRad);
            var yawBasis = new Basis(Vector3.Up, yawRad);
            
            return yawBasis * pitchBasis;
        }
        
        /// <summary>
        /// Calculate thrust efficiency based on atmospheric pressure
        /// </summary>
        /// <param name="atmosphericPressure">Atmospheric pressure in Pa</param>
        /// <returns>Efficiency factor (0-1)</returns>
        private static double CalculateThrustEfficiency(double atmosphericPressure)
        {
            var seaLevelPressure = 101325.0; // Pa
            var pressureRatio = Math.Clamp(atmosphericPressure / seaLevelPressure, 0.0, 1.0);
            
            // Linear interpolation: sea level efficiency 0.85, vacuum efficiency 1.0
            return 0.85 + (0.15 * (1.0 - pressureRatio));
        }
        
        /// <summary>
        /// Get thrust system performance metrics
        /// </summary>
        /// <returns>Performance statistics</returns>
        public static ThrustSystemMetrics GetPerformanceMetrics()
        {
            return new ThrustSystemMetrics
            {
                LastCalculationTime = _lastCalculationTime,
                EnginesProcessed = _enginesProcessed,
                CalculationBudget = ThrustCalculationBudget,
                PhysicsIntegrationBudget = PhysicsIntegrationBudget,
                IsWithinBudget = _lastCalculationTime <= (ThrustCalculationBudget * _enginesProcessed)
            };
        }
    }
    
    /// <summary>
    /// Result of thrust calculations for a vessel
    /// </summary>
    public class ThrustResult
    {
        public double TotalThrust { get; set; } = 0.0;
        public Vector3 ThrustVector { get; set; } = Vector3.Zero;
        public int ActiveEngines { get; set; } = 0;
        public List<EngineThrustData> EngineThrustData { get; set; } = new();
    }
    
    /// <summary>
    /// Thrust data for individual engine
    /// </summary>
    public struct EngineThrust
    {
        public double Magnitude { get; set; }
        public Vector3 ThrustVector { get; set; }
        public double Efficiency { get; set; }
    }
    
    /// <summary>
    /// Engine thrust data for force application
    /// </summary>
    public class EngineThrustData
    {
        private Engine? _engine;
        public Engine Engine 
        { 
            get => _engine ?? throw new InvalidOperationException("Engine not initialized in EngineThrustData");
            set => _engine = value ?? throw new ArgumentNullException(nameof(value));
        }
        
        public double Thrust { get; set; }
        public Vector3 ThrustVector { get; set; }
        public Vector3 MountPosition { get; set; }
        
        /// <summary>
        /// Constructor requiring engine to be set
        /// </summary>
        public EngineThrustData(Engine engine)
        {
            Engine = engine; // Uses the setter with validation
        }
        
        /// <summary>
        /// Checks if the engine is valid and can be safely used
        /// </summary>
        public bool IsEngineValid => SafeOperations.IsValid(_engine, "EngineThrustData.Engine");
    }
    
    /// <summary>
    /// Thrust system performance metrics
    /// </summary>
    public struct ThrustSystemMetrics
    {
        public double LastCalculationTime;
        public int EnginesProcessed;
        public double CalculationBudget;
        public double PhysicsIntegrationBudget;
        public bool IsWithinBudget;
    }
}