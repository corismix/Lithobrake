using Godot;
using System;
using System.Collections.Generic;

namespace Lithobrake.Core
{
    /// <summary>
    /// Test rocket assembly system for validating the 3-part stack (CommandPod + FuelTank + Engine).
    /// Creates hardcoded assemblies for testing mass calculations, attachment systems, and part integration.
    /// </summary>
    public partial class TestRocketAssembly : Node3D
    {
        // Test rocket configuration
        private CommandPod? _commandPod;
        private FuelTank? _fuelTank;
        private Engine? _engine;
        
        // Assembly properties
        public double TotalMass { get; private set; } = 0.0;
        public Vector3 CenterOfMass { get; private set; } = Vector3.Zero;
        public List<Part> AllParts { get; private set; } = new();
        
        // Expected values for validation
        private const double ExpectedTotalMass = 5350.0; // 100 + 500 + 3600 + 250 = 5350kg
        private const double MassToleranceKg = 10.0; // Allow 10kg tolerance
        
        // Performance tracking
        private static PerformanceMonitor? _performanceMonitor;
        
        // Assembly state
        public bool IsAssembled { get; private set; } = false;
        public bool IsValidated { get; private set; } = false;
        public string ValidationResults { get; private set; } = string.Empty;
        
        public override void _Ready()
        {
            _performanceMonitor = PerformanceMonitor.Instance;
            GD.Print("TestRocketAssembly: Starting 3-part rocket assembly test");
            
            // Create and assemble the test rocket
            CreateTestRocket();
        }
        
        /// <summary>
        /// Create the complete 3-part test rocket assembly
        /// </summary>
        private void CreateTestRocket()
        {
            var startTime = Time.GetTicksMsec();
            
            try
            {
                GD.Print("TestRocketAssembly: Creating parts from catalog");
                
                // Create parts from catalog
                CreatePartsFromCatalog();
                
                // Position parts in the scene
                PositionParts();
                
                // Assemble parts together
                AssembleParts();
                
                // Calculate mass properties
                CalculateMassProperties();
                
                // Validate the assembly
                ValidateAssembly();
                
                var duration = Time.GetTicksMsec() - startTime;
                GD.Print($"TestRocketAssembly: Assembly completed in {duration:F1}ms");
                
                // Performance validation
                if (duration > 10.0) // Target: <10ms for test assembly
                {
                    GD.PrintErr($"TestRocketAssembly: Assembly took {duration:F1}ms (target: <10ms)");
                }
                
                IsAssembled = true;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"TestRocketAssembly: Exception during assembly: {ex.Message}");
                IsAssembled = false;
            }
        }
        
        /// <summary>
        /// Create parts from the part catalog
        /// </summary>
        private void CreatePartsFromCatalog()
        {
            var partStartTime = Time.GetTicksMsec();
            
            // Create CommandPod
            _commandPod = PartCatalog.CreatePart("command-pod") as CommandPod;
            if (_commandPod == null)
            {
                throw new Exception("Failed to create CommandPod from catalog");
            }
            
            // Create FuelTank
            _fuelTank = PartCatalog.CreatePart("fuel-tank") as FuelTank;
            if (_fuelTank == null)
            {
                throw new Exception("Failed to create FuelTank from catalog");
            }
            
            // Create Engine
            _engine = PartCatalog.CreatePart("engine") as Engine;
            if (_engine == null)
            {
                throw new Exception("Failed to create Engine from catalog");
            }
            
            // Add parts to scene tree
            AddChild(_commandPod);
            AddChild(_fuelTank);
            AddChild(_engine);
            
            // Store all parts
            AllParts.Clear();
            AllParts.Add(_commandPod);
            AllParts.Add(_fuelTank);
            AllParts.Add(_engine);
            
            var partDuration = Time.GetTicksMsec() - partStartTime;
            if (partDuration > 3.0) // Target: <1ms per part * 3 parts = 3ms
            {
                GD.PrintErr($"TestRocketAssembly: Part creation took {partDuration:F1}ms (target: <3ms)");
            }
            
            GD.Print($"TestRocketAssembly: Created {AllParts.Count} parts from catalog in {partDuration:F1}ms");
        }
        
        /// <summary>
        /// Position parts in a vertical stack configuration
        /// </summary>
        private void PositionParts()
        {
            if (_commandPod == null || _fuelTank == null || _engine == null)
                return;
            
            // Stack configuration: CommandPod (top) -> FuelTank (middle) -> Engine (bottom)
            var stackHeight = 0.0f;
            
            // Position CommandPod at top
            _commandPod.Position = new Vector3(0, stackHeight, 0);
            stackHeight -= 2.0f; // CommandPod height
            
            // Position FuelTank in middle
            _fuelTank.Position = new Vector3(0, stackHeight, 0);
            stackHeight -= 3.0f; // FuelTank height
            
            // Position Engine at bottom
            _engine.Position = new Vector3(0, stackHeight, 0);
            
            GD.Print($"TestRocketAssembly: Positioned parts in vertical stack (total height: {Math.Abs(stackHeight) + 2.0f:F1}m)");
        }
        
