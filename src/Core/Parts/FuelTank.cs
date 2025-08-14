using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lithobrake.Core
{
    /// <summary>
    /// Fuel tank part for storing liquid fuel and oxidizer.
    /// Mass: 500kg dry + up to 4500kg fuel, provides fuel storage and transfer capabilities.
    /// </summary>
    public partial class FuelTank : Part
    {
        // Fuel tank specific properties
        public double LiquidFuel { get; set; } = 4500.0;
        public double LiquidFuelMax { get; set; } = 4500.0;
        public double Oxidizer { get; set; } = 0.0;
        public double OxidizerMax { get; set; } = 0.0;
        public bool CanCrossfeed { get; set; } = true;
        
        // Transfer properties
        public double TransferRate { get; set; } = 100.0; // Units per second
        public List<FuelTank> ConnectedTanks { get; private set; } = new();
        public bool IsTransferring { get; private set; } = false;
        
        // Tank configuration
        public FuelTankType TankType { get; set; } = FuelTankType.LiquidFuel;
        public double PressurePSI { get; set; } = 14.7; // Atmospheric pressure default
        public double MaxPressurePSI { get; set; } = 300.0; // Maximum safe pressure
        
        // Performance tracking
        private double _lastTransferTime = 0;
        private const double TransferCooldown = 0.1; // 100ms between transfer updates
        
        public override void _Ready()
        {
            // Set default properties for fuel tank
            Type = PartType.FuelTank;
            if (string.IsNullOrEmpty(PartId))
                PartId = "fuel-tank";
            if (string.IsNullOrEmpty(PartName))
                PartName = "FL-T400 Fuel Tank";
            if (string.IsNullOrEmpty(Description))
                Description = "A basic fuel tank for storing liquid fuel";
            
            // Default mass properties
            if (DryMass <= 0)
                DryMass = 500.0; // 500kg dry mass
            if (FuelMass <= 0)
                FuelMass = LiquidFuel + Oxidizer; // Update fuel mass from resources
            
            base._Ready();
        }
        
        /// <summary>
        /// Initialize fuel tank specific components
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();
            
            // Initialize fuel systems
            InitializeFuelSystems();
            
            // Connect to other fuel tanks in the vessel
            ConnectToFuelNetwork();
            
            GD.Print($"FuelTank initialized: {LiquidFuel:F0}L fuel, {GetFuelPercentage():P1} full, Crossfeed: {CanCrossfeed}");
        }
        
        /// <summary>
        /// Initialize fuel systems and validate fuel levels
        /// </summary>
        private void InitializeFuelSystems()
        {
            // Clamp fuel levels to valid ranges
            LiquidFuel = Math.Clamp(LiquidFuel, 0, LiquidFuelMax);
            Oxidizer = Math.Clamp(Oxidizer, 0, OxidizerMax);
            
            // Update fuel mass
            UpdateFuelMass();
            
            // Initialize pressure
            PressurePSI = Math.Clamp(PressurePSI, 0, MaxPressurePSI);
        }
        
        /// <summary>
        /// Connect to other fuel tanks for crossfeed
        /// </summary>
        private void ConnectToFuelNetwork()
        {
            ConnectedTanks.Clear();
            
            if (!CanCrossfeed)
                return;
            
            // In a full implementation, this would traverse the vessel structure
            // to find connected fuel tanks. For now, it's a placeholder.
        }
        
        /// <summary>
        /// Update fuel mass based on current fuel levels
        /// </summary>
        public void UpdateFuelMass()
        {
            // Simple density model: liquid fuel = 0.8 kg/L, oxidizer = 1.1 kg/L
            FuelMass = (LiquidFuel * 0.8) + (Oxidizer * 1.1);
            
            // Update physics properties if physics body exists
            if (RigidBody != null)
            {
                RigidBody.Mass = (float)GetTotalMass();
            }
        }
        
        /// <summary>
        /// Process fuel tank systems each frame
        /// </summary>
        public override void _Process(double delta)
        {
            base._Process(delta);
            
            // Process fuel transfer if active
            if (IsTransferring && Time.GetTicksMsec() / 1000.0 - _lastTransferTime > TransferCooldown)
            {
                ProcessFuelTransfer(delta);
            }
            
            // Update fuel mass if it has changed
            var expectedFuelMass = (LiquidFuel * 0.8) + (Oxidizer * 1.1);
            if (Math.Abs(FuelMass - expectedFuelMass) > 0.1)
            {
                UpdateFuelMass();
            }
        }
        
        /// <summary>
        /// Process fuel transfer between tanks
        /// </summary>
        private void ProcessFuelTransfer(double delta)
        {
            _lastTransferTime = Time.GetTicksMsec() / 1000.0;
            
            // Transfer fuel to connected tanks with lower fuel percentage
            foreach (var connectedTank in ConnectedTanks.ToList())
            {
                if (connectedTank == null || connectedTank.IsQueuedForDeletion())
                {
                    ConnectedTanks.Remove(connectedTank);
                    continue;
                }
                
                TransferFuelTo(connectedTank, delta);
            }
        }
        
        /// <summary>
        /// Transfer fuel to another tank
        /// </summary>
        private void TransferFuelTo(FuelTank targetTank, double delta)
        {
            if (!CanCrossfeed || !targetTank.CanCrossfeed)
                return;
            
            var sourceFuelPercentage = GetFuelPercentage();
            var targetFuelPercentage = targetTank.GetFuelPercentage();
            
            // Only transfer if source has more fuel percentage than target
            if (sourceFuelPercentage <= targetFuelPercentage)
                return;
            
            var transferAmount = TransferRate * delta;
            var actualTransfer = Math.Min(transferAmount, LiquidFuel);
            actualTransfer = Math.Min(actualTransfer, targetTank.LiquidFuelMax - targetTank.LiquidFuel);
            
            if (actualTransfer > 0.1) // Only transfer if meaningful amount
            {
                // Transfer liquid fuel
                LiquidFuel -= actualTransfer;
                targetTank.LiquidFuel += actualTransfer;
                
                // Update masses
                UpdateFuelMass();
                targetTank.UpdateFuelMass();
            }
        }
        
        /// <summary>
        /// Drain fuel from the tank (used by engines)
        /// </summary>
        public double DrainFuel(double requestedAmount, FuelType fuelType = FuelType.LiquidFuel)
        {
            var startTime = Time.GetTicksMsec();
            
            double actualDrained = 0;
            
            switch (fuelType)
            {
                case FuelType.LiquidFuel:
                    actualDrained = Math.Min(requestedAmount, LiquidFuel);
                    LiquidFuel -= actualDrained;
                    break;
                    
                case FuelType.Oxidizer:
                    actualDrained = Math.Min(requestedAmount, Oxidizer);
                    Oxidizer -= actualDrained;
                    break;
                    
                case FuelType.Both:
                    // Drain proportionally
                    var fuelRatio = LiquidFuelMax > 0 ? LiquidFuel / LiquidFuelMax : 0;
                    var oxidizerRatio = OxidizerMax > 0 ? Oxidizer / OxidizerMax : 0;
                    
                    if (fuelRatio > 0 && oxidizerRatio > 0)
                    {
                        var fuelDrain = Math.Min(requestedAmount * 0.9, LiquidFuel); // 9:11 ratio
                        var oxidizerDrain = Math.Min(requestedAmount * 1.1, Oxidizer);
                        
                        LiquidFuel -= fuelDrain;
                        Oxidizer -= oxidizerDrain;
                        actualDrained = fuelDrain + oxidizerDrain;
                    }
                    break;
            }
            
            if (actualDrained > 0)
            {
                UpdateFuelMass();
                
                // Performance monitoring
                var duration = Time.GetTicksMsec() - startTime;
                if (duration > 0.1) // Target: <0.1ms for fuel drain operations
                {
                    GD.PrintErr($"FuelTank.DrainFuel took {duration:F2}ms (target: <0.1ms)");
                }
            }
            
            return actualDrained;
        }
        
        /// <summary>
        /// Add fuel to the tank
        /// </summary>
        public double AddFuel(double amount, FuelType fuelType = FuelType.LiquidFuel)
        {
            double actualAdded = 0;
            
            switch (fuelType)
            {
                case FuelType.LiquidFuel:
                    actualAdded = Math.Min(amount, LiquidFuelMax - LiquidFuel);
                    LiquidFuel += actualAdded;
                    break;
                    
                case FuelType.Oxidizer:
                    actualAdded = Math.Min(amount, OxidizerMax - Oxidizer);
                    Oxidizer += actualAdded;
                    break;
            }
            
            if (actualAdded > 0)
            {
                UpdateFuelMass();
            }
            
            return actualAdded;
        }
        
        /// <summary>
        /// Get total fuel percentage (0-1)
        /// </summary>
        public double GetFuelPercentage()
        {
            var totalMax = LiquidFuelMax + OxidizerMax;
            var totalCurrent = LiquidFuel + Oxidizer;
            
            return totalMax > 0 ? totalCurrent / totalMax : 0;
        }
        
        /// <summary>
        /// Get liquid fuel percentage (0-1)
        /// </summary>
        public double GetLiquidFuelPercentage()
        {
            return LiquidFuelMax > 0 ? LiquidFuel / LiquidFuelMax : 0;
        }
        
        /// <summary>
        /// Get oxidizer percentage (0-1)
        /// </summary>
        public double GetOxidizerPercentage()
        {
            return OxidizerMax > 0 ? Oxidizer / OxidizerMax : 0;
        }
        
        /// <summary>
        /// Check if tank is empty
        /// </summary>
        public bool IsEmpty()
        {
            return LiquidFuel <= 0.1 && Oxidizer <= 0.1;
        }
        
        /// <summary>
        /// Check if tank is full
        /// </summary>
        public bool IsFull()
        {
            return LiquidFuel >= LiquidFuelMax - 0.1 && Oxidizer >= OxidizerMax - 0.1;
        }
        
        /// <summary>
        /// Enable/disable fuel transfer
        /// </summary>
        public void SetTransferring(bool transferring)
        {
            IsTransferring = transferring && CanCrossfeed;
            GD.Print($"FuelTank: Transfer {(IsTransferring ? "enabled" : "disabled")}");
        }
        
        /// <summary>
        /// Connect to another fuel tank for crossfeed
        /// </summary>
        public void ConnectToTank(FuelTank otherTank)
        {
            if (otherTank != null && !ConnectedTanks.Contains(otherTank))
            {
                ConnectedTanks.Add(otherTank);
                
                // Create bidirectional connection
                if (!otherTank.ConnectedTanks.Contains(this))
                {
                    otherTank.ConnectedTanks.Add(this);
                }
                
                GD.Print($"FuelTank: Connected to {otherTank.PartName}");
            }
        }
        
        /// <summary>
        /// Disconnect from another fuel tank
        /// </summary>
        public void DisconnectFromTank(FuelTank otherTank)
        {
            if (ConnectedTanks.Contains(otherTank))
            {
                ConnectedTanks.Remove(otherTank);
                
                // Remove bidirectional connection
                if (otherTank.ConnectedTanks.Contains(this))
                {
                    otherTank.ConnectedTanks.Remove(this);
                }
                
                GD.Print($"FuelTank: Disconnected from {otherTank.PartName}");
            }
        }
        
        /// <summary>
        /// Create primitive mesh fallback for fuel tank
        /// </summary>
        protected override Mesh CreatePrimitiveMesh()
        {
            // Create cylinder shape for fuel tank
            var cylinderMesh = new CylinderMesh
            {
                TopRadius = 0.75f,
                BottomRadius = 0.75f,
                Height = 3.0f,
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
            // Fuel tank dimensions (cylindrical)
            return new Vector3(1.5f, 3.0f, 1.5f);
        }
        
        /// <summary>
        /// Initialize attachment nodes for fuel tank
        /// </summary>
        protected override void InitializeAttachmentNodes()
        {
            var dimensions = GetPartDimensions();
            
            // Fuel tanks have both top and bottom stack attachments
            AttachTop = new AttachmentNode(
                new Vector3(0, dimensions.Y / 2, 0),
                Vector3.Up,
                AttachmentNodeType.Stack,
                AttachmentNodeSize.Size1,
                10000.0, // Can support 10 tons above
                "top"
            );
            
            AttachBottom = new AttachmentNode(
                new Vector3(0, -dimensions.Y / 2, 0),
                Vector3.Down,
                AttachmentNodeType.Stack,
                AttachmentNodeSize.Size1,
                5000.0, // Can support 5 tons below
                "bottom"
            );
            
            // Add some radial attachment points for surface-mounted parts
            var radius = dimensions.X / 2;
            for (int i = 0; i < 4; i++)
            {
                var angle = i * Mathf.Pi / 2; // 90 degrees apart
                var position = new Vector3(
                    radius * Mathf.Cos(angle),
                    0,
                    radius * Mathf.Sin(angle)
                );
                
                var radialNode = new AttachmentNode(
                    position,
                    position.Normalized(), // Outward facing
                    AttachmentNodeType.Radial,
                    AttachmentNodeSize.Size1,
                    1000.0, // Support 1 ton radially
                    $"radial_{i}"
                )
                {
                    AllowSurfaceAttach = true
                };
                
                RadialAttachPoints.Add(radialNode);
            }
        }
        
        /// <summary>
        /// Get maximum attachment mass based on fuel tank structure
        /// </summary>
        protected override double GetMaxAttachmentMass()
        {
            // Fuel tanks are structural and can support heavy loads
            return 10000.0; // 10 tons
        }
        
        /// <summary>
        /// Get fuel tank status for UI display
        /// </summary>
        public string GetStatusSummary()
        {
            var fuelStatus = $"Fuel: {LiquidFuel:F0}L ({GetLiquidFuelPercentage():P1})";
            var oxidizerStatus = OxidizerMax > 0 ? $", Ox: {Oxidizer:F0}L ({GetOxidizerPercentage():P1})" : "";
            var transferStatus = CanCrossfeed ? $", Xfeed: {(IsTransferring ? "ON" : "OFF")}" : ", No Xfeed";
            
            return $"{fuelStatus}{oxidizerStatus}{transferStatus}";
        }
        
        /// <summary>
        /// Cleanup fuel tank connections
        /// </summary>
        public override void _ExitTree()
        {
            // Disconnect from all connected tanks
            foreach (var tank in ConnectedTanks.ToList())
            {
                DisconnectFromTank(tank);
            }
            
            base._ExitTree();
        }
        
        /// <summary>
        /// Fuel tank specific toString
        /// </summary>
        public override string ToString()
        {
            return $"{base.ToString()}, {GetStatusSummary()}";
        }
    }
    
    /// <summary>
    /// Types of fuel that can be stored and transferred
    /// </summary>
    public enum FuelType
    {
        LiquidFuel,
        Oxidizer,
        Both,
        MonoPropellant,
        XenonGas
    }
    
    /// <summary>
    /// Types of fuel tanks
    /// </summary>
    public enum FuelTankType
    {
        LiquidFuel,      // Only liquid fuel
        LiquidOxidizer,  // Liquid fuel + oxidizer
        MonoPropellant,  // RCS fuel
        XenonGas        // Ion engine fuel
    }
}