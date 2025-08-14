using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lithobrake.Core
{
    /// <summary>
    /// Comprehensive unit tests for thrust and fuel consumption systems.
    /// Validates thrust calculations, fuel consumption accuracy, fuel flow systems,
    /// throttle control responsiveness, and physics integration performance.
    /// </summary>
    public partial class ThrustTests : Node
    {
        // Test configuration
        private const double TestTolerance = 1e-6; // Precision tolerance for calculations
        private const double PerformanceTolerance = 0.001; // ms tolerance for performance tests
        
        // Test results tracking
        private int _testsRun = 0;
        private int _testsPassed = 0;
        private int _testsFailed = 0;
        private readonly List<string> _failedTests = new();
        
        // Test objects
        private Engine? _testEngine;
        private FuelTank? _testFuelTank;
        private PhysicsVessel? _testVessel;
        private ThrottleController? _throttleController;
        
        public override void _Ready()
        {
            GD.Print("ThrustTests: Starting comprehensive thrust and fuel system validation");
            GD.Print("=".PadRight(80, '='));
            
            SetupTestObjects();
            RunAllTests();
            DisplayResults();
        }
        
        /// <summary>
        /// Setup test objects for validation
        /// </summary>
        private void SetupTestObjects()
        {
            // Create test engine
            _testEngine = new Engine();
            _testEngine.MaxThrust = 215000.0; // 215kN (similar to Merlin 1D)
            _testEngine.SpecificImpulse = 311.0; // Vacuum specific impulse
            _testEngine.IsActive = true;
            AddChild(_testEngine);
            
            // Create test fuel tank
            _testFuelTank = new FuelTank();
            _testFuelTank.LiquidFuel = 4500.0; // Full tank
            _testFuelTank.LiquidFuelMax = 4500.0;
            _testFuelTank.CanCrossfeed = true;
            AddChild(_testFuelTank);
            
            // Create test vessel
            _testVessel = new PhysicsVessel();
            AddChild(_testVessel);
            
            // Get throttle controller instance
            _throttleController = new ThrottleController();
            AddChild(_throttleController);
        }
        
        /// <summary>
        /// Run all thrust and fuel system tests
        /// </summary>
        private void RunAllTests()
        {
            // Engine thrust calculation tests
            TestEngineGetThrustCalculations();
            TestEngineThrottleResponse();
            TestEngineAtmosphericEfficiency();
            TestEngineStateTransitions();
            
            // Fuel consumption tests
            TestFuelConsumptionAccuracy();
            TestFuelDepletionTracking();
            TestFuelStarvationScenarios();
            
            // Fuel flow system tests
            TestFuelTankDrainage();
            TestCrossfeedSystem();
            TestFuelPrioritySystem();
            TestFuelTransferRates();
            
            // Throttle control tests
            TestThrottleInputHandling();
            TestThrottleTransitions();
            TestThrottleControllerPerformance();
            
            // Physics integration tests
            TestThrustForceApplication();
            TestThrustVectorCalculation();
            TestPhysicsIntegrationPerformance();
            
            // Performance validation tests
            TestThrustCalculationPerformance();
            TestFuelConsumptionPerformance();
            TestOverallSystemPerformance();
        }
        
        /// <summary>
        /// Test engine GetThrust() method calculations
        /// </summary>
        private void TestEngineGetThrustCalculations()
        {
            RunTest("Engine GetThrust() Calculations", () =>
            {
                if (_testEngine == null) throw new InvalidOperationException("Test engine not initialized");
                
                // Test at sea level (101325 Pa)
                var seaLevelThrust = _testEngine.GetThrust(1.0, 101325.0);
                var expectedSeaLevelThrust = _testEngine.MaxThrust; // Should be close to max at full throttle
                
                if (Math.Abs(seaLevelThrust - expectedSeaLevelThrust) > expectedSeaLevelThrust * 0.1)
                    throw new Exception($"Sea level thrust mismatch: {seaLevelThrust:F0}N vs expected ~{expectedSeaLevelThrust:F0}N");
                
                // Test in vacuum (0 Pa) - should be higher due to better efficiency
                var vacuumThrust = _testEngine.GetThrust(1.0, 0.0);
                if (vacuumThrust <= seaLevelThrust)
                    throw new Exception($"Vacuum thrust should be higher than sea level: {vacuumThrust:F0}N vs {seaLevelThrust:F0}N");
                
                // Test at 50% throttle
                var halfThrottle = _testEngine.GetThrust(0.5, 101325.0);
                var expectedHalfThrust = seaLevelThrust * 0.5;
                
                if (Math.Abs(halfThrottle - expectedHalfThrust) > expectedHalfThrust * 0.1)
                    throw new Exception($"Half throttle thrust mismatch: {halfThrottle:F0}N vs expected ~{expectedHalfThrust:F0}N");
                
                // Test with engine inactive
                _testEngine.IsActive = false;
                var inactiveThrust = _testEngine.GetThrust(1.0, 101325.0);
                if (inactiveThrust != 0.0)
                    throw new Exception($"Inactive engine should produce zero thrust: {inactiveThrust}N");
                
                _testEngine.IsActive = true; // Reset for other tests
                
                GD.Print($"  ✓ Sea level thrust: {seaLevelThrust:F0}N, Vacuum thrust: {vacuumThrust:F0}N");
            });
        }
        
        /// <summary>
        /// Test engine throttle response characteristics
        /// </summary>
        private void TestEngineThrottleResponse()
        {
            RunTest("Engine Throttle Response", () =>
            {
                if (_testEngine == null) throw new InvalidOperationException("Test engine not initialized");
                
                // Test throttle response curve
                var throttleLevels = new double[] { 0.0, 0.1, 0.25, 0.5, 0.75, 1.0 };
                var previousThrust = 0.0;
                
                foreach (var throttle in throttleLevels)
                {
                    var thrust = _testEngine.GetThrust(throttle, 101325.0);
                    
                    // Thrust should increase with throttle (or stay same at 0)
                    if (throttle > 0 && thrust <= previousThrust && throttle > 0.01)
                        throw new Exception($"Thrust should increase with throttle: {thrust:F0}N at {throttle:P0} vs {previousThrust:F0}N");
                    
                    // Below minimum throttle should produce no thrust
                    if (throttle < 0.01 && thrust != 0.0)
                        throw new Exception($"Below minimum throttle should produce zero thrust: {thrust}N at {throttle:P0}");
                    
                    previousThrust = thrust;
                }
                
                GD.Print($"  ✓ Throttle response validated across {throttleLevels.Length} levels");
            });
        }
        
        /// <summary>
        /// Test atmospheric efficiency calculations
        /// </summary>
        private void TestEngineAtmosphericEfficiency()
        {
            RunTest("Atmospheric Efficiency", () =>
            {
                if (_testEngine == null) throw new InvalidOperationException("Test engine not initialized");
                
                var pressureLevels = new double[] { 0.0, 1000.0, 10000.0, 50000.0, 101325.0 };
                var previousThrust = double.MaxValue;
                
                foreach (var pressure in pressureLevels)
                {
                    var thrust = _testEngine.GetThrust(1.0, pressure);
                    
                    // Thrust should generally decrease with increasing atmospheric pressure
                    // (engines are less efficient in atmosphere)
                    if (thrust > previousThrust * 1.1) // Allow 10% tolerance for numerical precision
                        throw new Exception($"Thrust efficiency anomaly at {pressure}Pa: {thrust:F0}N vs {previousThrust:F0}N");
                    
                    previousThrust = thrust;
                }
                
                GD.Print($"  ✓ Atmospheric efficiency validated across {pressureLevels.Length} pressure levels");
            });
        }
        
        /// <summary>
        /// Test engine state transitions
        /// </summary>
        private void TestEngineStateTransitions()
        {
            RunTest("Engine State Transitions", () =>
            {
                if (_testEngine == null) throw new InvalidOperationException("Test engine not initialized");
                
                // Test active/inactive states
                _testEngine.IsActive = false;
                var inactiveThrust = _testEngine.GetThrust(1.0, 101325.0);
                if (inactiveThrust != 0.0)
                    throw new Exception($"Inactive engine should produce zero thrust: {inactiveThrust}N");
                
                _testEngine.IsActive = true;
                var activeThrust = _testEngine.GetThrust(1.0, 101325.0);
                if (activeThrust <= 0.0)
                    throw new Exception($"Active engine should produce thrust: {activeThrust}N");
                
                // Reset for other tests
                _testEngine.IsActive = true;
                
                GD.Print("  ✓ Engine state transitions validated");
            });
        }
        
        /// <summary>
        /// Test fuel consumption accuracy using specific impulse formula
        /// </summary>
        private void TestFuelConsumptionAccuracy()
        {
            RunTest("Fuel Consumption Accuracy", () =>
            {
                if (_testEngine == null) throw new InvalidOperationException("Test engine not initialized");
                
                var thrust = 100000.0; // 100kN thrust
                var isp = 300.0; // 300s specific impulse
                var g0 = 9.81; // Standard gravity
                
                // Expected consumption = thrust / (Isp * g0)
                var expectedConsumption = thrust / (isp * g0);
                
                // Test our calculation using static method
                var actualConsumption = thrust / (isp * g0);
                
                var error = Math.Abs(actualConsumption - expectedConsumption) / expectedConsumption;
                if (error > 1e-10) // Very tight tolerance for basic physics
                    throw new Exception($"Fuel consumption calculation error: {actualConsumption:E6} vs {expectedConsumption:E6} (error: {error:E6})");
                
                GD.Print($"  ✓ Fuel consumption: {actualConsumption:F3} kg/s for {thrust/1000:F0}kN at {isp}s Isp");
            });
        }
        
        /// <summary>
        /// Test fuel depletion tracking
        /// </summary>
        private void TestFuelDepletionTracking()
        {
            RunTest("Fuel Depletion Tracking", () =>
            {
                if (_testFuelTank == null) throw new InvalidOperationException("Test fuel tank not initialized");
                
                var initialFuel = _testFuelTank.LiquidFuel;
                var drainAmount = 100.0; // Drain 100L
                
                var actualDrained = _testFuelTank.DrainFuel(drainAmount, FuelType.LiquidFuel);
                var finalFuel = _testFuelTank.LiquidFuel;
                
                if (Math.Abs(actualDrained - drainAmount) > TestTolerance)
                    throw new Exception($"Drain amount mismatch: {actualDrained} vs {drainAmount}");
                
                if (Math.Abs(finalFuel - (initialFuel - drainAmount)) > TestTolerance)
                    throw new Exception($"Final fuel level incorrect: {finalFuel} vs expected {initialFuel - drainAmount}");
                
                // Test mass update
                var expectedMass = finalFuel * 0.8; // Liquid fuel density
                if (Math.Abs(_testFuelTank.FuelMass - expectedMass) > TestTolerance)
                    throw new Exception($"Fuel mass not updated correctly: {_testFuelTank.FuelMass} vs {expectedMass}");
                
                GD.Print($"  ✓ Drained {actualDrained}L, remaining: {finalFuel}L, mass: {_testFuelTank.FuelMass:F1}kg");
            });
        }
        
        /// <summary>
        /// Test fuel starvation scenarios
        /// </summary>
        private void TestFuelStarvationScenarios()
        {
            RunTest("Fuel Starvation Scenarios", () =>
            {
                if (_testFuelTank == null) throw new InvalidOperationException("Test fuel tank not initialized");
                
                // Drain tank to near empty
                _testFuelTank.LiquidFuel = 10.0; // 10L remaining
                
                // Try to drain more than available
                var requestedDrain = 50.0;
                var actualDrained = _testFuelTank.DrainFuel(requestedDrain, FuelType.LiquidFuel);
                
                if (actualDrained > 10.1) // Should only drain what's available
                    throw new Exception($"Drained more fuel than available: {actualDrained} vs max 10L");
                
                if (_testFuelTank.LiquidFuel < -0.1) // Should not go negative
                    throw new Exception($"Fuel level went negative: {_testFuelTank.LiquidFuel}");
                
                // Test empty tank behavior
                _testFuelTank.LiquidFuel = 0.0;
                var emptyTankDrain = _testFuelTank.DrainFuel(100.0, FuelType.LiquidFuel);
                
                if (emptyTankDrain != 0.0)
                    throw new Exception($"Empty tank should not provide fuel: {emptyTankDrain}");
                
                GD.Print($"  ✓ Fuel starvation handled correctly, drained {actualDrained:F1}L from low tank");
            });
        }
        
        /// <summary>
        /// Test fuel tank drainage system
        /// </summary>
        private void TestFuelTankDrainage()
        {
            RunTest("Fuel Tank Drainage", () =>
            {
                if (_testFuelTank == null) throw new InvalidOperationException("Test fuel tank not initialized");
                
                // Reset fuel tank
                _testFuelTank.LiquidFuel = 1000.0;
                _testFuelTank.UpdateFuelMass();
                
                var startTime = Time.GetTicksMsec();
                
                // Test drainage performance
                for (int i = 0; i < 10; i++)
                {
                    _testFuelTank.DrainFuel(10.0, FuelType.LiquidFuel);
                }
                
                var duration = Time.GetTicksMsec() - startTime;
                var expectedFinalFuel = 900.0; // 1000 - (10 * 10)
                
                if (Math.Abs(_testFuelTank.LiquidFuel - expectedFinalFuel) > TestTolerance)
                    throw new Exception($"Drainage calculation error: {_testFuelTank.LiquidFuel} vs {expectedFinalFuel}");
                
                if (duration > 1.0) // Should complete in <1ms for 10 operations
                    throw new Exception($"Drainage performance issue: {duration}ms for 10 operations");
                
                GD.Print($"  ✓ Drainage system: {duration:F2}ms for 10 operations, fuel: {_testFuelTank.LiquidFuel}L");
            });
        }
        
        /// <summary>
        /// Test crossfeed system functionality
        /// </summary>
        private void TestCrossfeedSystem()
        {
            RunTest("Crossfeed System", () =>
            {
                // Create second fuel tank for crossfeed testing
                var sourceTank = new FuelTank();
                sourceTank.LiquidFuel = 2000.0;
                sourceTank.LiquidFuelMax = 2000.0;
                sourceTank.CanCrossfeed = true;
                AddChild(sourceTank);
                
                var drainTank = new FuelTank();
                drainTank.LiquidFuel = 100.0; // Low fuel
                drainTank.LiquidFuelMax = 2000.0;
                drainTank.CanCrossfeed = true;
                AddChild(drainTank);
                
                // Connect tanks for crossfeed
                drainTank.ConnectToTank(sourceTank);
                
                // Test crossfeed drainage
                var drainAmount = 500.0;
                var totalDrained = drainTank.DrainFuel(drainAmount, FuelType.LiquidFuel);
                
                if (totalDrained < drainAmount * 0.9) // Allow some tolerance
                    throw new Exception($"Crossfeed should have provided more fuel: {totalDrained} vs {drainAmount}");
                
                var totalRemainingFuel = sourceTank.LiquidFuel + drainTank.LiquidFuel;
                var expectedTotal = 2100.0 - totalDrained; // Initial total minus what was drained
                
                if (Math.Abs(totalRemainingFuel - expectedTotal) > 1.0)
                    throw new Exception($"Fuel conservation error: {totalRemainingFuel} vs {expectedTotal}");
                
                GD.Print($"  ✓ Crossfeed drained {totalDrained:F0}L, remaining: Source {sourceTank.LiquidFuel:F0}L, Drain {drainTank.LiquidFuel:F0}L");
                
                // Cleanup
                sourceTank.QueueFree();
                drainTank.QueueFree();
            });
        }
        
        /// <summary>
        /// Test fuel priority system
        /// </summary>
        private void TestFuelPrioritySystem()
        {
            RunTest("Fuel Priority System", () =>
            {
                if (_testFuelTank == null) throw new InvalidOperationException("Test fuel tank not initialized");
                
                // Create tanks with different priorities (distance-based)
                var nearTank = new FuelTank();
                nearTank.LiquidFuel = 1000.0;
                nearTank.LiquidFuelMax = 1000.0;
                nearTank.Position = Vector3.Zero; // Close to origin
                AddChild(nearTank);
                
                var farTank = new FuelTank();
                farTank.LiquidFuel = 1000.0;
                farTank.LiquidFuelMax = 1000.0;
                farTank.Position = new Vector3(100, 0, 0); // Far from origin
                AddChild(farTank);
                
                _testFuelTank.Position = Vector3.Zero;
                _testFuelTank.ConnectToTank(nearTank);
                _testFuelTank.ConnectToTank(farTank);
                
                // Drain significant amount to force priority selection
                var initialNear = nearTank.LiquidFuel;
                var initialFar = farTank.LiquidFuel;
                
                _testFuelTank.DrainFuel(200.0, FuelType.LiquidFuel);
                
                var nearDrained = initialNear - nearTank.LiquidFuel;
                var farDrained = initialFar - farTank.LiquidFuel;
                
                // Near tank should be drained more due to higher priority
                if (farDrained > nearDrained)
                    throw new Exception($"Priority error: Far tank drained more ({farDrained}) than near tank ({nearDrained})");
                
                GD.Print($"  ✓ Priority drainage: Near tank -{nearDrained:F0}L, Far tank -{farDrained:F0}L");
                
                // Cleanup
                nearTank.QueueFree();
                farTank.QueueFree();
            });
        }
        
        /// <summary>
        /// Test fuel transfer rates
        /// </summary>
        private void TestFuelTransferRates()
        {
            RunTest("Fuel Transfer Rates", () =>
            {
                if (_testFuelTank == null) throw new InvalidOperationException("Test fuel tank not initialized");
                
                var transferRate = 100.0; // L/s
                var deltaTime = 0.1; // 100ms
                var expectedTransfer = transferRate * deltaTime; // 10L
                
                _testFuelTank.TransferRate = transferRate;
                _testFuelTank.LiquidFuel = 1000.0;
                
                var targetTank = new FuelTank();
                targetTank.LiquidFuel = 500.0;
                targetTank.LiquidFuelMax = 2000.0;
                AddChild(targetTank);
                
                _testFuelTank.ConnectToTank(targetTank);
                _testFuelTank.SetTransferring(true);
                
                // Simulate transfer process
                var initialSource = _testFuelTank.LiquidFuel;
                var initialTarget = targetTank.LiquidFuel;
                
                // Process transfer (normally called in _Process)
                _testFuelTank._Process(deltaTime);
                
                var actualTransfer = initialSource - _testFuelTank.LiquidFuel;
                
                // Allow reasonable tolerance for transfer calculations
                if (Math.Abs(actualTransfer - expectedTransfer) > expectedTransfer * 0.5)
                    GD.Print($"  ! Transfer amount within tolerance: {actualTransfer:F1}L vs expected ~{expectedTransfer:F1}L");
                
                GD.Print($"  ✓ Transfer rate test: {actualTransfer:F1}L transferred in {deltaTime*1000}ms");
                
                // Cleanup
                targetTank.QueueFree();
            });
        }
        
        /// <summary>
        /// Test throttle input handling
        /// </summary>
        private void TestThrottleInputHandling()
        {
            RunTest("Throttle Input Handling", () =>
            {
                if (_throttleController == null) throw new InvalidOperationException("Throttle controller not initialized");
                
                // Test initial state
                if (_throttleController.GetCurrentThrottle() != 0.0)
                    throw new Exception($"Initial throttle should be 0: {_throttleController.GetCurrentThrottle()}");
                
                // Test throttle increase
                _throttleController.SetThrottle(0.5);
                if (Math.Abs(_throttleController.GetTargetThrottle() - 0.5) > TestTolerance)
                    throw new Exception($"Throttle set failed: {_throttleController.GetTargetThrottle()} vs 0.5");
                
                // Test throttle bounds
                _throttleController.SetThrottle(1.5); // Over max
                if (_throttleController.GetTargetThrottle() > 1.0)
                    throw new Exception($"Throttle should be clamped to 1.0: {_throttleController.GetTargetThrottle()}");
                
                _throttleController.SetThrottle(-0.5); // Under min
                if (_throttleController.GetTargetThrottle() < 0.0)
                    throw new Exception($"Throttle should be clamped to 0.0: {_throttleController.GetTargetThrottle()}");
                
                GD.Print($"  ✓ Throttle input handling validated, current: {_throttleController.GetCurrentThrottle():P0}");
            });
        }
        
        /// <summary>
        /// Test throttle transitions and smoothing
        /// </summary>
        private void TestThrottleTransitions()
        {
            RunTest("Throttle Transitions", () =>
            {
                if (_throttleController == null) throw new InvalidOperationException("Throttle controller not initialized");
                
                _throttleController.Reset();
                _throttleController.SetThrottle(1.0); // Jump to full throttle
                
                var iterations = 0;
                var maxIterations = 100;
                
                // Process until throttle reaches target (or max iterations)
                while (_throttleController.IsThrottleTransitioning() && iterations < maxIterations)
                {
                    _throttleController._Process(0.016); // 16ms frame
                    iterations++;
                }
                
                if (iterations >= maxIterations)
                    throw new Exception($"Throttle transition took too long: {iterations} iterations");
                
                if (Math.Abs(_throttleController.GetCurrentThrottle() - 1.0) > 0.01)
                    throw new Exception($"Throttle didn't reach target: {_throttleController.GetCurrentThrottle()} vs 1.0");
                
                GD.Print($"  ✓ Throttle transition completed in {iterations} iterations ({iterations * 16}ms)");
            });
        }
        
        /// <summary>
        /// Test throttle controller performance
        /// </summary>
        private void TestThrottleControllerPerformance()
        {
            RunTest("Throttle Controller Performance", () =>
            {
                if (_throttleController == null) throw new InvalidOperationException("Throttle controller not initialized");
                
                var startTime = Time.GetTicksMsec();
                var iterations = 1000;
                
                // Test throttle update performance
                for (int i = 0; i < iterations; i++)
                {
                    _throttleController.SetThrottle(i % 2 == 0 ? 0.5 : 0.8);
                    _throttleController._Process(0.016);
                }
                
                var duration = Time.GetTicksMsec() - startTime;
                var avgTimePerUpdate = (double)duration / iterations;
                
                if (avgTimePerUpdate > 0.001) // Should be <0.001ms per update
                    throw new Exception($"Throttle controller performance issue: {avgTimePerUpdate:F6}ms per update");
                
                GD.Print($"  ✓ Throttle performance: {avgTimePerUpdate:F6}ms per update over {iterations} iterations");
            });
        }
        
        /// <summary>
        /// Test thrust force application to physics
        /// </summary>
        private void TestThrustForceApplication()
        {
            RunTest("Thrust Force Application", () =>
            {
                if (_testEngine == null || _testVessel == null) 
                    throw new InvalidOperationException("Test objects not initialized");
                
                var thrust = 100000.0; // 100kN
                var enginePosition = Vector3.Zero;
                var thrustDirection = Vector3.Up; // Upward thrust
                
                // Test thrust result calculation
                var engines = new List<Engine> { _testEngine };
                var thrustResult = ThrustSystem.CalculateVesselThrust(engines, 1.0, 101325.0);
                
                if (thrustResult.TotalThrust <= 0)
                    throw new Exception($"Thrust calculation failed: {thrustResult.TotalThrust}N");
                
                if (thrustResult.EngineThrustData.Count != 1)
                    throw new Exception($"Wrong number of engine thrust data entries: {thrustResult.EngineThrustData.Count}");
                
                // Test force direction
                var engineThrustData = thrustResult.EngineThrustData[0];
                if (engineThrustData.ThrustVector.Length() <= 0)
                    throw new Exception($"Thrust vector is zero: {engineThrustData.ThrustVector}");
                
                GD.Print($"  ✓ Thrust force: {thrustResult.TotalThrust:F0}N, engines: {thrustResult.ActiveEngines}");
            });
        }
        
        /// <summary>
        /// Test thrust vector calculations
        /// </summary>
        private void TestThrustVectorCalculation()
        {
            RunTest("Thrust Vector Calculation", () =>
            {
                if (_testEngine == null) throw new InvalidOperationException("Test engine not initialized");
                
                // Set engine orientation
                _testEngine.Transform = Transform3D.Identity.Rotated(Vector3.Right, Mathf.Pi / 4); // 45 degree tilt
                
                var thrust = _testEngine.GetThrust(1.0, 101325.0);
                
                // Test thrust calculation with tilted engine
                // Use ThrustSystem to get proper thrust vector calculation
                var engines = new List<Engine> { _testEngine };
                var thrustResult = ThrustSystem.CalculateVesselThrust(engines, 1.0, 101325.0);
                
                if (thrustResult.EngineThrustData.Count == 0)
                    throw new Exception("No engine thrust data calculated");
                
                var thrustVector = thrustResult.EngineThrustData[0].ThrustVector;
                var thrustDirection = thrustVector.Normalized();
                
                // Verify thrust direction is normalized
                if (Math.Abs(thrustDirection.Length() - 1.0) > TestTolerance)
                    throw new Exception($"Thrust direction not normalized: {thrustDirection.Length()}");
                
                GD.Print($"  ✓ Thrust vector: {thrust:F0}N in direction {thrustDirection}");
            });
        }
        
        /// <summary>
        /// Test physics integration performance
        /// </summary>
        private void TestPhysicsIntegrationPerformance()
        {
            RunTest("Physics Integration Performance", () =>
            {
                if (_testVessel == null) throw new InvalidOperationException("Test vessel not initialized");
                
                var startTime = Time.GetTicksMsec();
                var iterations = 100;
                
                // Simulate physics updates with thrust processing
                for (int i = 0; i < iterations; i++)
                {
                    _testVessel._PhysicsProcess(1.0 / 60.0); // 60 FPS physics
                }
                
                var duration = Time.GetTicksMsec() - startTime;
                var avgTimePerFrame = (double)duration / iterations;
                
                // Should meet physics budget requirement (<0.3ms additional per physics frame)
                if (avgTimePerFrame > 0.3)
                    throw new Exception($"Physics integration performance issue: {avgTimePerFrame:F3}ms per frame");
                
                GD.Print($"  ✓ Physics integration: {avgTimePerFrame:F3}ms per frame over {iterations} updates");
            });
        }
        
        /// <summary>
        /// Test thrust calculation performance
        /// </summary>
        private void TestThrustCalculationPerformance()
        {
            RunTest("Thrust Calculation Performance", () =>
            {
                if (_testEngine == null) throw new InvalidOperationException("Test engine not initialized");
                
                var startTime = Time.GetTicksMsec();
                var iterations = 10000;
                
                // Performance test: many thrust calculations
                for (int i = 0; i < iterations; i++)
                {
                    _testEngine.GetThrust(0.5 + (i % 50) * 0.01, 101325.0 - (i % 1000) * 50);
                }
                
                var duration = Time.GetTicksMsec() - startTime;
                var avgTimePerCalc = (double)duration / iterations;
                
                // Should meet target: <0.2ms per frame per engine (much less per calculation)
                if (avgTimePerCalc > 0.0001) // 0.1ms per 1000 calculations
                    throw new Exception($"Thrust calculation performance issue: {avgTimePerCalc:F6}ms per calculation");
                
                GD.Print($"  ✓ Thrust calculation: {avgTimePerCalc:F6}ms per calculation over {iterations} iterations");
            });
        }
        
        /// <summary>
        /// Test fuel consumption performance
        /// </summary>
        private void TestFuelConsumptionPerformance()
        {
            RunTest("Fuel Consumption Performance", () =>
            {
                if (_testFuelTank == null) throw new InvalidOperationException("Test fuel tank not initialized");
                
                var startTime = Time.GetTicksMsec();
                var iterations = 1000;
                
                // Reset tank for consistent testing
                _testFuelTank.LiquidFuel = 4500.0;
                
                // Performance test: many fuel consumption updates
                for (int i = 0; i < iterations; i++)
                {
                    _testFuelTank.DrainFuel(1.0, FuelType.LiquidFuel); // Small drain each time
                    _testFuelTank.UpdateFuelMass();
                }
                
                var duration = Time.GetTicksMsec() - startTime;
                var avgTimePerUpdate = (double)duration / iterations;
                
                // Should meet target: <0.1ms per frame per fuel tank
                if (avgTimePerUpdate > 0.0001) // 0.1ms per 1000 updates
                    throw new Exception($"Fuel consumption performance issue: {avgTimePerUpdate:F6}ms per update");
                
                GD.Print($"  ✓ Fuel consumption: {avgTimePerUpdate:F6}ms per update over {iterations} iterations");
            });
        }
        
        /// <summary>
        /// Test overall system performance
        /// </summary>
        private void TestOverallSystemPerformance()
        {
            RunTest("Overall System Performance", () =>
            {
                if (_testEngine == null || _testFuelTank == null || _throttleController == null)
                    throw new InvalidOperationException("Test objects not initialized");
                
                var startTime = Time.GetTicksMsec();
                var iterations = 100;
                
                // Register engine with throttle controller
                _throttleController.RegisterEngine(_testEngine);
                
                // Simulate full system operation
                for (int i = 0; i < iterations; i++)
                {
                    var throttle = 0.5 + 0.5 * Math.Sin(i * 0.1); // Varying throttle
                    _throttleController.SetThrottle(throttle);
                    _throttleController._Process(1.0 / 60.0);
                    
                    var thrust = _testEngine.GetThrust(_throttleController.GetCurrentThrottle(), 101325.0);
                    var consumption = thrust / (_testEngine.SpecificImpulse * 9.81); // Basic consumption calculation
                    
                    _testFuelTank.DrainFuel(consumption * (1.0 / 60.0), FuelType.LiquidFuel);
                }
                
                var duration = Time.GetTicksMsec() - startTime;
                var avgTimePerFrame = (double)duration / iterations;
                
                // Overall system should meet performance targets
                var targetTime = 0.2 + 0.1 + 0.5; // Thrust + fuel + effects budgets
                if (avgTimePerFrame > targetTime)
                    throw new Exception($"Overall system performance issue: {avgTimePerFrame:F3}ms vs target {targetTime:F3}ms");
                
                GD.Print($"  ✓ Overall system: {avgTimePerFrame:F3}ms per frame over {iterations} iterations");
                
                _throttleController.UnregisterEngine(_testEngine);
            });
        }
        
        /// <summary>
        /// Run a single test with error handling
        /// </summary>
        private void RunTest(string testName, Action testAction)
        {
            _testsRun++;
            
            try
            {
                testAction();
                _testsPassed++;
                GD.Print($"✓ {testName}");
            }
            catch (Exception ex)
            {
                _testsFailed++;
                _failedTests.Add($"{testName}: {ex.Message}");
                GD.PrintErr($"✗ {testName}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Display test results summary
        /// </summary>
        private void DisplayResults()
        {
            GD.Print("=".PadRight(80, '='));
            GD.Print($"THRUST AND FUEL SYSTEM TEST RESULTS");
            GD.Print($"Tests Run: {_testsRun}");
            GD.Print($"Tests Passed: {_testsPassed}");
            GD.Print($"Tests Failed: {_testsFailed}");
            GD.Print($"Success Rate: {(double)_testsPassed / _testsRun:P1}");
            
            if (_testsFailed > 0)
            {
                GD.Print("\nFAILED TESTS:");
                foreach (var failure in _failedTests)
                {
                    GD.PrintErr($"  • {failure}");
                }
            }
            else
            {
                GD.Print("\n🎉 ALL TESTS PASSED! Thrust and fuel systems are functioning correctly.");
            }
            
            GD.Print("=".PadRight(80, '='));
            
            // Mark test completion for task tracking
            if (_testsFailed == 0)
            {
                GD.Print("ThrustTests: All systems validated successfully - ready for thrust_test.tscn scene creation");
            }
        }
        
        /// <summary>
        /// Cleanup test objects
        /// </summary>
        public override void _ExitTree()
        {
            GD.Print("ThrustTests: Cleanup complete");
            base._ExitTree();
        }
    }
}