using System;

namespace Lithobrake.Core.Orbital
{
    /// <summary>
    /// Centralized Kepler equation solver and orbital propagation system.
    /// Handles both elliptical (e<1) and hyperbolic (e>=1) orbits using Newton-Raphson methods.
    /// Provides high-precision position and velocity calculations with 1e-10 tolerance.
    /// </summary>
    public static class Kepler
    {
        // Solver constants
        private const double TOLERANCE = 1e-10;
        private const int MAX_ITERATIONS = 16;
        private const double PRECISION_THRESHOLD = 1e-15;
        
        /// <summary>
        /// Solve Kepler's equation for elliptical orbits (e < 1) using Newton-Raphson method.
        /// Equation: E - e*sin(E) = M
        /// </summary>
        /// <param name="meanAnomaly">Mean anomaly in radians</param>
        /// <param name="eccentricity">Orbital eccentricity (0 <= e < 1)</param>
        /// <param name="tolerance">Convergence tolerance (default 1e-10)</param>
        /// <param name="maxIterations">Maximum iterations (default 16)</param>
        /// <returns>Eccentric anomaly in radians</returns>
        public static double SolveElliptic(double meanAnomaly, double eccentricity, double tolerance = TOLERANCE, int maxIterations = MAX_ITERATIONS)
        {
            if (eccentricity >= 1.0)
                throw new ArgumentException("Eccentricity must be less than 1.0 for elliptical orbits", nameof(eccentricity));
            
            if (eccentricity < 0.0)
                throw new ArgumentException("Eccentricity must be non-negative", nameof(eccentricity));
            
            // Normalize mean anomaly to [0, 2π]
            double M = NormalizeMeanAnomaly(meanAnomaly);
            
            // Handle circular orbit edge case
            if (eccentricity < PRECISION_THRESHOLD)
                return M;
            
            // Initial guess using Kepler starter
            double E = GetEllipticStarter(M, eccentricity);
            
            // Newton-Raphson iteration
            for (int i = 0; i < maxIterations; i++)
            {
                double sinE = Math.Sin(E);
                double cosE = Math.Cos(E);
                
                double f = E - eccentricity * sinE - M;
                double df = 1.0 - eccentricity * cosE;
                
                // Check for derivative near zero
                if (Math.Abs(df) < PRECISION_THRESHOLD)
                    break;
                
                double deltaE = f / df;
                E -= deltaE;
                
                if (Math.Abs(deltaE) < tolerance)
                    return E;
            }
            
            return E;
        }
        
        /// <summary>
        /// Solve Kepler's equation for hyperbolic orbits (e >= 1) using Newton-Raphson method.
        /// Equation: e*sinh(H) - H = M
        /// </summary>
        /// <param name="meanAnomaly">Mean anomaly in radians</param>
        /// <param name="eccentricity">Orbital eccentricity (e >= 1)</param>
        /// <param name="tolerance">Convergence tolerance (default 1e-10)</param>
        /// <param name="maxIterations">Maximum iterations (default 16)</param>
        /// <returns>Hyperbolic eccentric anomaly</returns>
        public static double SolveHyperbolic(double meanAnomaly, double eccentricity, double tolerance = TOLERANCE, int maxIterations = MAX_ITERATIONS)
        {
            if (eccentricity < 1.0)
                throw new ArgumentException("Eccentricity must be greater than or equal to 1.0 for hyperbolic orbits", nameof(eccentricity));
            
            // Handle parabolic orbit edge case
            if (Math.Abs(eccentricity - 1.0) < PRECISION_THRESHOLD)
                return SolveParabolic(meanAnomaly);
            
            // Initial guess for hyperbolic eccentric anomaly
            double H = GetHyperbolicStarter(meanAnomaly, eccentricity);
            
            // Newton-Raphson iteration for hyperbolic case
            for (int i = 0; i < maxIterations; i++)
            {
                double sinhH = Math.Sinh(H);
                double coshH = Math.Cosh(H);
                
                double f = eccentricity * sinhH - H - meanAnomaly;
                double df = eccentricity * coshH - 1.0;
                
                // Check for derivative near zero
                if (Math.Abs(df) < PRECISION_THRESHOLD)
                    break;
                
                double deltaH = f / df;
                H -= deltaH;
                
                if (Math.Abs(deltaH) < tolerance)
                    return H;
            }
            
            return H;
        }
        
