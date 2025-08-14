using Godot;
using System;

namespace Lithobrake.Core
{
    /// <summary>
    /// Orbital mechanics system testing and validation.
    /// Tests 100km circular orbit stability over 10 complete orbits with <1% drift.
    /// Validates coordinate transformations and orbital state calculations.
    /// </summary>
    public partial class OrbitalTest : Node3D
    {
        private CelestialBody? _kerbin;
        private PhysicsVessel? _testVessel;
        private PhysicsManager? _physicsManager;
        private PerformanceMonitor? _performanceMonitor;
        
        // Test parameters from current-task.md
        private const double TestAltitude = 100_000.0; // 100km test altitude
        private const double TestInclination = 0.0; // Equatorial orbit
        private const double MaxAllowedDrift = 0.01; // 1% maximum drift
        private const int OrbitTestCount = 10; // Test 10 complete orbits
        
        // Test state tracking
        private OrbitalState _initialOrbitalState;
        private double _testStartTime;
        private int _completedOrbits = 0;
        private double _maxObservedDrift = 0.0;
        private bool _testRunning = false;
        private bool _testPassed = false;
        
        // Performance tracking
        private double _maxOrbitalCalcTime = 0.0;
        private double _maxCoordinateConversionTime = 0.0;
        private double _totalOrbitalTime = 0.0;
        private int _orbitalCalculations = 0;
        
        public override void _Ready()
        {
            GD.Print("OrbitalTest: Starting orbital mechanics validation tests");
            
            // Initialize systems
            _kerbin = CelestialBody.CreateKerbin();
            _performanceMonitor = PerformanceMonitor.Instance;
            _physicsManager = GetNode<PhysicsManager>("/root/PhysicsManager");
            
            if (_physicsManager == null)
            {
                GD.PrintErr("OrbitalTest: PhysicsManager not found, creating one");
                _physicsManager = new PhysicsManager();
                AddChild(_physicsManager);
            }
            
            // Create test vessel
            CreateTestVessel();
            
            // Start orbital stability test
            StartOrbitalStabilityTest();
        }
        
        public override void _PhysicsProcess(double delta)
        {
            if (!_testRunning)
                return;
            
            UpdateOrbitalTest(delta);
        }
        
        /// <summary>
        /// Create a test vessel for orbital mechanics validation
        /// </summary>
        private void CreateTestVessel()
        {
            // Create PhysicsVessel
            _testVessel = new PhysicsVessel();
            AddChild(_testVessel);
            _testVessel.Initialize(1, _physicsManager);
            
            // Create a simple test part (RigidBody3D with CollisionShape3D)
            var rigidBody = new RigidBody3D();
            rigidBody.Name = "TestPart";
            rigidBody.Mass = 1000.0f; // 1 ton test mass
            
            // Add collision shape
            var collisionShape = new CollisionShape3D();
            var boxShape = new BoxShape3D();
            boxShape.Size = Vector3.One;
            collisionShape.Shape = boxShape;
            rigidBody.AddChild(collisionShape);
            
            AddChild(rigidBody);
            
            // Add part to vessel
            _testVessel.AddPart(rigidBody, 1000.0, Double3.Zero);
            
            GD.Print("OrbitalTest: Created test vessel with 1 ton mass");
        }
        
        /// <summary>
        /// Start the 10-orbit stability test
        /// </summary>
        private void StartOrbitalStabilityTest()
        {
            if (_testVessel == null || _kerbin == null)
            {
                GD.PrintErr("OrbitalTest: Cannot start test - vessel or kerbin not initialized");
                return;
            }
            
            // Set vessel to circular orbit at test altitude
            _testVessel.SetCircularOrbit(TestAltitude, TestInclination);
            
            // Record initial orbital state
            var (orbitalState, isValid, _) = _testVessel.GetOrbitalState();
            if (!isValid || !orbitalState.HasValue)
            {
                GD.PrintErr("OrbitalTest: Cannot start test - invalid orbital state");
                return;
            }
            
            _initialOrbitalState = orbitalState.Value;
            _testStartTime = Time.GetUnixTimeFromSystem();
            _testRunning = true;
            _completedOrbits = 0;
            _maxObservedDrift = 0.0;
            _maxOrbitalCalcTime = 0.0;
            _maxCoordinateConversionTime = 0.0;
            _totalOrbitalTime = 0.0;
            _orbitalCalculations = 0;
            
            GD.Print($"OrbitalTest: Started stability test");
            GD.Print($"Initial orbit: a={_initialOrbitalState.SemiMajorAxis/1000:F1}km, e={_initialOrbitalState.Eccentricity:F6}");
            GD.Print($"Period: {_initialOrbitalState.OrbitalPeriod:F1}s ({_initialOrbitalState.OrbitalPeriod/60:F1} minutes)");
        }
        
