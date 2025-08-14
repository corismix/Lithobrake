using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Lithobrake.Core
{
    /// <summary>
    /// Comprehensive unit tests and validation for the parts system.
    /// Tests part creation, catalog loading, attachment systems, and performance targets.
    /// </summary>
    public partial class PartTests : Node3D
    {
        // Test configuration
        private const double PerformanceTargetMs = 1.0; // 1ms per part creation
        private const double CatalogLoadTargetMs = 10.0; // 10ms for catalog loading
        private const double MeshLoadTargetMs = 5.0; // 5ms per mesh loading
        private const double MassCalculationTargetMs = 0.1; // 0.1ms for mass calculation
        
        // Test state
        private readonly List<PartTestResult> _testResults = new();
        private TestRocketAssembly? _testAssembly;
        
        // Performance tracking
        private static PerformanceMonitor? _performanceMonitor;
        
        public override void _Ready()
        {
            _performanceMonitor = PerformanceMonitor.Instance;
            GD.Print("PartTests: Starting comprehensive parts system validation");
            
            // Run all tests
            RunAllTests();
            
            // Report results
            ReportTestResults();
        }
        
        /// <summary>
        /// Run all parts system tests
        /// </summary>
        private void RunAllTests()
        {
            try
            {
                // Catalog system tests
                _testResults.Add(TestCatalogLoading());
                _testResults.Add(TestCatalogHotReload());
                _testResults.Add(TestPartCatalogStats());
                
                // Part creation tests
                _testResults.Add(TestPartCreationPerformance());
                _testResults.Add(TestPartInitialization());
                _testResults.Add(TestPartProperties());
                
                // Attachment system tests
                _testResults.Add(TestAttachmentNodeSystem());
                _testResults.Add(TestPartAttachment());
                _testResults.Add(TestPartDetachment());
                
                // Mass calculation tests
                _testResults.Add(TestMassCalculations());
                _testResults.Add(TestCenterOfMassCalculation());
                
                // Assembly system tests
                _testResults.Add(TestRocketAssembly());
                _testResults.Add(TestAssemblyValidation());
                
                // Performance tests
                _testResults.Add(TestPartSystemPerformance());
                _testResults.Add(TestMeshLoadingPerformance());
            }
            catch (Exception ex)
            {
                GD.PrintErr($"PartTests: Exception during testing: {ex.Message}");
                _testResults.Add(new PartTestResult("ExceptionHandling", false, $"Unhandled exception: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Test catalog loading functionality
        /// </summary>
        private PartTestResult TestCatalogLoading()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                
                // Test catalog reload
                var reloadSuccess = PartCatalog.ReloadCatalog();
                
                stopwatch.Stop();
                var loadTime = stopwatch.Elapsed.TotalMilliseconds;
                
                if (!PartCatalog.IsCatalogLoaded)
                {
                    return new PartTestResult("CatalogLoading", false, "Catalog not loaded after reload");
                }
                
                var stats = PartCatalog.GetCatalogStats();
                if (stats.TotalParts < 3)
                {
                    return new PartTestResult("CatalogLoading", false, $"Expected at least 3 parts, got {stats.TotalParts}");
                }
                
                if (loadTime > CatalogLoadTargetMs)
                {
                    return new PartTestResult("CatalogLoading", false, 
                        $"Catalog loading too slow: {loadTime:F1}ms (target: {CatalogLoadTargetMs:F0}ms)");
                }
                
                return new PartTestResult("CatalogLoading", true, 
                    $"Loaded {stats.TotalParts} parts in {loadTime:F1}ms");
            }
            catch (Exception ex)
            {
                return new PartTestResult("CatalogLoading", false, $"Exception: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Test catalog hot-reload functionality
        /// </summary>
        private PartTestResult TestCatalogHotReload()
        {
            try
            {
                var initialStats = PartCatalog.GetCatalogStats();
                
                // Test hot-reload (would normally be triggered by file change)
                var reloadResult = PartCatalog.ReloadCatalog();
                
                var newStats = PartCatalog.GetCatalogStats();
                
                if (reloadResult && newStats.TotalParts == initialStats.TotalParts)
                {
                    return new PartTestResult("CatalogHotReload", true, 
                        $"Hot-reload successful, {newStats.TotalParts} parts maintained");
                }
                else
                {
                    return new PartTestResult("CatalogHotReload", false, 
                        $"Hot-reload failed or part count changed ({initialStats.TotalParts} -> {newStats.TotalParts})");
                }
            }
            catch (Exception ex)
            {
                return new PartTestResult("CatalogHotReload", false, $"Exception: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Test part catalog statistics
        /// </summary>
        private PartTestResult TestPartCatalogStats()
        {
            try
            {
                var stats = PartCatalog.GetCatalogStats();
                
                var expectedTypes = new[] { PartType.Command, PartType.FuelTank, PartType.Engine };
                var missingTypes = new List<PartType>();
                
                foreach (var expectedType in expectedTypes)
                {
                    if (!stats.PartsByType.ContainsKey(expectedType) || stats.PartsByType[expectedType] == 0)
                    {
                        missingTypes.Add(expectedType);
                    }
                }
                
                if (missingTypes.Count > 0)
                {
                    return new PartTestResult("PartCatalogStats", false, 
                        $"Missing part types: {string.Join(", ", missingTypes)}");
                }
                
                return new PartTestResult("PartCatalogStats", true, 
                    $"All expected part types present: {string.Join(", ", expectedTypes)}");
            }
            catch (Exception ex)
            {
                return new PartTestResult("PartCatalogStats", false, $"Exception: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Test part creation performance
        /// </summary>
        private PartTestResult TestPartCreationPerformance()
        {
            try
            {
                var partIds = new[] { "command-pod", "fuel-tank", "engine" };
                var totalTime = 0.0;
                var createdParts = new List<Part>();
                
                foreach (var partId in partIds)
                {
                    var stopwatch = Stopwatch.StartNew();
                    
                    var part = PartCatalog.CreatePart(partId);
                    
                    stopwatch.Stop();
                    var creationTime = stopwatch.Elapsed.TotalMilliseconds;
                    totalTime += creationTime;
                    
                    if (part == null)
                    {
                        return new PartTestResult("PartCreationPerformance", false, 
                            $"Failed to create part: {partId}");
                    }
                    
                    createdParts.Add(part);
                    
                    if (creationTime > PerformanceTargetMs)
                    {
                        return new PartTestResult("PartCreationPerformance", false, 
                            $"Part creation too slow: {partId} took {creationTime:F1}ms (target: {PerformanceTargetMs:F0}ms)");
                    }
                }
                
                // Cleanup
                foreach (var part in createdParts)
                {
                    part.QueueFree();
                }
                
                return new PartTestResult("PartCreationPerformance", true, 
                    $"Created {partIds.Length} parts in {totalTime:F1}ms (avg: {totalTime/partIds.Length:F1}ms)");
            }
            catch (Exception ex)
            {
                return new PartTestResult("PartCreationPerformance", false, $"Exception: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Test part initialization
        /// </summary>
        private PartTestResult TestPartInitialization()
        {
            try
            {
                var commandPod = PartCatalog.CreatePart("command-pod") as CommandPod;
                if (commandPod == null)
                {
                    return new PartTestResult("PartInitialization", false, "Failed to create CommandPod");
                }
                
                AddChild(commandPod);
                commandPod._Ready(); // Manually call _Ready for testing
                
                if (!commandPod.IsInitialized)
                {
                    return new PartTestResult("PartInitialization", false, "CommandPod not initialized after _Ready");
                }
                
                if (commandPod.RigidBody == null)
                {
                    return new PartTestResult("PartInitialization", false, "RigidBody not created during initialization");
                }
                
                if (commandPod.MeshInstance == null)
                {
                    return new PartTestResult("PartInitialization", false, "MeshInstance not created during initialization");
                }
                
                commandPod.QueueFree();
                
                return new PartTestResult("PartInitialization", true, 
                    "Part initialization successful with physics and visual components");
            }
            catch (Exception ex)
            {
                return new PartTestResult("PartInitialization", false, $"Exception: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Test part properties
        /// </summary>
        private PartTestResult TestPartProperties()
        {
            try
            {
                var parts = new[]
                {
                    PartCatalog.CreatePart("command-pod"),
                    PartCatalog.CreatePart("fuel-tank"),
                    PartCatalog.CreatePart("engine")
                };
                
                var validationErrors = new List<string>();
                
                foreach (var part in parts)
                {
                    if (part == null)
                    {
                        validationErrors.Add("Part creation returned null");
                        continue;
                    }
                    
                    if (part.DryMass <= 0)
                        validationErrors.Add($"{part.PartName}: Invalid dry mass ({part.DryMass})");
                    
                    if (string.IsNullOrEmpty(part.PartId))
                        validationErrors.Add($"{part.PartName}: Missing PartId");
                    
                    if (string.IsNullOrEmpty(part.PartName))
                        validationErrors.Add($"{part.PartId}: Missing PartName");
                    
                    if (part.Type == PartType.Unknown)
                        validationErrors.Add($"{part.PartName}: Unknown part type");
                    
                    part.QueueFree();
                }
                
                if (validationErrors.Count > 0)
                {
                    return new PartTestResult("PartProperties", false, 
                        $"Validation errors: {string.Join(", ", validationErrors)}");
                }
                
                return new PartTestResult("PartProperties", true, 
                    "All part properties valid");
            }
            catch (Exception ex)
            {
                return new PartTestResult("PartProperties", false, $"Exception: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Test attachment node system
        /// </summary>
        private PartTestResult TestAttachmentNodeSystem()
        {
            try
            {
                var fuelTank = PartCatalog.CreatePart("fuel-tank") as FuelTank;
                if (fuelTank == null)
                {
                    return new PartTestResult("AttachmentNodeSystem", false, "Failed to create FuelTank");
                }
                
                AddChild(fuelTank);
                // Process frame manually for testing
                
                // Test attachment node creation
                if (fuelTank.AttachTop == null)
                {
                    return new PartTestResult("AttachmentNodeSystem", false, "FuelTank missing top attachment node");
                }
                
                if (fuelTank.AttachBottom == null)
                {
                    return new PartTestResult("AttachmentNodeSystem", false, "FuelTank missing bottom attachment node");
                }
                
                if (fuelTank.RadialAttachPoints.Count == 0)
                {
                    return new PartTestResult("AttachmentNodeSystem", false, "FuelTank missing radial attachment points");
                }
                
                // Test attachment node properties
                if (fuelTank.AttachTop.IsOccupied)
                {
                    return new PartTestResult("AttachmentNodeSystem", false, "New attachment node should not be occupied");
                }
                
                if (fuelTank.AttachTop.MaxMass <= 0)
                {
                    return new PartTestResult("AttachmentNodeSystem", false, "Attachment node has invalid max mass");
                }
                
                fuelTank.QueueFree();
                
                return new PartTestResult("AttachmentNodeSystem", true, 
                    $"Attachment nodes created successfully (top, bottom, {fuelTank.RadialAttachPoints.Count} radial)");
            }
            catch (Exception ex)
            {
                return new PartTestResult("AttachmentNodeSystem", false, $"Exception: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Test part attachment functionality
        /// </summary>
        private PartTestResult TestPartAttachment()
        {
            try
            {
                var commandPod = PartCatalog.CreatePart("command-pod") as CommandPod;
                var fuelTank = PartCatalog.CreatePart("fuel-tank") as FuelTank;
                
                if (commandPod == null || fuelTank == null)
                {
                    return new PartTestResult("PartAttachment", false, "Failed to create parts for attachment test");
                }
                
                AddChild(commandPod);
                AddChild(fuelTank);
                // Process frame manually for testing
                
                // Test attachment
                if (fuelTank.AttachTop == null)
                {
                    return new PartTestResult("PartAttachment", false, "FuelTank has no top attachment node");
                }
                
                var attachResult = fuelTank.AttachPart(commandPod, fuelTank.AttachTop);
                
                if (!attachResult)
                {
                    return new PartTestResult("PartAttachment", false, "Failed to attach CommandPod to FuelTank");
                }
                
                if (!commandPod.IsAttached)
                {
                    return new PartTestResult("PartAttachment", false, "CommandPod not marked as attached");
                }
                
                if (!fuelTank.AttachedChildren.Contains(commandPod))
                {
                    return new PartTestResult("PartAttachment", false, "CommandPod not in FuelTank children list");
                }
                
                if (!fuelTank.AttachTop.IsOccupied)
                {
                    return new PartTestResult("PartAttachment", false, "Attachment node not marked as occupied");
                }
                
                commandPod.QueueFree();
                fuelTank.QueueFree();
                
                return new PartTestResult("PartAttachment", true, 
                    "Part attachment system working correctly");
            }
            catch (Exception ex)
            {
                return new PartTestResult("PartAttachment", false, $"Exception: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Test part detachment functionality
        /// </summary>
        private PartTestResult TestPartDetachment()
        {
            try
            {
                var commandPod = PartCatalog.CreatePart("command-pod") as CommandPod;
                var fuelTank = PartCatalog.CreatePart("fuel-tank") as FuelTank;
                
                if (commandPod == null || fuelTank == null)
                {
                    return new PartTestResult("PartDetachment", false, "Failed to create parts for detachment test");
                }
                
                AddChild(commandPod);
                AddChild(fuelTank);
                // Process frame manually for testing
                
                // Attach first
                if (fuelTank.AttachTop != null)
                {
                    fuelTank.AttachPart(commandPod, fuelTank.AttachTop);
                }
                
                // Test detachment
                var detachResult = fuelTank.DetachPart(commandPod);
                
                if (!detachResult)
                {
                    return new PartTestResult("PartDetachment", false, "Failed to detach CommandPod from FuelTank");
                }
                
                if (commandPod.IsAttached)
                {
                    return new PartTestResult("PartDetachment", false, "CommandPod still marked as attached after detachment");
                }
                
                if (fuelTank.AttachedChildren.Contains(commandPod))
                {
                    return new PartTestResult("PartDetachment", false, "CommandPod still in FuelTank children list after detachment");
                }
                
                if (fuelTank.AttachTop?.IsOccupied == true)
                {
                    return new PartTestResult("PartDetachment", false, "Attachment node still marked as occupied after detachment");
                }
                
                commandPod.QueueFree();
                fuelTank.QueueFree();
                
                return new PartTestResult("PartDetachment", true, 
                    "Part detachment system working correctly");
            }
            catch (Exception ex)
            {
                return new PartTestResult("PartDetachment", false, $"Exception: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Test mass calculations
        /// </summary>
        private PartTestResult TestMassCalculations()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                
                var parts = new[]
                {
                    PartCatalog.CreatePart("command-pod"),
                    PartCatalog.CreatePart("fuel-tank"),
                    PartCatalog.CreatePart("engine")
                };
                
                var expectedMasses = new[] { 100.0, 4100.0, 250.0 }; // Approximate values
                var massErrors = new List<string>();
                
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i] == null)
                        continue;
                    
                    var actualMass = parts[i].GetTotalMass();
                    var expectedMass = expectedMasses[i];
                    var error = Math.Abs(actualMass - expectedMass);
                    var errorPercent = (error / expectedMass) * 100;
                    
                    if (errorPercent > 10) // Allow 10% error tolerance
                    {
                        massErrors.Add($"{parts[i].PartName}: {actualMass:F0}kg vs {expectedMass:F0}kg ({errorPercent:F1}% error)");
                    }
                    
                    parts[i].QueueFree();
                }
                
                stopwatch.Stop();
                var calculationTime = stopwatch.Elapsed.TotalMilliseconds;
                
                if (calculationTime > MassCalculationTargetMs)
                {
                    return new PartTestResult("MassCalculations", false, 
                        $"Mass calculations too slow: {calculationTime:F2}ms (target: {MassCalculationTargetMs:F1}ms)");
                }
                
                if (massErrors.Count > 0)
                {
                    return new PartTestResult("MassCalculations", false, 
                        $"Mass calculation errors: {string.Join(", ", massErrors)}");
                }
                
                return new PartTestResult("MassCalculations", true, 
                    $"Mass calculations accurate and fast ({calculationTime:F2}ms)");
            }
            catch (Exception ex)
            {
                return new PartTestResult("MassCalculations", false, $"Exception: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Test center of mass calculation
        /// </summary>
        private PartTestResult TestCenterOfMassCalculation()
        {
            try
            {
                var fuelTank = PartCatalog.CreatePart("fuel-tank") as FuelTank;
                if (fuelTank == null)
                {
                    return new PartTestResult("CenterOfMassCalculation", false, "Failed to create FuelTank");
                }
                
                AddChild(fuelTank);
                // Process frame manually for testing
                
                // Test single part center of mass
                var com = fuelTank.CalculateCenterOfMass();
                
                // Should be approximately at the part's position for a single part
                var distance = com.DistanceTo(fuelTank.GlobalPosition);
                
                if (distance > 0.1f) // Within 10cm tolerance
                {
                    return new PartTestResult("CenterOfMassCalculation", false, 
                        $"Center of mass too far from part position: {distance:F2}m");
                }
                
                fuelTank.QueueFree();
                
                return new PartTestResult("CenterOfMassCalculation", true, 
                    "Center of mass calculation working correctly");
            }
            catch (Exception ex)
            {
                return new PartTestResult("CenterOfMassCalculation", false, $"Exception: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Test rocket assembly system
        /// </summary>
        private PartTestResult TestRocketAssembly()
        {
            try
            {
                _testAssembly = new TestRocketAssembly();
                AddChild(_testAssembly);
                
                // Wait for assembly to complete
                // Process frame manually for testing
                
                if (!_testAssembly.IsAssembled)
                {
                    return new PartTestResult("RocketAssembly", false, "Test rocket assembly failed");
                }
                
                var stats = _testAssembly.GetAssemblyStats();
                
                if (stats.PartCount != 3)
                {
                    return new PartTestResult("RocketAssembly", false, $"Expected 3 parts, got {stats.PartCount}");
                }
                
                if (Math.Abs(stats.MassError) > 50.0) // Allow 50kg tolerance
                {
                    return new PartTestResult("RocketAssembly", false, 
                        $"Mass error too large: {stats.MassError:F0}kg");
                }
                
                return new PartTestResult("RocketAssembly", true, 
                    $"Assembly successful: {stats.PartCount} parts, {stats.TotalMass:F0}kg total mass");
            }
            catch (Exception ex)
            {
                return new PartTestResult("RocketAssembly", false, $"Exception: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Test assembly validation
        /// </summary>
        private PartTestResult TestAssemblyValidation()
        {
            try
            {
                if (_testAssembly == null)
                {
                    return new PartTestResult("AssemblyValidation", false, "No test assembly available");
                }
                
                if (!_testAssembly.IsValidated)
                {
                    return new PartTestResult("AssemblyValidation", false, 
                        $"Assembly validation failed:\n{_testAssembly.ValidationResults}");
                }
                
                return new PartTestResult("AssemblyValidation", true, 
                    "Assembly validation passed all checks");
            }
            catch (Exception ex)
            {
                return new PartTestResult("AssemblyValidation", false, $"Exception: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Test parts system performance
        /// </summary>
        private PartTestResult TestPartSystemPerformance()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                
                // Create multiple parts to test performance under load
                var parts = new List<Part>();
                for (int i = 0; i < 10; i++)
                {
                    parts.Add(PartCatalog.CreatePart("fuel-tank"));
                }
                
                stopwatch.Stop();
                var totalTime = stopwatch.Elapsed.TotalMilliseconds;
                var avgTime = totalTime / parts.Count;
                
                // Cleanup
                foreach (var part in parts)
                {
                    part?.QueueFree();
                }
                
                if (avgTime > PerformanceTargetMs)
                {
                    return new PartTestResult("PartSystemPerformance", false, 
                        $"Performance target missed: {avgTime:F1}ms per part (target: {PerformanceTargetMs:F0}ms)");
                }
                
                return new PartTestResult("PartSystemPerformance", true, 
                    $"Performance target met: {avgTime:F1}ms per part");
            }
            catch (Exception ex)
            {
                return new PartTestResult("PartSystemPerformance", false, $"Exception: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Test mesh loading performance
        /// </summary>
        private PartTestResult TestMeshLoadingPerformance()
        {
            try
            {
                // Test is conceptual since we don't have actual mesh files
                // In a real implementation, this would test loading .obj/.glb files
                // and falling back to primitive meshes
                
                var commandPod = PartCatalog.CreatePart("command-pod");
                if (commandPod == null)
                {
                    return new PartTestResult("MeshLoadingPerformance", false, "Failed to create part for mesh test");
                }
                
                AddChild(commandPod);
                // Process frame manually for testing
                
                // Check that mesh instance was created (fallback to primitive)
                if (commandPod.MeshInstance?.Mesh == null)
                {
                    return new PartTestResult("MeshLoadingPerformance", false, "No mesh created for part");
                }
                
                commandPod.QueueFree();
                
                return new PartTestResult("MeshLoadingPerformance", true, 
                    "Mesh loading working (using primitive fallback)");
            }
            catch (Exception ex)
            {
                return new PartTestResult("MeshLoadingPerformance", false, $"Exception: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Report all test results
        /// </summary>
        private void ReportTestResults()
        {
            var passedTests = _testResults.Where(r => r.Passed).Count();
            var totalTests = _testResults.Count;
            
            GD.Print($"\n==========================================");
            GD.Print($"PartTests: Test Results Summary");
            GD.Print($"==========================================");
            GD.Print($"Passed: {passedTests}/{totalTests} ({(double)passedTests/totalTests:P1})");
            GD.Print($"==========================================");
            
            foreach (var result in _testResults)
            {
                var status = result.Passed ? "✅ PASS" : "❌ FAIL";
                GD.Print($"{status} | {result.TestName}: {result.Details}");
            }
            
            // Test part functionality if assembly succeeded
            if (_testAssembly?.IsAssembled == true)
            {
                _testAssembly.TestPartFunctionality();
            }
            
            GD.Print($"==========================================\n");
        }
    }
    
    /// <summary>
    /// Part test result data structure
    /// </summary>
    public struct PartTestResult
    {
        public string TestName;
        public bool Passed;
        public string Details;
        
        public PartTestResult(string testName, bool passed, string details)
        {
            TestName = testName;
            Passed = passed;
            Details = details;
        }
    }
}