        /// <summary>
        /// Get position at specified time using Kepler solver and coordinate transformations.
        /// </summary>
        /// <param name="state">Orbital state containing Keplerian elements</param>
        /// <param name="time">Time in seconds since epoch</param>
        /// <returns>Position vector in inertial coordinates</returns>
        public static Double3 GetPositionAtTime(OrbitalState state, double time)
        {
            // Calculate mean anomaly at given time
            double meanAnomalyAtTime = state.MeanAnomaly + state.MeanMotion * (time - state.Epoch);
            
            Double3 position;
            
            if (state.Eccentricity < 1.0)
            {
                // Elliptical orbit
                double E = SolveElliptic(meanAnomalyAtTime, state.Eccentricity);
                double r = state.SemiMajorAxis * (1.0 - state.Eccentricity * Math.Cos(E));
                double cosNu = (Math.Cos(E) - state.Eccentricity) / (1.0 - state.Eccentricity * Math.Cos(E));
                double sinNu = Math.Sqrt(1.0 - state.Eccentricity * state.Eccentricity) * Math.Sin(E) / (1.0 - state.Eccentricity * Math.Cos(E));
                
                // Position in orbital plane
                position = new Double3(r * cosNu, r * sinNu, 0);
            }
            else
            {
                // Hyperbolic orbit
                double H = SolveHyperbolic(meanAnomalyAtTime, state.Eccentricity);
                double r = state.SemiMajorAxis * (state.Eccentricity * Math.Cosh(H) - 1.0);
                double cosNu = (state.Eccentricity - Math.Cosh(H)) / (state.Eccentricity * Math.Cosh(H) - 1.0);
                double sinNu = Math.Sqrt(state.Eccentricity * state.Eccentricity - 1.0) * Math.Sinh(H) / (state.Eccentricity * Math.Cosh(H) - 1.0);
                
                // Position in orbital plane
                position = new Double3(r * cosNu, r * sinNu, 0);
            }
            
            // Transform from orbital plane to inertial frame
            return TransformToInertial(position, state.Inclination, state.LongitudeOfAscendingNode, state.ArgumentOfPeriapsis);
        }
        
        /// <summary>
        /// Get velocity at specified time using vis-viva equation and coordinate transformations.
        /// </summary>
        /// <param name="state">Orbital state containing Keplerian elements</param>
        /// <param name="time">Time in seconds since epoch</param>
        /// <returns>Velocity vector in inertial coordinates</returns>
        public static Double3 GetVelocityAtTime(OrbitalState state, double time)
        {
            // Calculate mean anomaly at given time
            double meanAnomalyAtTime = state.MeanAnomaly + state.MeanMotion * (time - state.Epoch);
            
            Double3 velocity;
            double r, trueAnomaly;
            
            if (state.Eccentricity < 1.0)
            {
                // Elliptical orbit
                double E = SolveElliptic(meanAnomalyAtTime, state.Eccentricity);
                r = state.SemiMajorAxis * (1.0 - state.Eccentricity * Math.Cos(E));
                trueAnomaly = 2.0 * Math.Atan2(Math.Sqrt(1.0 + state.Eccentricity) * Math.Sin(E / 2.0), 
                                               Math.Sqrt(1.0 - state.Eccentricity) * Math.Cos(E / 2.0));
            }
            else
            {
                // Hyperbolic orbit  
                double H = SolveHyperbolic(meanAnomalyAtTime, state.Eccentricity);
                r = state.SemiMajorAxis * (state.Eccentricity * Math.Cosh(H) - 1.0);
                trueAnomaly = 2.0 * Math.Atan2(Math.Sqrt(state.Eccentricity + 1.0) * Math.Sinh(H / 2.0),
                                               Math.Sqrt(state.Eccentricity - 1.0) * Math.Cosh(H / 2.0));
            }
            
            // Calculate velocity magnitude using vis-viva equation
            double v = Math.Sqrt(state.GravitationalParameter * (2.0 / r - 1.0 / Math.Abs(state.SemiMajorAxis)));
            
            // Calculate angular momentum
            double h = Math.Sqrt(state.GravitationalParameter * Math.Abs(state.SemiMajorAxis) * (1.0 - state.Eccentricity * state.Eccentricity));
            
            // Velocity components in orbital plane
            double vr = h * state.Eccentricity * Math.Sin(trueAnomaly) / (r * (1.0 + state.Eccentricity * Math.Cos(trueAnomaly)));
            double vt = h / r;
            
            // Velocity in orbital plane coordinates
            velocity = new Double3(vr * Math.Cos(trueAnomaly) - vt * Math.Sin(trueAnomaly),
                                   vr * Math.Sin(trueAnomaly) + vt * Math.Cos(trueAnomaly), 
                                   0);
            
            // Transform from orbital plane to inertial frame
            return TransformToInertial(velocity, state.Inclination, state.LongitudeOfAscendingNode, state.ArgumentOfPeriapsis);
        }
        