        /// <summary>
        /// Update orbital test progress and validation
        /// </summary>
        private void UpdateOrbitalTest(double delta)
        {
            if (_testVessel == null)
                return;
            
            double currentTime = Time.GetUnixTimeFromSystem();
            double elapsedTime = currentTime - _testStartTime;
            
            // Update orbital state from physics
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _testVessel.UpdateOrbitalStateFromPhysics(currentTime);
            stopwatch.Stop();
            
            double calcTime = stopwatch.Elapsed.TotalMilliseconds;
            _totalOrbitalTime += calcTime;
            _orbitalCalculations++;
            _maxOrbitalCalcTime = Math.Max(_maxOrbitalCalcTime, calcTime);
            
            // Get current orbital state
            var (currentState, isValid, conversionTime) = _testVessel.GetOrbitalState();
            if (!isValid || !currentState.HasValue)
            {
                GD.PrintErr("OrbitalTest: Invalid orbital state during test");
                StopTest(false);
                return;
            }
            
            _maxCoordinateConversionTime = Math.Max(_maxCoordinateConversionTime, conversionTime);
            
            // Calculate orbital drift
            double drift = CalculateOrbitalDrift(_initialOrbitalState, currentState.Value);
            _maxObservedDrift = Math.Max(_maxObservedDrift, drift);
            
            // Check if we've completed an orbit
            double expectedOrbits = elapsedTime / _initialOrbitalState.OrbitalPeriod;
            int currentOrbitCount = (int)Math.Floor(expectedOrbits);
            
            if (currentOrbitCount > _completedOrbits)
            {
                _completedOrbits = currentOrbitCount;
                GD.Print($"OrbitalTest: Completed orbit {_completedOrbits}/{OrbitTestCount}, drift: {drift*100:F3}%");
                
                if (drift > MaxAllowedDrift)
                {
                    GD.PrintErr($"OrbitalTest: FAILED - Orbital drift {drift*100:F3}% exceeds maximum {MaxAllowedDrift*100:F1}%");
                    StopTest(false);
                    return;
                }
            }
            
            // Check if we've completed all test orbits
            if (_completedOrbits >= OrbitTestCount)
            {
                GD.Print($"OrbitalTest: PASSED - Completed {OrbitTestCount} orbits with max drift {_maxObservedDrift*100:F3}%");
                StopTest(true);
                return;
            }
            
            // Check performance targets every second
            if ((int)elapsedTime % 60 == 0 && _orbitalCalculations > 0)
            {
                double avgOrbitalTime = _totalOrbitalTime / _orbitalCalculations;
                ReportPerformanceMetrics(avgOrbitalTime);
            }
        }
        
        /// <summary>
        /// Calculate orbital drift between two orbital states
        /// </summary>
        private double CalculateOrbitalDrift(OrbitalState initial, OrbitalState current)
        {
            // Calculate relative differences in orbital elements
            double aDrift = Math.Abs(current.SemiMajorAxis - initial.SemiMajorAxis) / initial.SemiMajorAxis;
            double eDrift = Math.Abs(current.Eccentricity - initial.Eccentricity);
            double iDrift = Math.Abs(current.Inclination - initial.Inclination);
            
            // Return maximum drift (most conservative measure)
            return Math.Max(aDrift, Math.Max(eDrift, iDrift));
        }
        
