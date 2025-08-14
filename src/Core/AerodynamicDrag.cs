using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lithobrake.Core
{
    /// <summary>
    /// Aerodynamic drag system implementing realistic drag calculations for rocket parts.
    /// Provides drag force calculations using Force = 0.5 * ρ * v² * Cd * A,
    /// part-specific drag coefficients, and effective cross-sectional area computation.
    /// Integrates with atmospheric system and physics simulation for accurate flight dynamics.
    /// </summary>
    public partial class AerodynamicDrag : Node
    {
        // Singleton pattern for global access
        private static AerodynamicDrag? _instance;
        public static AerodynamicDrag Instance => _instance ?? throw new InvalidOperationException("AerodynamicDrag not initialized");
        
        // Drag coefficient presets by part shape category
        public static class DragCoefficients
        {
            // Basic shape categories with realistic Cd values
            public const double Streamlined = 0.3; // Aerodynamic parts like fairings, nosecones
            public const double Blunt = 1.0; // Cylindrical tanks, command pods
            public const double Fairings = 0.2; // Enclosed fairings and streamlined surfaces
            public const double Engine = 1.2; // Engine bells and complex shapes
            public const double Fins = 0.05; // Fins and control surfaces (per unit area)
            public const double Solar = 1.3; // Solar panels and flat surfaces
            
            // Supersonic vs subsonic multipliers
            public const double SupersonicMultiplier = 1.8; // Increased drag at Mach > 1
            public const double TransonicMultiplier = 2.2; // Peak drag around Mach 1
            public const double TransonicStartMach = 0.8; // Start of transonic region
            public const double TransonicEndMach = 1.2; // End of transonic region
        }
        
        // Performance tracking
        private double _lastCalculationTime = 0.0;
        private int _dragCalculationsThisFrame = 0;
        
        // Performance targets from current-task.md
        private const double DRAG_CALCULATION_BUDGET = 0.2; // ms per frame for all parts in vessel
        
        public override void _Ready()
        {
            if (_instance == null)
            {
                _instance = this;
                GD.Print("AerodynamicDrag: Initialized with realistic drag coefficients and cross-sectional area calculation");
            }
            else
            {
                GD.PrintErr("AerodynamicDrag: Multiple instances detected!");
                QueueFree();
            }
        }
        
        /// <summary>
        /// Calculate drag force for a single part using Force = 0.5 * ρ * v² * Cd * A
        /// Applied opposite to velocity vector with atmospheric density consideration
        /// </summary>
        /// <param name="part">Part to calculate drag for</param>
        /// <param name="velocity">Part velocity in m/s</param>
        /// <param name="atmosphericProperties">Atmospheric conditions</param>
        /// <returns>Drag force vector in Newtons</returns>
        public static Vector3 CalculateDragForce(Part part, Vector3 velocity, AtmosphericProperties atmosphericProperties)
        {
            var startTime = Time.GetTicksMsec();
            
            // Early exit for vacuum or stationary
            if (atmosphericProperties.Density <= 0.0 || velocity.LengthSquared() < 1e-6)
            {
                return Vector3.Zero;
            }
            
            // Get part drag properties
            var dragCoefficient = GetPartDragCoefficient(part, atmosphericProperties, velocity);
            var crossSectionalArea = GetPartCrossSectionalArea(part, velocity);
            var velocitySquared = velocity.LengthSquared();
            
            // Calculate drag magnitude: Force = 0.5 * ρ * v² * Cd * A
            var dragMagnitude = 0.5 * atmosphericProperties.Density * velocitySquared * dragCoefficient * crossSectionalArea;
            
            // Apply drag opposite to velocity direction
            var dragDirection = velocity.Normalized() * -1.0f;
            var dragForce = dragDirection * (float)dragMagnitude;
            
            // Performance monitoring
            if (Instance != null)
            {
                Instance._dragCalculationsThisFrame++;
                Instance._lastCalculationTime = Time.GetTicksMsec() - startTime;
            }
            
            return dragForce;
        }
        
        /// <summary>
        /// Apply drag forces to all parts in a vessel
        /// </summary>
        /// <param name="vessel">Vessel to apply drag forces to</param>
        public static void ApplyDragForces(PhysicsVessel vessel)
        {
            if (vessel?.Parts == null || !vessel.Parts.Any())
                return;
            
            var startTime = Time.GetTicksMsec();
            
            // Get atmospheric properties at vessel position
            var atmosphericProperties = Atmosphere.GetVesselAtmosphericProperties(vessel);
            
            // Early exit for vacuum
            if (atmosphericProperties.Density <= 0.0)
                return;
            
            // Apply drag to each part
            foreach (var part in vessel.Parts)
            {
                if (part?.RigidBody == null)
                    continue;
                
                // Get part velocity (could be different for each part due to rotation)
                var partVelocity = part.RigidBody.LinearVelocity;
                
                // Calculate and apply drag force
                var dragForce = CalculateDragForce(part, partVelocity, atmosphericProperties);
                
                if (dragForce.LengthSquared() > 1e-6)
                {
                    // Apply drag force at part center of pressure
                    var centerOfPressure = GetPartCenterOfPressure(part);
                    part.RigidBody.ApplyForce(dragForce, centerOfPressure);
                }
            }
            
            // Performance monitoring
            if (Instance != null)
            {
                var duration = Time.GetTicksMsec() - startTime;
                if (duration > DRAG_CALCULATION_BUDGET)
                {
                    GD.PrintErr($"AerodynamicDrag: Calculation exceeded budget - {duration:F2}ms (target: {DRAG_CALCULATION_BUDGET:F2}ms)");
                }
            }
        }
        
        /// <summary>
        /// Get drag coefficient for a part based on shape and flow conditions
        /// </summary>
        /// <param name="part">Part to get drag coefficient for</param>
        /// <param name="atmosphericProperties">Atmospheric conditions</param>
        /// <param name="velocity">Part velocity</param>
        /// <returns>Drag coefficient (dimensionless)</returns>
        private static double GetPartDragCoefficient(Part part, AtmosphericProperties atmosphericProperties, Vector3 velocity)
        {
            double baseDragCoefficient = part.BaseDragCoefficient;
            
            // Apply Mach number effects
            var machNumber = atmosphericProperties.GetMachNumber(velocity);
            var machMultiplier = GetMachDragMultiplier(machNumber);
            
            return baseDragCoefficient * machMultiplier;
        }
        
        /// <summary>
        /// Calculate effective cross-sectional area based on velocity direction
        /// </summary>
        /// <param name="part">Part to calculate area for</param>
        /// <param name="velocity">Velocity vector</param>
        /// <returns>Effective cross-sectional area in m²</returns>
        private static double GetPartCrossSectionalArea(Part part, Vector3 velocity)
        {
            if (velocity.LengthSquared() < 1e-6)
                return part.CrossSectionalArea; // Default area if no velocity
            
            // Calculate angle between part forward direction and velocity
            var partForward = part.Transform.Basis.Z; // Assuming Z+ is forward
            var velocityDirection = velocity.Normalized();
            var angle = Math.Acos(Math.Clamp(partForward.Dot(velocityDirection), -1.0f, 1.0f));
            
            // Effective area varies with angle of attack
            // Minimum area when aligned (angle = 0), maximum when perpendicular (angle = π/2)
            var minArea = part.CrossSectionalArea * 0.1; // 10% when perfectly streamlined
            var maxArea = part.CrossSectionalArea;
            
            // Smooth interpolation based on angle
            var areaFactor = minArea + (maxArea - minArea) * Math.Sin(angle);
            
            return areaFactor;
        }
        
        /// <summary>
        /// Get center of pressure offset from part center for torque calculations
        /// </summary>
        /// <param name="part">Part to get center of pressure for</param>
        /// <returns>Center of pressure offset vector</returns>
        private static Vector3 GetPartCenterOfPressure(Part part)
        {
            // For now, use part center. In the future, this could be made more sophisticated
            // with per-part center of pressure definitions for aerodynamic stability
            return Vector3.Zero; // Applied at part center
        }
        
        /// <summary>
        /// Calculate drag multiplier based on Mach number for compressibility effects
        /// </summary>
        /// <param name="machNumber">Mach number (velocity / sound speed)</param>
        /// <returns>Drag coefficient multiplier</returns>
        private static double GetMachDragMultiplier(double machNumber)
        {
            if (machNumber < DragCoefficients.TransonicStartMach)
            {
                // Subsonic - constant drag coefficient
                return 1.0;
            }
            else if (machNumber < DragCoefficients.TransonicEndMach)
            {
                // Transonic - increased drag due to shock formation
                var transitionFactor = (machNumber - DragCoefficients.TransonicStartMach) / 
                                     (DragCoefficients.TransonicEndMach - DragCoefficients.TransonicStartMach);
                
                return 1.0 + (DragCoefficients.TransonicMultiplier - 1.0) * Math.Sin(transitionFactor * Math.PI);
            }
            else
            {
                // Supersonic - elevated but stable drag coefficient
                return DragCoefficients.SupersonicMultiplier;
            }
        }
        
        /// <summary>
        /// Calculate terminal velocity where drag = weight
        /// Used for validation and testing realistic atmospheric flight
        /// </summary>
        /// <param name="vessel">Vessel to calculate terminal velocity for</param>
        /// <param name="atmosphericProperties">Atmospheric conditions</param>
        /// <returns>Terminal velocity in m/s</returns>
        public static double CalculateTerminalVelocity(PhysicsVessel vessel, AtmosphericProperties atmosphericProperties)
        {
            if (vessel?.Parts == null || atmosphericProperties.Density <= 0.0)
                return double.PositiveInfinity; // No terminal velocity in vacuum
            
            // Sum vessel mass and drag properties
            double totalMass = 0.0;
            double totalDragArea = 0.0; // Cd * A for all parts
            
            foreach (var part in vessel.Parts)
            {
                if (part != null)
                {
                    totalMass += part.GetTotalMass();
                    totalDragArea += part.BaseDragCoefficient * part.CrossSectionalArea;
                }
            }
            
            if (totalDragArea <= 0.0)
                return double.PositiveInfinity;
            
            // Terminal velocity: v = sqrt(2 * mg / (ρ * Cd * A))
            var weight = totalMass * UNIVERSE_CONSTANTS.KERBIN_STANDARD_GRAVITY;
            var terminalVelocity = Math.Sqrt(2.0 * weight / (atmosphericProperties.Density * totalDragArea));
            
            return terminalVelocity;
        }
        
        /// <summary>
        /// Get drag system performance metrics for monitoring
        /// </summary>
        /// <returns>Performance statistics</returns>
        public DragPerformanceMetrics GetPerformanceMetrics()
        {
            return new DragPerformanceMetrics
            {
                LastCalculationTime = _lastCalculationTime,
                CalculationsThisFrame = _dragCalculationsThisFrame,
                CalculationBudget = DRAG_CALCULATION_BUDGET,
                IsWithinBudget = _lastCalculationTime <= DRAG_CALCULATION_BUDGET,
                AverageTimePerCalculation = _dragCalculationsThisFrame > 0 ? _lastCalculationTime / _dragCalculationsThisFrame : 0.0
            };
        }
        
        /// <summary>
        /// Reset per-frame performance counters
        /// </summary>
        public override void _Process(double delta)
        {
            _dragCalculationsThisFrame = 0;
        }
        
        /// <summary>
        /// Cleanup on exit
        /// </summary>
        public override void _ExitTree()
        {
            if (_instance == this)
            {
                _instance = null;
            }
            
            base._ExitTree();
        }
    }
    
    /// <summary>
    /// Performance metrics for aerodynamic drag calculations
    /// </summary>
    public struct DragPerformanceMetrics
    {
        public double LastCalculationTime;
        public int CalculationsThisFrame;
        public double CalculationBudget;
        public bool IsWithinBudget;
        public double AverageTimePerCalculation;
    }
}