        /// <summary>
        /// Generate adaptive trajectory sampling for visualization.
        /// Dense points near periapsis/apoapsis, sparse elsewhere.
        /// </summary>
        /// <param name="state">Orbital state to sample</param>
        /// <param name="sampleCount">Total number of sample points</param>
        /// <param name="adaptiveDensity">Enable adaptive density based on true anomaly</param>
        /// <returns>Array of position points along the trajectory</returns>
        public static Double3[] SampleTrajectory(OrbitalState state, int sampleCount, bool adaptiveDensity = true)
        {
            if (sampleCount <= 0)
                throw new ArgumentException("Sample count must be positive", nameof(sampleCount));
            
            var points = new Double3[sampleCount];
            
            if (state.Eccentricity >= 1.0)
            {
                // Hyperbolic trajectory - sample limited arc
                return SampleHyperbolicTrajectory(state, sampleCount, adaptiveDensity);
            }
            
            // Elliptical trajectory - full orbit sampling
            for (int i = 0; i < sampleCount; i++)
            {
                double trueAnomaly;
                
                if (adaptiveDensity)
                {
                    // Adaptive sampling: more points near periapsis/apoapsis
                    double t = (double)i / (sampleCount - 1);
                    trueAnomaly = AdaptiveTrueAnomalyDistribution(t, state.Eccentricity);
                }
                else
                {
                    // Uniform sampling
                    trueAnomaly = 2.0 * Math.PI * i / sampleCount;
                }
                
                points[i] = GetPositionAtTrueAnomaly(state, trueAnomaly);
            }
            
            return points;
        }
        
        /// <summary>
        /// Validate energy conservation for orbital propagation accuracy.
        /// </summary>
        /// <param name="state">Initial orbital state</param>
        /// <param name="time">Time to propagate forward</param>
        /// <param name="relativeTolerance">Relative error tolerance</param>
        /// <returns>True if energy is conserved within tolerance</returns>
        public static bool ValidateEnergyConservation(OrbitalState state, double time, double relativeTolerance = 1e-12)
        {
            // Calculate initial energy
            var (pos0, vel0) = state.ToCartesian(state.Epoch);
            double initialEnergy = Double3.SpecificOrbitalEnergy(pos0, vel0, state.GravitationalParameter);
            
            // Calculate energy at propagated time
            var (pos1, vel1) = state.ToCartesian(state.Epoch + time);
            double finalEnergy = Double3.SpecificOrbitalEnergy(pos1, vel1, state.GravitationalParameter);
            
            // Check relative error
            double relativeError = Math.Abs((finalEnergy - initialEnergy) / initialEnergy);
            return relativeError < relativeTolerance;
        }
        
        /// <summary>
        /// Benchmark Kepler solver performance for validation.
        /// </summary>
        /// <param name="iterations">Number of iterations to test</param>
        /// <returns>Average solve time in milliseconds</returns>
        public static double BenchmarkSolver(int iterations = 1000)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var random = new Random(42);
            
            for (int i = 0; i < iterations; i++)
            {
                double meanAnomaly = random.NextDouble() * 2.0 * Math.PI;
                double eccentricity = random.NextDouble() * 0.95; // Elliptical
                
                SolveElliptic(meanAnomaly, eccentricity);
            }
            
            stopwatch.Stop();
            return stopwatch.Elapsed.TotalMilliseconds / iterations;
        }
        
        #region Private Helper Methods
        
        /// <summary>
        /// Normalize mean anomaly to [0, 2π] range
        /// </summary>
        private static double NormalizeMeanAnomaly(double meanAnomaly)
        {
            double normalized = meanAnomaly % (2.0 * Math.PI);
            return normalized < 0 ? normalized + 2.0 * Math.PI : normalized;
        }
        
        /// <summary>
        /// Get improved initial guess for elliptical Kepler equation
        /// </summary>
        private static double GetEllipticStarter(double meanAnomaly, double eccentricity)
        {
            // Improved starter for better convergence
            if (eccentricity < 0.8)
            {
                return meanAnomaly + eccentricity * Math.Sin(meanAnomaly);
            }
            else
            {
                // High eccentricity starter
                double x = (6.0 * meanAnomaly) / Math.PI;
                return meanAnomaly + eccentricity * (3.0 * x / (2.0 + Math.Sqrt(1.0 + 9.0 * x * x)));
            }
        }
        