        /// <summary>
        /// Stop the orbital test and report results
        /// </summary>
        private void StopTest(bool passed)
        {
            _testRunning = false;
            _testPassed = passed;
            
            double avgOrbitalTime = _orbitalCalculations > 0 ? _totalOrbitalTime / _orbitalCalculations : 0;
            
            GD.Print("\n=== ORBITAL MECHANICS TEST RESULTS ===");
            GD.Print($"Test Status: {(passed ? "PASSED" : "FAILED")}");
            GD.Print($"Completed Orbits: {_completedOrbits}/{OrbitTestCount}");
            GD.Print($"Maximum Orbital Drift: {_maxObservedDrift*100:F3}% (limit: {MaxAllowedDrift*100:F1}%)");
            GD.Print($"Average Orbital Calc Time: {avgOrbitalTime:F3}ms (target: <0.1ms)");
            GD.Print($"Maximum Orbital Calc Time: {_maxOrbitalCalcTime:F3}ms");
            GD.Print($"Maximum Conversion Time: {_maxCoordinateConversionTime:F3}ms (target: <0.05ms)");
            GD.Print($"Total Orbital Calculations: {_orbitalCalculations}");
            
            // Performance validation
            bool perfPassed = avgOrbitalTime <= 0.1 && _maxCoordinateConversionTime <= 0.05;
            GD.Print($"Performance Test: {(perfPassed ? "PASSED" : "FAILED")}");
            
            if (passed && perfPassed)
            {
                GD.Print("✅ ALL ORBITAL MECHANICS TESTS PASSED");
            }
            else
            {
                GD.Print("❌ ORBITAL MECHANICS TESTS FAILED");
            }
            
            GD.Print("=====================================\n");
        }
        
        /// <summary>
        /// Report current performance metrics
        /// </summary>
        private void ReportPerformanceMetrics(double avgOrbitalTime)
        {
            var (altitude, velocity, period, inAtmosphere) = _testVessel.GetOrbitalMetrics();
            
            GD.Print($"Orbital Metrics - Alt: {altitude/1000:F1}km, Vel: {velocity:F1}m/s, Period: {period/60:F1}min");
            GD.Print($"Performance - Avg calc: {avgOrbitalTime:F3}ms, Max: {_maxOrbitalCalcTime:F3}ms");
        }
        
        /// <summary>
        /// Manual test trigger for coordinate transformations
        /// </summary>
        public void TestCoordinateTransformations()
        {
            GD.Print("Testing coordinate transformations...");
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Test various orbital scenarios
            for (int i = 0; i < 100; i++)
            {
                // Create test orbital states
                var circularOrbit = _kerbin.CreateCircularOrbit(100000 + i * 1000, i * 0.01);
                
                // Convert to Cartesian
                var (position, velocity) = circularOrbit.ToCartesian(0);
                
                // Convert back to orbital elements
                var reconstructed = OrbitalState.FromCartesian(position, velocity, 0, _kerbin.GravitationalParameter);
                
                // Check accuracy
                double error = Math.Abs(reconstructed.SemiMajorAxis - circularOrbit.SemiMajorAxis) / circularOrbit.SemiMajorAxis;
                if (error > 1e-10)
                {
                    GD.PrintErr($"Coordinate transformation error: {error:E3}");
                }
            }
            
            stopwatch.Stop();
            GD.Print($"Coordinate transformation test completed in {stopwatch.Elapsed.TotalMilliseconds:F3}ms");
        }
        
        /// <summary>
        /// Test various orbital eccentricities and inclinations
        /// </summary>
        public void TestOrbitalVariations()
        {
            GD.Print("Testing orbital variations...");
            
            var eccentricities = new[] { 0.0, 0.1, 0.3, 0.5, 0.7, 0.9 };
            var inclinations = new[] { 0.0, 30.0, 60.0, 90.0, 120.0, 180.0 };
            
            foreach (double e in eccentricities)
            {
                foreach (double i_deg in inclinations)
                {
                    double i_rad = i_deg * Math.PI / 180.0;
                    
                    // Create orbital state
                    var orbital = new OrbitalState(
                        semiMajorAxis: 700_000, // 700km
                        eccentricity: e,
                        inclination: i_rad,
                        longitudeOfAscendingNode: 0,
                        argumentOfPeriapsis: 0,
                        meanAnomaly: 0,
                        epoch: 0,
                        gravitationalParameter: _kerbin.GravitationalParameter
                    );
                    
                    if (orbital.IsValid())
                    {
                        var (pos, vel) = orbital.ToCartesian(0);
                        double altPeri = pos.Length - _kerbin.Radius;
                        GD.Print($"e={e:F1}, i={i_deg:F0}°: Alt={altPeri/1000:F1}km, Valid=✅");
                    }
                    else
                    {
                        GD.Print($"e={e:F1}, i={i_deg:F0}°: Invalid orbital state ❌");
                    }
                }
            }
            
            GD.Print("Orbital variations test completed");
        }
    }
}