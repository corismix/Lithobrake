using System;
using Godot;

namespace Lithobrake.Core.Orbital
{
    /// <summary>
    /// Comprehensive unit tests for Kepler solver validation.
    /// Tests elliptical, hyperbolic, circular, and parabolic orbit cases.
    /// Validates energy conservation and performance requirements.
    /// </summary>
    public partial class KeplerTests : Node
    {
        private const double TOLERANCE = 1e-10;
        private const double ENERGY_TOLERANCE = 1e-12;
        private const double PERFORMANCE_TARGET_MS = 0.05;
        
        public override void _Ready()
        {
            GD.Print("=== Kepler Solver Unit Tests ===");
            
            bool allTestsPassed = true;
            
            allTestsPassed &= TestEllipticalOrbits();
            allTestsPassed &= TestHyperbolicOrbits();
            allTestsPassed &= TestCircularOrbitEdgeCase();
            allTestsPassed &= TestParabolicOrbitEdgeCase();
            allTestsPassed &= TestHighEccentricityOrbits();
            allTestsPassed &= TestPositionVelocityCalculations();
            allTestsPassed &= TestEnergyConservation();
            allTestsPassed &= TestTrajectorySpanSampling();
            allTestsPassed &= TestPerformanceBenchmarks();
            
            GD.Print($"=== Kepler Tests Complete: {(allTestsPassed ? "PASSED" : "FAILED")} ===");
        }
        