        /// <summary>
        /// Assemble parts together using attachment system
        /// </summary>
        private void AssembleParts()
        {
            if (_commandPod == null || _fuelTank == null || _engine == null)
                return;
            
            var assemblyStartTime = Time.GetTicksMsec();
            
            try
            {
                // Attach CommandPod to FuelTank (CommandPod bottom -> FuelTank top)
                if (_fuelTank.AttachTop != null)
                {
                    var attachSuccess = _fuelTank.AttachPart(_commandPod, _fuelTank.AttachTop);
                    if (!attachSuccess)
                    {
                        throw new Exception("Failed to attach CommandPod to FuelTank");
                    }
                    GD.Print("TestRocketAssembly: CommandPod attached to FuelTank");
                }
                else
                {
                    throw new Exception("FuelTank has no top attachment node");
                }
                
                // Attach FuelTank to Engine (FuelTank bottom -> Engine top)
                if (_engine.AttachTop != null)
                {
                    var attachSuccess = _engine.AttachPart(_fuelTank, _engine.AttachTop);
                    if (!attachSuccess)
                    {
                        throw new Exception("Failed to attach FuelTank to Engine");
                    }
                    GD.Print("TestRocketAssembly: FuelTank attached to Engine");
                }
                else
                {
                    throw new Exception("Engine has no top attachment node");
                }
                
                // Connect fuel systems (Engine to FuelTank)
                _engine.ConnectToFuelTank(_fuelTank);
                
                var assemblyDuration = Time.GetTicksMsec() - assemblyStartTime;
                GD.Print($"TestRocketAssembly: Parts assembled in {assemblyDuration:F1}ms");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"TestRocketAssembly: Assembly failed: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Calculate mass properties for the assembled rocket
        /// </summary>
        private void CalculateMassProperties()
        {
            var massStartTime = Time.GetTicksMsec();
            
            try
            {
                // Calculate total mass
                TotalMass = 0.0;
                var massDetails = new List<string>();
                
                foreach (var part in AllParts)
                {
                    var partMass = part.GetTotalMass();
                    TotalMass += partMass;
                    massDetails.Add($"{part.PartName}: {partMass:F0}kg");
                }
                
                // Calculate center of mass (simplified - using part positions)
                var weightedPosition = Vector3.Zero;
                foreach (var part in AllParts)
                {
                    var partMass = part.GetTotalMass();
                    weightedPosition += part.GlobalPosition * (float)partMass;
                }
                
                if (TotalMass > 0)
                {
                    CenterOfMass = weightedPosition / (float)TotalMass;
                }
                
                var massDuration = Time.GetTicksMsec() - massStartTime;
                if (massDuration > 0.1) // Target: <0.1ms for mass calculation
                {
                    GD.PrintErr($"TestRocketAssembly: Mass calculation took {massDuration:F1}ms (target: <0.1ms)");
                }
                
                GD.Print($"TestRocketAssembly: Mass calculation completed - Total: {TotalMass:F0}kg");
                foreach (var detail in massDetails)
                {
                    GD.Print($"  {detail}");
                }
                GD.Print($"  Center of Mass: ({CenterOfMass.X:F2}, {CenterOfMass.Y:F2}, {CenterOfMass.Z:F2})");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"TestRocketAssembly: Mass calculation failed: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Validate the assembled rocket against expected values
        /// </summary>
        private void ValidateAssembly()
        {
            var validationResults = new List<string>();
            var validationPassed = true;
            
            try
            {
                // Validate part count
                if (AllParts.Count == 3)
                {
                    validationResults.Add("✅ Part count: 3 parts created successfully");
                }
                else
                {
                    validationResults.Add($"❌ Part count: Expected 3, got {AllParts.Count}");
                    validationPassed = false;
                }
                
                // Validate total mass
                var massError = Math.Abs(TotalMass - ExpectedTotalMass);
                if (massError <= MassToleranceKg)
                {
                    validationResults.Add($"✅ Total mass: {TotalMass:F0}kg (expected: {ExpectedTotalMass:F0}kg, error: {massError:F1}kg)");
                }
                else
                {
                    validationResults.Add($"❌ Total mass: {TotalMass:F0}kg (expected: {ExpectedTotalMass:F0}kg, error: {massError:F1}kg)");
                    validationPassed = false;
                }
                
                // Validate part types and properties
                if (_commandPod != null)
                {
                    var commandPodMass = _commandPod.GetTotalMass();
                    if (Math.Abs(commandPodMass - 100.0) <= 1.0)
                    {
                        validationResults.Add($"✅ CommandPod mass: {commandPodMass:F0}kg");
                    }
                    else
                    {
                        validationResults.Add($"❌ CommandPod mass: {commandPodMass:F0}kg (expected: 100kg)");
                        validationPassed = false;
                    }
                }
                
                if (_fuelTank != null)
                {
                    var fuelTankMass = _fuelTank.GetTotalMass();
                    if (Math.Abs(fuelTankMass - 4100.0) <= 50.0) // 500 dry + ~3600 fuel
                    {
                        validationResults.Add($"✅ FuelTank mass: {fuelTankMass:F0}kg (with fuel)");
                    }
                    else
                    {
                        validationResults.Add($"❌ FuelTank mass: {fuelTankMass:F0}kg (expected: ~4100kg)");
                        validationPassed = false;
                    }
                }
                
                if (_engine != null)
                {
                    var engineMass = _engine.GetTotalMass();
                    if (Math.Abs(engineMass - 250.0) <= 1.0)
                    {
                        validationResults.Add($"✅ Engine mass: {engineMass:F0}kg");
                    }
                    else
                    {
                        validationResults.Add($"❌ Engine mass: {engineMass:F0}kg (expected: 250kg)");
                        validationPassed = false;
                    }
                }
                
                // Validate attachment system
                if (_commandPod?.IsAttached == true && _fuelTank?.AttachedChildren.Count > 0)
                {
                    validationResults.Add("✅ CommandPod attached to FuelTank");
                }
                else
                {
                    validationResults.Add("❌ CommandPod not properly attached to FuelTank");
                    validationPassed = false;
                }
                
                if (_fuelTank?.IsAttached == true && _engine?.AttachedChildren.Count > 0)
                {
                    validationResults.Add("✅ FuelTank attached to Engine");
                }
                else
                {
                    validationResults.Add("❌ FuelTank not properly attached to Engine");
                    validationPassed = false;
                }
                
                // Validate part initialization
                var initializedCount = 0;
                foreach (var part in AllParts)
                {
                    if (part.IsInitialized)
                        initializedCount++;
                }
                
                if (initializedCount == AllParts.Count)
                {
                    validationResults.Add($"✅ All parts initialized ({initializedCount}/{AllParts.Count})");
                }
                else
                {
                    validationResults.Add($"❌ Not all parts initialized ({initializedCount}/{AllParts.Count})");
                    validationPassed = false;
                }
                
                IsValidated = validationPassed;
                ValidationResults = string.Join("\n", validationResults);
                
                // Print results
                GD.Print("\n==========================================");
                GD.Print("TestRocketAssembly: Validation Results");
                GD.Print("==========================================");
                
                foreach (var result in validationResults)
                {
                    GD.Print(result);
                }
                
                var overallResult = validationPassed ? "✅ PASSED" : "❌ FAILED";
                GD.Print($"\nOverall validation: {overallResult}");
                GD.Print("==========================================\n");
            }
            catch (Exception ex)
            {
                ValidationResults = $"❌ Validation exception: {ex.Message}";
                IsValidated = false;
                GD.PrintErr($"TestRocketAssembly: Validation failed with exception: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Get assembly statistics for debugging
        /// </summary>
        public AssemblyStats GetAssemblyStats()
        {
            return new AssemblyStats
            {
                IsAssembled = IsAssembled,
                IsValidated = IsValidated,
                PartCount = AllParts.Count,
                TotalMass = TotalMass,
                ExpectedMass = ExpectedTotalMass,
                MassError = Math.Abs(TotalMass - ExpectedTotalMass),
                CenterOfMass = CenterOfMass,
                ValidationResults = ValidationResults
            };
        }
        
        /// <summary>
        /// Test individual part functionality
        /// </summary>
        public void TestPartFunctionality()
        {
            GD.Print("\nTestRocketAssembly: Testing part functionality");
            
            try
            {
                // Test CommandPod
                if (_commandPod != null)
                {
                    GD.Print($"CommandPod: {_commandPod.GetStatusSummary()}");
                    _commandPod.BoardCrew();
                    _commandPod.ToggleSAS();
                }
                
                // Test FuelTank
                if (_fuelTank != null)
                {
                    GD.Print($"FuelTank: {_fuelTank.GetStatusSummary()}");
                    var fuelPercentage = _fuelTank.GetFuelPercentage();
                    GD.Print($"  Fuel level: {fuelPercentage:P1}");
                }
                
                // Test Engine
                if (_engine != null)
                {
                    GD.Print($"Engine: {_engine.GetStatusSummary()}");
                    _engine.SetThrottle(0.5);
                    var stats = _engine.GetEngineStats();
                    GD.Print($"  Throttle set to 50%, thrust: {stats.CurrentThrust/1000:F0}kN");
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"TestRocketAssembly: Functionality test failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Cleanup assembly resources
        /// </summary>
        public override void _ExitTree()
        {
            AllParts.Clear();
            base._ExitTree();
        }
    }
    
    /// <summary>
    /// Assembly statistics structure
    /// </summary>
    public struct AssemblyStats
    {
        public bool IsAssembled;
        public bool IsValidated;
        public int PartCount;
        public double TotalMass;
        public double ExpectedMass;
        public double MassError;
        public Vector3 CenterOfMass;
        public string ValidationResults;
    }
}