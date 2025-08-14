using Godot;
using System;

namespace Lithobrake.Core
{
    /// <summary>
    /// Command pod part providing crew capacity, control authority, and electrical systems.
    /// Mass: 100kg (typical), provides vessel control and crew accommodation.
    /// </summary>
    public partial class CommandPod : Part
    {
        // Command pod specific properties
        public int CrewCapacity { get; set; } = 1;
        public double ElectricCharge { get; set; } = 50.0;
        public double ElectricChargeMax { get; set; } = 50.0;
        public bool HasSAS { get; set; } = true;
        public double SASAuthorityLevel { get; set; } = 5.0; // Nm of control torque
        
        // Control state
        public bool SASEnabled { get; set; } = false;
        public bool IsOccupied { get; set; } = false;
        public int CurrentCrew { get; set; } = 0;
        
        // Performance constants
        private const double ElectricalConsumptionPerSecond = 1.0; // EC/s when active
        private const double SASConsumptionPerSecond = 0.2; // Additional EC/s for SAS
        
        public override void _Ready()
        {
            // Set default properties for command pod
            Type = PartType.Command;
            if (string.IsNullOrEmpty(PartId))
                PartId = "command-pod";
            if (string.IsNullOrEmpty(PartName))
                PartName = "Mk1 Command Pod";
            if (string.IsNullOrEmpty(Description))
                Description = "A basic command pod capable of housing one crew member";
            
            // Default mass properties
            if (DryMass <= 0)
                DryMass = 100.0; // 100kg dry mass
            FuelMass = 0; // Command pods don't contain fuel
            
            base._Ready();
        }
        
        /// <summary>
        /// Initialize command pod specific components
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();
            
            // Initialize electrical systems
            InitializeElectricalSystems();
            
            // Initialize SAS system
            if (HasSAS)
            {
                InitializeSASSystem();
            }
            
            GD.Print($"CommandPod initialized: {CrewCapacity} crew, {ElectricChargeMax} EC, SAS: {HasSAS}");
        }
        
        /// <summary>
        /// Initialize electrical systems
        /// </summary>
        private void InitializeElectricalSystems()
        {
            // Ensure electrical charge is within limits
            ElectricCharge = Math.Clamp(ElectricCharge, 0, ElectricChargeMax);
        }
        
        /// <summary>
        /// Initialize SAS (Stability Augmentation System)
        /// </summary>
        private void InitializeSASSystem()
        {
            // SAS provides rotational stability and attitude control
            SASAuthorityLevel = Math.Max(1.0, SASAuthorityLevel);
        }
        
        /// <summary>
        /// Process command pod systems each frame
        /// </summary>
        public override void _Process(double delta)
        {
            base._Process(delta);
            
            // Process electrical systems
            ProcessElectricalSystems(delta);
            
            // Process SAS if enabled
            if (HasSAS && SASEnabled)
            {
                ProcessSAS(delta);
            }
        }
        
        /// <summary>
        /// Process electrical systems
        /// </summary>
        private void ProcessElectricalSystems(double delta)
        {
            // Consume electrical charge when crew is present or systems are active
            if (CurrentCrew > 0 || SASEnabled)
            {
                var consumption = ElectricalConsumptionPerSecond * delta;
                if (SASEnabled && HasSAS)
                {
                    consumption += SASConsumptionPerSecond * delta;
                }
                
                ElectricCharge = Math.Max(0, ElectricCharge - consumption);
                
                // Disable SAS if out of power
                if (SASEnabled && ElectricCharge <= 0)
                {
                    SASEnabled = false;
                    GD.Print("CommandPod: SAS disabled due to insufficient electrical charge");
                }
            }
        }
        
        /// <summary>
        /// Process SAS (Stability Augmentation System)
        /// </summary>
        private void ProcessSAS(double delta)
        {
            if (!HasSAS || ElectricCharge <= 0)
            {
                SASEnabled = false;
                return;
            }
            
            // SAS provides attitude stability
            // In a full implementation, this would apply corrective torques
            // For now, just track that it's active
        }
        
        /// <summary>
        /// Board crew member
        /// </summary>
        public bool BoardCrew()
        {
            if (CurrentCrew >= CrewCapacity)
            {
                GD.PrintErr($"CommandPod: Cannot board crew - at capacity ({CurrentCrew}/{CrewCapacity})");
                return false;
            }
            
            CurrentCrew++;
            IsOccupied = CurrentCrew > 0;
            
            GD.Print($"CommandPod: Crew boarded ({CurrentCrew}/{CrewCapacity})");
            return true;
        }
        
        /// <summary>
        /// Remove crew member
        /// </summary>
        public bool RemoveCrew()
        {
            if (CurrentCrew <= 0)
            {
                GD.PrintErr("CommandPod: No crew to remove");
                return false;
            }
            
            CurrentCrew--;
            IsOccupied = CurrentCrew > 0;
            
            GD.Print($"CommandPod: Crew removed ({CurrentCrew}/{CrewCapacity})");
            return true;
        }
        