        /// <summary>
        /// Test elliptical orbit solver with various eccentricities (0.1 to 0.99)
        /// </summary>
        private bool TestEllipticalOrbits()
        {
            GD.Print("Testing elliptical orbit solver...");
            bool passed = true;
            
            double[] eccentricities = { 0.1, 0.3, 0.5, 0.7, 0.85, 0.95, 0.99 };
            double[] meanAnomalies = { 0.0, Math.PI / 4, Math.PI / 2, Math.PI, 3 * Math.PI / 2, 2 * Math.PI - 0.1 };
            
            foreach (double e in eccentricities)
            {
                foreach (double M in meanAnomalies)
                {
                    try
                    {
                        double E = Kepler.SolveElliptic(M, e);
                        
                        // Verify Kepler equation: E - e*sin(E) = M
                        double keplerError = E - e * Math.Sin(E) - M;
                        
                        if (Math.Abs(keplerError) > TOLERANCE)
                        {
                            GD.PrintErr($"Elliptical solver failed: e={e:F3}, M={M:F3}, error={keplerError:E}");
                            passed = false;
                        }
                        
                        // Verify eccentric anomaly is reasonable
                        if (E < 0 || E > 2 * Math.PI)
                        {
                            GD.PrintErr($"Elliptical solver produced invalid E: e={e:F3}, M={M:F3}, E={E:F3}");
                            passed = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        GD.PrintErr($"Elliptical solver exception: e={e:F3}, M={M:F3}, {ex.Message}");
                        passed = false;
                    }
                }
            }
            
            GD.Print($"Elliptical orbit tests: {(passed ? "PASSED" : "FAILED")}");
            return passed;
        }
        
        /// <summary>
        /// Test hyperbolic orbit solver with eccentricities from 1.01 to 5.0
        /// </summary>
        private bool TestHyperbolicOrbits()
        {
            GD.Print("Testing hyperbolic orbit solver...");
            bool passed = true;
            
            double[] eccentricities = { 1.01, 1.1, 1.5, 2.0, 3.0, 5.0 };
            double[] meanAnomalies = { -2.0, -1.0, -0.1, 0.0, 0.1, 1.0, 2.0 };
            
            foreach (double e in eccentricities)
            {
                foreach (double M in meanAnomalies)
                {
                    try
                    {
                        double H = Kepler.SolveHyperbolic(M, e);
                        
                        // Verify hyperbolic Kepler equation: e*sinh(H) - H = M
                        double keplerError = e * Math.Sinh(H) - H - M;
                        
                        if (Math.Abs(keplerError) > TOLERANCE)
                        {
                            GD.PrintErr($"Hyperbolic solver failed: e={e:F3}, M={M:F3}, error={keplerError:E}");
                            passed = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        GD.PrintErr($"Hyperbolic solver exception: e={e:F3}, M={M:F3}, {ex.Message}");
                        passed = false;
                    }
                }
            }
            
            GD.Print($"Hyperbolic orbit tests: {(passed ? "PASSED" : "FAILED")}");
            return passed;
        }
        
        /// <summary>
        /// Test circular orbit edge case (e ≈ 0)
        /// </summary>
        private bool TestCircularOrbitEdgeCase()
        {
            GD.Print("Testing circular orbit edge case...");
            bool passed = true;
            
            double[] meanAnomalies = { 0.0, Math.PI / 2, Math.PI, 3 * Math.PI / 2 };
            double verySmallE = 1e-16;
            
            foreach (double M in meanAnomalies)
            {
                try
                {
                    double E = Kepler.SolveElliptic(M, verySmallE);
                    
                    // For circular orbits, E should approximately equal M
                    double error = Math.Abs(E - M);
                    
                    if (error > 1e-10)
                    {
                        GD.PrintErr($"Circular orbit test failed: M={M:F3}, E={E:F3}, error={error:E}");
                        passed = false;
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"Circular orbit exception: M={M:F3}, {ex.Message}");
                    passed = false;
                }
            }
            
            GD.Print($"Circular orbit edge case: {(passed ? "PASSED" : "FAILED")}");
            return passed;
        }
        
        /// <summary>
        /// Test parabolic orbit edge case (e ≈ 1)
        /// </summary>
        private bool TestParabolicOrbitEdgeCase()
        {
            GD.Print("Testing parabolic orbit edge case...");
            bool passed = true;
            
            double[] meanAnomalies = { -1.0, -0.5, 0.0, 0.5, 1.0 };
            double parabolicE = 1.0;
            
            foreach (double M in meanAnomalies)
            {
                try
                {
                    double D = Kepler.SolveHyperbolic(M, parabolicE);
                    
                    // For parabolic orbits, verify Barker's equation: D + D³/3 = M
                    double barkerError = D + (D * D * D) / 3.0 - M;
                    
                    if (Math.Abs(barkerError) > TOLERANCE)
                    {
                        GD.PrintErr($"Parabolic orbit test failed: M={M:F3}, D={D:F3}, error={barkerError:E}");
                        passed = false;
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"Parabolic orbit exception: M={M:F3}, {ex.Message}");
                    passed = false;
                }
            }
            
            GD.Print($"Parabolic orbit edge case: {(passed ? "PASSED" : "FAILED")}");
            return passed;
        }
        
        /// <summary>
        /// Test high eccentricity ellipses near unity
        /// </summary>
        private bool TestHighEccentricityOrbits()
        {
            GD.Print("Testing high eccentricity orbits...");
            bool passed = true;
            
            double[] highEccentricities = { 0.99, 0.995, 0.999, 0.9999 };
            double[] meanAnomalies = { 0.1, Math.PI / 6, Math.PI / 3, Math.PI / 2 };
            
            foreach (double e in highEccentricities)
            {
                foreach (double M in meanAnomalies)
                {
                    try
                    {
                        double E = Kepler.SolveElliptic(M, e);
                        
                        // Verify Kepler equation
                        double keplerError = E - e * Math.Sin(E) - M;
                        
                        if (Math.Abs(keplerError) > TOLERANCE)
                        {
                            GD.PrintErr($"High ecc. test failed: e={e:F6}, M={M:F3}, error={keplerError:E}");
                            passed = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        GD.PrintErr($"High ecc. exception: e={e:F6}, M={M:F3}, {ex.Message}");
                        passed = false;
                    }
                }
            }
            
            GD.Print($"High eccentricity tests: {(passed ? "PASSED" : "FAILED")}");
            return passed;
        }
        
        /// <summary>
        /// Test position and velocity calculation methods
        /// </summary>
        private bool TestPositionVelocityCalculations()
        {
            GD.Print("Testing position and velocity calculations...");
            bool passed = true;
            
            // Create test orbital state (100km circular orbit around Kerbin)
            double altitude = 100000.0; // 100 km
            double radius = UNIVERSE_CONSTANTS.KERBIN_RADIUS + altitude;
            double semiMajorAxis = radius;
            double eccentricity = 0.0; // Circular
            
            var state = new OrbitalState(
                semiMajorAxis, eccentricity, 0.0, 0.0, 0.0, 0.0, 
                0.0, UNIVERSE_CONSTANTS.KERBIN_GRAVITATIONAL_PARAMETER
            );
            
            try
            {
                // Test position calculation
                Double3 position = Kepler.GetPositionAtTime(state, 0.0);
                double calculatedRadius = position.Length;
                
                if (Math.Abs(calculatedRadius - radius) > 1.0) // 1 meter tolerance
                {
                    GD.PrintErr($"Position test failed: expected radius {radius:F1}, got {calculatedRadius:F1}");
                    passed = false;
                }
                
                // Test velocity calculation
                Double3 velocity = Kepler.GetVelocityAtTime(state, 0.0);
                double calculatedSpeed = velocity.Length;
                double expectedSpeed = UNIVERSE_CONSTANTS.CircularVelocity(radius);
                
                if (Math.Abs(calculatedSpeed - expectedSpeed) > 1.0) // 1 m/s tolerance
                {
                    GD.PrintErr($"Velocity test failed: expected speed {expectedSpeed:F1}, got {calculatedSpeed:F1}");
                    passed = false;
                }
                
                // Test orthogonality (position and velocity should be perpendicular for circular orbit)
                double dotProduct = Double3.Dot(position.Normalized, velocity.Normalized);
                
                if (Math.Abs(dotProduct) > 1e-10)
                {
                    GD.PrintErr($"Orthogonality test failed: dot product = {dotProduct:E}");
                    passed = false;
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Position/velocity calculation exception: {ex.Message}");
                passed = false;
            }
            
            GD.Print($"Position/velocity tests: {(passed ? "PASSED" : "FAILED")}");
            return passed;
        }
        
        /// <summary>
        /// Test energy conservation over extended propagation periods
        /// </summary>
        private bool TestEnergyConservation()
        {
            GD.Print("Testing energy conservation...");
            bool passed = true;
            
            // Test various orbit types
            var testOrbits = new[]
            {
                new { name = "100km circular", a = 700000.0, e = 0.0 },
                new { name = "200x400km elliptical", a = 900000.0, e = 0.11 },
                new { name = "High eccentricity", a = 1000000.0, e = 0.8 },
                new { name = "Hyperbolic", a = -500000.0, e = 1.5 }
            };
            
            foreach (var orbit in testOrbits)
            {
                var state = new OrbitalState(
                    orbit.a, orbit.e, 0.0, 0.0, 0.0, 0.0,
                    0.0, UNIVERSE_CONSTANTS.KERBIN_GRAVITATIONAL_PARAMETER
                );
                
                try
                {
                    // Test energy conservation over multiple orbital periods
                    double testTime = orbit.e < 1.0 ? state.OrbitalPeriod * 10 : 3600.0; // 10 periods or 1 hour
                    
                    bool energyConserved = Kepler.ValidateEnergyConservation(state, testTime, ENERGY_TOLERANCE);
                    
                    if (!energyConserved)
                    {
                        GD.PrintErr($"Energy conservation failed for {orbit.name}");
                        passed = false;
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"Energy conservation exception for {orbit.name}: {ex.Message}");
                    passed = false;
                }
            }
            
            GD.Print($"Energy conservation tests: {(passed ? "PASSED" : "FAILED")}");
            return passed;
        }
        
        /// <summary>
        /// Test trajectory sampling system
        /// </summary>
        private bool TestTrajectorySpanSampling()
        {
            GD.Print("Testing trajectory sampling...");
            bool passed = true;
            
            try
            {
                // Test elliptical orbit sampling
                var ellipticalState = new OrbitalState(
                    800000.0, 0.3, 0.0, 0.0, 0.0, 0.0,
                    0.0, UNIVERSE_CONSTANTS.KERBIN_GRAVITATIONAL_PARAMETER
                );
                
                Double3[] ellipticalTrajectory = Kepler.SampleTrajectory(ellipticalState, 100, true);
                
                if (ellipticalTrajectory.Length != 100)
                {
                    GD.PrintErr($"Elliptical trajectory sampling failed: expected 100 points, got {ellipticalTrajectory.Length}");
                    passed = false;
                }
                
                // Test hyperbolic orbit sampling
                var hyperbolicState = new OrbitalState(
                    -500000.0, 1.5, 0.0, 0.0, 0.0, 0.0,
                    0.0, UNIVERSE_CONSTANTS.KERBIN_GRAVITATIONAL_PARAMETER
                );
                
                Double3[] hyperbolicTrajectory = Kepler.SampleTrajectory(hyperbolicState, 50, true);
                
                if (hyperbolicTrajectory.Length != 50)
                {
                    GD.PrintErr($"Hyperbolic trajectory sampling failed: expected 50 points, got {hyperbolicTrajectory.Length}");
                    passed = false;
                }
                
                // Verify no NaN or infinite values in trajectories
                foreach (var point in ellipticalTrajectory)
                {
                    if (!UNIVERSE_CONSTANTS.IsValidOrbitalValue(point.X) || 
                        !UNIVERSE_CONSTANTS.IsValidOrbitalValue(point.Y) || 
                        !UNIVERSE_CONSTANTS.IsValidOrbitalValue(point.Z))
                    {
                        GD.PrintErr($"Invalid trajectory point detected in elliptical orbit");
                        passed = false;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Trajectory sampling exception: {ex.Message}");
                passed = false;
            }
            
            GD.Print($"Trajectory sampling tests: {(passed ? "PASSED" : "FAILED")}");
            return passed;
        }
        
        /// <summary>
        /// Test performance benchmarks to ensure <0.05ms per solve operation
        /// </summary>
        private bool TestPerformanceBenchmarks()
        {
            GD.Print("Testing performance benchmarks...");
            bool passed = true;
            
            try
            {
                // Benchmark elliptical solver
                double ellipticalTime = Kepler.BenchmarkSolver(1000);
                
                GD.Print($"Elliptical solver performance: {ellipticalTime:F6} ms/operation");
                
                if (ellipticalTime > PERFORMANCE_TARGET_MS)
                {
                    GD.PrintErr($"Performance target missed: {ellipticalTime:F6} ms > {PERFORMANCE_TARGET_MS:F6} ms");
                    passed = false;
                }
                
                // Benchmark position/velocity calculations
                var state = new OrbitalState(
                    700000.0, 0.2, 0.0, 0.0, 0.0, 0.0,
                    0.0, UNIVERSE_CONSTANTS.KERBIN_GRAVITATIONAL_PARAMETER
                );
                
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                for (int i = 0; i < 1000; i++)
                {
                    Kepler.GetPositionAtTime(state, i * 10.0);
                    Kepler.GetVelocityAtTime(state, i * 10.0);
                }
                
                stopwatch.Stop();
                double posVelTime = stopwatch.Elapsed.TotalMilliseconds / 2000.0; // 2000 operations
                
                GD.Print($"Position/velocity performance: {posVelTime:F6} ms/operation");
                
                if (posVelTime > 0.1) // 0.1ms target for position/velocity
                {
                    GD.PrintErr($"Position/velocity performance target missed: {posVelTime:F6} ms > 0.1 ms");
                    passed = false;
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Performance benchmark exception: {ex.Message}");
                passed = false;
            }
            
            GD.Print($"Performance benchmarks: {(passed ? "PASSED" : "FAILED")}");
            return passed;
        }
    }
}