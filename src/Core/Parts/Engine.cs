using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lithobrake.Core
{
    /// <summary>
    /// Engine part providing thrust generation and fuel consumption.
    /// Mass: 250kg, 200kN thrust, 280s specific impulse with throttle control and gimbal.
    /// </summary>
    public partial class Engine : Part
    {
        // Engine specific properties
        public double MaxThrust { get; set; } = 200000.0; // 200kN in Newtons
        public double SpecificImpulse { get; set; } = 280.0; // Seconds
        public double FuelConsumption { get; set; } = 72.5; // kg/s at full throttle
        public bool CanGimbal { get; set; } = true;
        public double GimbalRange { get; set; } = 5.0; // Degrees
        
        // Engine state
        public bool IsActive { get; set; } = false;
        public double CurrentThrottle { get; set; } = 0.0; // 0-1
        public double CurrentThrust { get; private set; } = 0.0; // Newtons
        public Vector3 ThrustVector { get; private set; } = Vector3.Zero;
        public Vector3 GimbalAngle { get; set; } = Vector3.Zero; // Pitch, yaw in degrees
        
        // Performance properties
        public double ThrottleResponse { get; set; } = 2.0; // Throttle change rate per second
        public double SpoolUpTime { get; set; } = 1.0; // Time to reach full thrust
        public double SpoolDownTime { get; set; } = 0.5; // Time to shut down
        
        // Fuel system
        private readonly List<FuelTank> _connectedFuelTanks = new();
        public double FuelFlowRate { get; private set; } = 0.0; // Current kg/s
        public bool HasFuel { get; private set; } = true;
        
        // Engine effects (placeholder for particle systems)
        public bool ShowExhaust { get; set; } = true;
        public double ExhaustScale { get; private set; } = 1.0;
        
        // Constants
        private const double StandardGravity = 9.81; // m/s²
        private const double MinThrottleForIgnition = 0.01; // 1% minimum throttle
        private const double EngineWarmupTime = 0.2; // Time for engine to warm up
        
        // Internal state
        private double _targetThrottle = 0.0;
        private double _engineWarmup = 0.0; // Engine warmup progress
        private bool _isSpoolingUp = false;
        private bool _isSpoolingDown = false;
        
        public override void _Ready()
        {
            // Set default properties for engine
            Type = PartType.Engine;
            if (string.IsNullOrEmpty(PartId))
                PartId = "engine";
            if (string.IsNullOrEmpty(PartName))
                PartName = "LV-T30 \"Reliant\" Engine";
            if (string.IsNullOrEmpty(Description))
                Description = "A basic liquid fuel rocket engine";
            
            // Default mass properties
            if (DryMass <= 0)
                DryMass = 250.0; // 250kg dry mass
            FuelMass = 0; // Engines don't store fuel
            
            // Calculate fuel consumption from thrust and Isp if not set
            if (FuelConsumption <= 0)
                FuelConsumption = MaxThrust / (SpecificImpulse * StandardGravity);
            
            base._Ready();
        }
        
        /// <summary>
        /// Initialize engine specific components
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();
            
            // Initialize engine systems
            InitializeEngineThrust();
            InitializeGimbalSystem();
            ConnectToFuelSystems();
            
            GD.Print($"Engine initialized: {MaxThrust/1000:F0}kN thrust, {SpecificImpulse:F0}s Isp, Gimbal: {CanGimbal}");
        }
        
        /// <summary>
        /// Initialize thrust system
        /// </summary>
        private void InitializeEngineThrust()
        {
            // Validate thrust parameters
            MaxThrust = Math.Max(1000.0, MaxThrust); // Minimum 1kN
            SpecificImpulse = Math.Max(100.0, SpecificImpulse); // Minimum 100s Isp
            
            // Recalculate fuel consumption if needed
            FuelConsumption = MaxThrust / (SpecificImpulse * StandardGravity);
            
            CurrentThrust = 0.0;
            ThrustVector = Vector3.Zero;
        }
        
        /// <summary>
        /// Initialize gimbal system
        /// </summary>
        private void InitializeGimbalSystem()
        {
            if (CanGimbal)
            {
                GimbalRange = Math.Clamp(GimbalRange, 0.5, 15.0); // Reasonable gimbal range
            }
            else
            {
                GimbalRange = 0.0;
            }
            
            GimbalAngle = Vector3.Zero;
        }
        
        /// <summary>
        /// Connect to fuel systems in the vessel
        /// </summary>
        private void ConnectToFuelSystems()
        {
            _connectedFuelTanks.Clear();
            
            // In a full implementation, this would traverse the vessel structure
            // to find fuel tanks that can supply this engine
            // For now, it's a placeholder
        }
        
        /// <summary>
        /// Process engine systems each frame
        /// </summary>
        public override void _Process(double delta)
        {
            base._Process(delta);
            
            // Process throttle changes
            ProcessThrottleControl(delta);
            
            // Process thrust generation
            ProcessThrustGeneration(delta);
            
            // Process fuel consumption
            if (IsActive && CurrentThrust > 0)
            {
                ProcessFuelConsumption(delta);
            }
            
            // Update visual effects
            UpdateEngineEffects();
        }
        
        /// <summary>
        /// Process throttle control and engine spooling
        /// </summary>
        private void ProcessThrottleControl(double delta)
        {
            // Handle throttle changes
            if (Math.Abs(CurrentThrottle - _targetThrottle) > 0.001)
            {
                var throttleChange = ThrottleResponse * delta;
                
                if (CurrentThrottle < _targetThrottle)
                {
                    CurrentThrottle = Math.Min(_targetThrottle, CurrentThrottle + throttleChange);
                    _isSpoolingUp = true;
                    _isSpoolingDown = false;
                }
                else
                {
                    CurrentThrottle = Math.Max(_targetThrottle, CurrentThrottle - throttleChange);
                    _isSpoolingUp = false;
                    _isSpoolingDown = true;
                }
            }
            else
            {
                _isSpoolingUp = false;
                _isSpoolingDown = false;
            }
            
            // Handle engine warmup
            if (IsActive && CurrentThrottle > MinThrottleForIgnition)
            {
                _engineWarmup = Math.Min(1.0, _engineWarmup + delta / EngineWarmupTime);
            }
            else
            {
                _engineWarmup = Math.Max(0.0, _engineWarmup - delta / (EngineWarmupTime * 2));
            }
        }
        
        /// <summary>
        /// Process thrust generation
        /// </summary>
        private void ProcessThrustGeneration(double delta)
        {
            if (!IsActive || !HasFuel || CurrentThrottle < MinThrottleForIgnition)
            {
                CurrentThrust = 0.0;
                ThrustVector = Vector3.Zero;
                return;
            }
            
            // Calculate thrust based on throttle, warmup, and atmospheric conditions
            var thrustMultiplier = CurrentThrottle * _engineWarmup;
            
            // Apply atmospheric efficiency (simple model)
            var atmosphericPressure = GetAtmosphericPressure();
            var thrustEfficiency = GetThrustEfficiency(atmosphericPressure);
            
            CurrentThrust = MaxThrust * thrustMultiplier * thrustEfficiency;
            
            // Calculate thrust vector with gimbal
            var baseDirection = -Transform.Basis.Y; // Engine points down
            var gimbalRotation = GetGimbalRotation();
            ThrustVector = gimbalRotation * baseDirection * (float)CurrentThrust;
        }
        
        /// <summary>
        /// Process fuel consumption
        /// </summary>
        private void ProcessFuelConsumption(double delta)
        {
            var fuelNeeded = FuelConsumption * (CurrentThrust / MaxThrust) * delta;
            var fuelConsumed = ConsumeFuelFromTanks(fuelNeeded);
            
            FuelFlowRate = fuelConsumed / delta;
            
            // Check if we have fuel
            HasFuel = fuelConsumed > fuelNeeded * 0.9; // 90% efficiency threshold
            
            if (!HasFuel && IsActive)
            {
                // Engine flameout
                SetThrottle(0.0);
                IsActive = false;
                GD.Print($"Engine: Fuel exhausted - engine shutdown");
            }
        }
        
        /// <summary>
        /// Consume fuel from connected fuel tanks
        /// </summary>
        private double ConsumeFuelFromTanks(double fuelNeeded)
        {
            var totalConsumed = 0.0;
            
            // Try to get fuel from connected tanks
            foreach (var tank in _connectedFuelTanks.ToList())
            {
                if (tank == null || tank.IsQueuedForDeletion())
                {
                    _connectedFuelTanks.Remove(tank);
                    continue;
                }
                
                if (fuelNeeded <= 0)
                    break;
                
                var consumed = tank.DrainFuel(fuelNeeded, FuelType.LiquidFuel);
                totalConsumed += consumed;
                fuelNeeded -= consumed;
            }
            
            return totalConsumed;
        }
        
        /// <summary>
        /// Get atmospheric pressure at current altitude
        /// </summary>
        private double GetAtmosphericPressure()
        {
            // Simple atmospheric model - would integrate with AtmosphericConditions in full implementation
            var altitude = GlobalPosition.Y;
            var seaLevelPressure = 101325.0; // Pa
            var scaleHeight = 7500.0; // m
            
            return seaLevelPressure * Math.Exp(-altitude / scaleHeight);
        }
        
        /// <summary>
        /// Get thrust efficiency based on atmospheric pressure
        /// </summary>
        private double GetThrustEfficiency(double atmosphericPressure)
        {
            // Simplified model - real rockets have complex thrust curves
            var seaLevelPressure = 101325.0; // Pa
            var pressureRatio = atmosphericPressure / seaLevelPressure;
            
            // Linear interpolation between sea level (0.9) and vacuum (1.0) efficiency
            return 0.9 + (0.1 * (1.0 - pressureRatio));
        }
        
        /// <summary>
        /// Get gimbal rotation based on gimbal angle
        /// </summary>
        private Basis GetGimbalRotation()
        {
            if (!CanGimbal || GimbalAngle.Length() < 0.001)
                return Basis.Identity;
            
            var pitchRad = Mathf.DegToRad(GimbalAngle.X);
            var yawRad = Mathf.DegToRad(GimbalAngle.Z);
            
            var pitchBasis = new Basis(Vector3.Right, pitchRad);
            var yawBasis = new Basis(Vector3.Up, yawRad);
            
            return yawBasis * pitchBasis;
        }
        
        /// <summary>
        /// Set engine throttle (0-1)
        /// </summary>
        public void SetThrottle(double throttle)
        {
            _targetThrottle = Math.Clamp(throttle, 0.0, 1.0);
            
            // Auto-activate engine if throttle is set above minimum
            if (_targetThrottle > MinThrottleForIgnition && HasFuel)
            {
                IsActive = true;
            }
            else if (_targetThrottle < MinThrottleForIgnition)
            {
                IsActive = false;
            }
        }
        
        /// <summary>
        /// Set gimbal angle for thrust vectoring
        /// </summary>
        public void SetGimbal(double pitch, double yaw)
        {
            if (!CanGimbal)
                return;
            
            var maxAngle = GimbalRange;
            GimbalAngle = new Vector3(
                (float)Math.Clamp(pitch, -maxAngle, maxAngle),
                0,
                (float)Math.Clamp(yaw, -maxAngle, maxAngle)
            );
        }
        
        /// <summary>
        /// Activate/deactivate engine
        /// </summary>
        public void SetActive(bool active)
        {
            if (active && !HasFuel)
            {
                GD.PrintErr("Engine: Cannot activate - no fuel available");
                return;
            }
            
            IsActive = active;
            
            if (!active)
            {
                SetThrottle(0.0);
            }
            
            GD.Print($"Engine: {(IsActive ? "Activated" : "Deactivated")}");
        }
        
        /// <summary>
        /// Connect to a fuel tank
        /// </summary>
        public void ConnectToFuelTank(FuelTank fuelTank)
        {
            if (fuelTank != null && !_connectedFuelTanks.Contains(fuelTank))
            {
                _connectedFuelTanks.Add(fuelTank);
                GD.Print($"Engine: Connected to fuel tank {fuelTank.PartName}");
            }
        }
        
        /// <summary>
        /// Disconnect from a fuel tank
        /// </summary>
        public void DisconnectFromFuelTank(FuelTank fuelTank)
        {
            if (_connectedFuelTanks.Contains(fuelTank))
            {
                _connectedFuelTanks.Remove(fuelTank);
                GD.Print($"Engine: Disconnected from fuel tank {fuelTank.PartName}");
            }
        }
        
        /// <summary>
        /// Update visual effects
        /// </summary>
        private void UpdateEngineEffects()
        {
            if (ShowExhaust && IsActive && CurrentThrust > 0)
            {
                ExhaustScale = CurrentThrust / MaxThrust;
                // In a full implementation, this would update particle systems
            }
            else
            {
                ExhaustScale = 0.0;
            }
        }
        
        /// <summary>
        /// Get engine performance statistics
        /// </summary>
        public EngineStats GetEngineStats()
        {
            return new EngineStats
            {
                IsActive = IsActive,
                CurrentThrottle = CurrentThrottle,
                CurrentThrust = CurrentThrust,
                MaxThrust = MaxThrust,
                SpecificImpulse = SpecificImpulse,
                FuelFlowRate = FuelFlowRate,
                ThrustToWeightRatio = CurrentThrust / (GetTotalMass() * StandardGravity),
                HasFuel = HasFuel,
                GimbalAngle = GimbalAngle,
                EngineWarmup = _engineWarmup
            };
        }
        
        /// <summary>
        /// Create primitive mesh fallback for engine
        /// </summary>
        protected override Mesh CreatePrimitiveMesh()
        {
            // Create a cone/cylinder hybrid for engine shape
            var cylinderMesh = new CylinderMesh
            {
                TopRadius = 0.6f,
                BottomRadius = 0.8f, // Slightly flared at bottom
                Height = 1.5f,
                RadialSegments = 16,
                Rings = 1
            };
            
            return cylinderMesh;
        }
        
        /// <summary>
        /// Get part dimensions for collision shape
        /// </summary>
        protected override Vector3 GetPartDimensions()
        {
            // Engine dimensions
            return new Vector3(1.6f, 1.5f, 1.6f);
        }
        
        /// <summary>
        /// Initialize attachment nodes for engine
        /// </summary>
        protected override void InitializeAttachmentNodes()
        {
            var dimensions = GetPartDimensions();
            
            // Engines only have top attachment (mounted to bottom of tanks)
            AttachTop = new AttachmentNode(
                new Vector3(0, dimensions.Y / 2, 0),
                Vector3.Up,
                AttachmentNodeType.Stack,
                AttachmentNodeSize.Size1,
                2000.0, // Can support 2 tons above
                "top"
            );
            
            // No bottom attachment - engines are typically at the bottom
            AttachBottom = null;
        }
        
        /// <summary>
        /// Get maximum attachment mass
        /// </summary>
        protected override double GetMaxAttachmentMass()
        {
            // Engines can support moderate loads
            return 2000.0; // 2 tons
        }
        
        /// <summary>
        /// Get engine status for UI display
        /// </summary>
        public string GetStatusSummary()
        {
            var activeStatus = IsActive ? "ACTIVE" : "INACTIVE";
            var throttleStatus = $"Throttle: {CurrentThrottle:P0}";
            var thrustStatus = $"Thrust: {CurrentThrust/1000:F0}kN";
            var fuelStatus = HasFuel ? "Fuel OK" : "NO FUEL";
            
            return $"{activeStatus}, {throttleStatus}, {thrustStatus}, {fuelStatus}";
        }
        
        /// <summary>
        /// Cleanup engine connections
        /// </summary>
        public override void _ExitTree()
        {
            // Disconnect from all fuel tanks
            _connectedFuelTanks.Clear();
            
            base._ExitTree();
        }
        
        /// <summary>
        /// Engine specific toString
        /// </summary>
        public override string ToString()
        {
            return $"{base.ToString()}, {GetStatusSummary()}";
        }
    }
    
    /// <summary>
    /// Engine performance statistics
    /// </summary>
    public struct EngineStats
    {
        public bool IsActive;
        public double CurrentThrottle;
        public double CurrentThrust;
        public double MaxThrust;
        public double SpecificImpulse;
        public double FuelFlowRate;
        public double ThrustToWeightRatio;
        public bool HasFuel;
        public Vector3 GimbalAngle;
        public double EngineWarmup;
    }
}