        /// <summary>
        /// Toggle SAS system
        /// </summary>
        public bool ToggleSAS()
        {
            if (!HasSAS)
            {
                GD.PrintErr("CommandPod: No SAS system available");
                return false;
            }
            
            if (ElectricCharge <= 0)
            {
                GD.PrintErr("CommandPod: Insufficient electrical charge for SAS");
                return false;
            }
            
            SASEnabled = !SASEnabled;
            GD.Print($"CommandPod: SAS {(SASEnabled ? "enabled" : "disabled")}");
            return SASEnabled;
        }
        
        /// <summary>
        /// Add electrical charge (from generators, solar panels, etc.)
        /// </summary>
        public double AddElectricalCharge(double amount)
        {
            var oldCharge = ElectricCharge;
            ElectricCharge = Math.Min(ElectricChargeMax, ElectricCharge + amount);
            return ElectricCharge - oldCharge; // Return actual amount added
        }
        
        /// <summary>
        /// Consume electrical charge
        /// </summary>
        public double ConsumeElectricalCharge(double amount)
        {
            var oldCharge = ElectricCharge;
            ElectricCharge = Math.Max(0, ElectricCharge - amount);
            return oldCharge - ElectricCharge; // Return actual amount consumed
        }
        
        /// <summary>
        /// Get electrical charge percentage
        /// </summary>
        public double GetElectricalChargePercentage()
        {
            return ElectricChargeMax > 0 ? ElectricCharge / ElectricChargeMax : 0;
        }
        
        /// <summary>
        /// Check if command pod can provide vessel control
        /// </summary>
        public bool CanProvideControl()
        {
            // Can provide control if occupied by crew or has electrical charge for probe control
            return (IsOccupied && CurrentCrew > 0) || ElectricCharge > 0;
        }
        
        /// <summary>
        /// Get control authority level (0-1)
        /// </summary>
        public double GetControlAuthority()
        {
            if (!CanProvideControl())
                return 0.0;
            
            // Full authority if crewed, reduced if probe-controlled
            if (IsOccupied && CurrentCrew > 0)
                return 1.0;
            else if (ElectricCharge > 0)
                return Math.Min(1.0, ElectricCharge / 10.0); // Reduced control for probes
            else
                return 0.0;
        }
        
        /// <summary>
        /// Create primitive mesh fallback for command pod
        /// </summary>
        protected override Mesh CreatePrimitiveMesh()
        {
            // Create sphere shape for command pod
            var sphereMesh = new SphereMesh
            {
                RadialSegments = 12,
                Rings = 8,
                Radius = 1.0f,
                Height = 2.0f
            };
            
            return sphereMesh;
        }
        
        /// <summary>
        /// Get part dimensions for collision shape
        /// </summary>
        protected override Vector3 GetPartDimensions()
        {
            // Command pod dimensions (roughly cylindrical)
            return new Vector3(2.0f, 2.0f, 2.0f);
        }
        
        /// <summary>
        /// Initialize attachment nodes for command pod
        /// </summary>
        protected override void InitializeAttachmentNodes()
        {
            var dimensions = GetPartDimensions();
            
            // Command pods typically only have bottom attachment
            AttachBottom = new AttachmentNode(
                new Vector3(0, -dimensions.Y / 2, 0),
                Vector3.Down,
                AttachmentNodeType.Stack,
                AttachmentNodeSize.Size1,
                5000.0, // Can support 5 tons
                "bottom"
            );
            
            // No top attachment - command pods are typically at the top of rockets
            AttachTop = null;
        }
        
        /// <summary>
        /// Get maximum attachment mass based on structural strength
        /// </summary>
        protected override double GetMaxAttachmentMass()
        {
            // Command pods can support moderate loads below them
            return 5000.0; // 5 tons
        }
        
        /// <summary>
        /// Update physics properties including electrical systems
        /// </summary>
        public override void UpdatePhysicsProperties()
        {
            base.UpdatePhysicsProperties();
            
            // Command pods don't change mass (no fuel consumption)
            // but electrical charge affects functionality
        }
        
        /// <summary>
        /// Get command pod status for UI display
        /// </summary>
        public string GetStatusSummary()
        {
            var crewStatus = $"Crew: {CurrentCrew}/{CrewCapacity}";
            var electricalStatus = $"EC: {ElectricCharge:F1}/{ElectricChargeMax:F1}";
            var sasStatus = HasSAS ? $"SAS: {(SASEnabled ? "ON" : "OFF")}" : "No SAS";
            
            return $"{crewStatus}, {electricalStatus}, {sasStatus}";
        }
        
        /// <summary>
        /// Command pod specific toString
        /// </summary>
        public override string ToString()
        {
            return $"{base.ToString()}, {GetStatusSummary()}";
        }
    }
}