        /// <summary>
        /// Get initial guess for hyperbolic Kepler equation
        /// </summary>
        private static double GetHyperbolicStarter(double meanAnomaly, double eccentricity)
        {
            // Initial guess for hyperbolic case
            return Math.Sign(meanAnomaly) * Math.Log(2.0 * Math.Abs(meanAnomaly) / eccentricity + 1.8);
        }
        
        /// <summary>
        /// Solve parabolic trajectory (e ≈ 1)
        /// </summary>
        private static double SolveParabolic(double meanAnomaly)
        {
            // Barker's equation for parabolic orbits: D + D³/3 = M
            // Using Halley's method for cubic convergence
            double D = Math.Cbrt(3.0 * meanAnomaly); // Initial guess
            
            for (int i = 0; i < MAX_ITERATIONS; i++)
            {
                double D2 = D * D;
                double f = D + D2 * D / 3.0 - meanAnomaly;
                double df = 1.0 + D2;
                double d2f = 2.0 * D;
                
                double deltaD = f / (df - f * d2f / (2.0 * df));
                D -= deltaD;
                
                if (Math.Abs(deltaD) < TOLERANCE)
                    break;
            }
            
            return D;
        }
        
        /// <summary>
        /// Transform vector from orbital plane to inertial coordinates
        /// </summary>
        private static Double3 TransformToInertial(Double3 vector, double inclination, double longitudeOfAscendingNode, double argumentOfPeriapsis)
        {
            double cosO = Math.Cos(longitudeOfAscendingNode);
            double sinO = Math.Sin(longitudeOfAscendingNode);
            double cosI = Math.Cos(inclination);
            double sinI = Math.Sin(inclination);
            double cosW = Math.Cos(argumentOfPeriapsis);
            double sinW = Math.Sin(argumentOfPeriapsis);
            
            // Combined transformation matrix elements
            double P11 = cosO * cosW - sinO * sinW * cosI;
            double P12 = -cosO * sinW - sinO * cosW * cosI;
            double P21 = sinO * cosW + cosO * sinW * cosI;
            double P22 = -sinO * sinW + cosO * cosW * cosI;
            double P31 = sinW * sinI;
            double P32 = cosW * sinI;
            
            return new Double3(
                P11 * vector.X + P12 * vector.Y,
                P21 * vector.X + P22 * vector.Y,
                P31 * vector.X + P32 * vector.Y
            );
        }
        
        /// <summary>
        /// Sample hyperbolic trajectory with limited arc
        /// </summary>
        private static Double3[] SampleHyperbolicTrajectory(OrbitalState state, int sampleCount, bool adaptiveDensity)
        {
            var points = new Double3[sampleCount];
            
            // Limit true anomaly range for hyperbolic trajectories
            double maxTrueAnomaly = Math.Acos(-1.0 / state.Eccentricity); // Asymptote limit
            double trueAnomalyRange = 2.0 * maxTrueAnomaly * 0.95; // 95% of full range
            
            for (int i = 0; i < sampleCount; i++)
            {
                double t = (double)i / (sampleCount - 1);
                double trueAnomaly = -maxTrueAnomaly * 0.95 + trueAnomalyRange * t;
                points[i] = GetPositionAtTrueAnomaly(state, trueAnomaly);
            }
            
            return points;
        }
        
        /// <summary>
        /// Calculate adaptive true anomaly distribution for better sampling
        /// </summary>
        private static double AdaptiveTrueAnomalyDistribution(double t, double eccentricity)
        {
            // More points near periapsis for eccentric orbits
            double densityFactor = 1.0 + 2.0 * eccentricity;
            double scaledT = Math.Pow(t, 1.0 / densityFactor);
            return 2.0 * Math.PI * scaledT;
        }
        
        /// <summary>
        /// Get position at specified true anomaly
        /// </summary>
        private static Double3 GetPositionAtTrueAnomaly(OrbitalState state, double trueAnomaly)
        {
            double r = state.SemiMajorAxis * (1.0 - state.Eccentricity * state.Eccentricity) / 
                      (1.0 + state.Eccentricity * Math.Cos(trueAnomaly));
            
            // Position in orbital plane
            Double3 position = new Double3(r * Math.Cos(trueAnomaly), r * Math.Sin(trueAnomaly), 0);
            
            // Transform to inertial frame
            return TransformToInertial(position, state.Inclination, state.LongitudeOfAscendingNode, state.ArgumentOfPeriapsis);
        }
        
        #endregion
    }
}