using System;

namespace Lithobrake.Core
{
    /// <summary>
    /// Double precision orbital state representation using Keplerian orbital elements.
    /// Provides conversion methods between orbital elements and Cartesian state vectors.
    /// Handles orbital element singularities for circular and equatorial orbits.
    /// </summary>
    public struct OrbitalState : IEquatable<OrbitalState>
    {
        // Classical orbital elements (Keplerian)
        public double SemiMajorAxis { get; set; }           // a (meters) - orbit size
        public double Eccentricity { get; set; }            // e (dimensionless) - orbit shape
        public double Inclination { get; set; }             // i (radians) - orbit tilt
        public double LongitudeOfAscendingNode { get; set; } // Ω (radians) - orbit orientation
        public double ArgumentOfPeriapsis { get; set; }     // ω (radians) - periapsis direction
        public double MeanAnomaly { get; set; }             // M (radians) - position in orbit
        
        // Time reference and gravitational parameter
        public double Epoch { get; set; }                   // Time reference in seconds
        public double GravitationalParameter { get; set; }  // μ = GM for central body
        
        /// <summary>
        /// Create orbital state from Keplerian elements
        /// </summary>
        public OrbitalState(double semiMajorAxis, double eccentricity, double inclination,
                           double longitudeOfAscendingNode, double argumentOfPeriapsis, 
                           double meanAnomaly, double epoch, double gravitationalParameter)
        {
            SemiMajorAxis = semiMajorAxis;
            Eccentricity = eccentricity;
            Inclination = inclination;
            LongitudeOfAscendingNode = longitudeOfAscendingNode;
            ArgumentOfPeriapsis = argumentOfPeriapsis;
            MeanAnomaly = meanAnomaly;
            Epoch = epoch;
            GravitationalParameter = gravitationalParameter;
        }
        
        /// <summary>
        /// Create orbital state from position and velocity vectors
        /// </summary>
        public static OrbitalState FromCartesian(Double3 position, Double3 velocity, double epoch, 
                                                double gravitationalParameter = UNIVERSE_CONSTANTS.KERBIN_GRAVITATIONAL_PARAMETER)
        {
            var orbital = new OrbitalState();
            orbital.Epoch = epoch;
            orbital.GravitationalParameter = gravitationalParameter;
            
            // Calculate orbital elements from position and velocity
            double r = position.Length;
            double v = velocity.Length;
            
            // Angular momentum vector
            Double3 h = Double3.Cross(position, velocity);
            double h_mag = h.Length;
            
            // Eccentricity vector
            Double3 e_vec = ((v * v - gravitationalParameter / r) * position - Double3.Dot(position, velocity) * velocity) / gravitationalParameter;
            orbital.Eccentricity = e_vec.Length;
            
            // Specific orbital energy
            double energy = (v * v) / 2 - gravitationalParameter / r;
            
            // Semi-major axis (handle parabolic and hyperbolic cases)
            if (Math.Abs(energy) > UNIVERSE_CONSTANTS.PRECISION_THRESHOLD)
            {
                orbital.SemiMajorAxis = -gravitationalParameter / (2 * energy);
            }
            else
            {
                // Parabolic orbit
                orbital.SemiMajorAxis = double.PositiveInfinity;
            }
            
            // Inclination
            orbital.Inclination = Math.Acos(Math.Max(-1, Math.Min(1, h.Z / h_mag)));
            
            // Longitude of ascending node (handle equatorial orbits)
            Double3 n = Double3.Cross(Double3.Up, h); // Node vector
            double n_mag = n.Length;
            
            if (n_mag > UNIVERSE_CONSTANTS.PRECISION_THRESHOLD)
            {
                orbital.LongitudeOfAscendingNode = Math.Atan2(n.Y, n.X);
                if (orbital.LongitudeOfAscendingNode < 0)
                    orbital.LongitudeOfAscendingNode += 2 * Math.PI;
            }
            else
            {
                // Equatorial orbit - set RAAN to zero
                orbital.LongitudeOfAscendingNode = 0.0;
            }
            
            // Argument of periapsis (handle circular orbits)
            if (orbital.Eccentricity > UNIVERSE_CONSTANTS.PRECISION_THRESHOLD && n_mag > UNIVERSE_CONSTANTS.PRECISION_THRESHOLD)
            {
                orbital.ArgumentOfPeriapsis = Math.Acos(Math.Max(-1, Math.Min(1, Double3.Dot(n, e_vec) / (n_mag * orbital.Eccentricity))));
                if (e_vec.Z < 0)
                    orbital.ArgumentOfPeriapsis = 2 * Math.PI - orbital.ArgumentOfPeriapsis;
            }
            else
            {
                // Circular or equatorial orbit
                orbital.ArgumentOfPeriapsis = 0.0;
            }
            
            // True anomaly and mean anomaly
            double trueAnomaly;
            if (orbital.Eccentricity > UNIVERSE_CONSTANTS.PRECISION_THRESHOLD)
            {
                trueAnomaly = Math.Acos(Math.Max(-1, Math.Min(1, Double3.Dot(e_vec, position) / (orbital.Eccentricity * r))));
                if (Double3.Dot(position, velocity) < 0)
                    trueAnomaly = 2 * Math.PI - trueAnomaly;
            }
            else
            {
                // Circular orbit - use longitude
                if (n_mag > UNIVERSE_CONSTANTS.PRECISION_THRESHOLD)
                {
                    trueAnomaly = Math.Acos(Math.Max(-1, Math.Min(1, Double3.Dot(n, position) / (n_mag * r))));
                    if (position.Z < 0)
                        trueAnomaly = 2 * Math.PI - trueAnomaly;
                }
                else
                {
                    // Equatorial circular orbit
                    trueAnomaly = Math.Atan2(position.Y, position.X);
                    if (trueAnomaly < 0)
                        trueAnomaly += 2 * Math.PI;
                }
            }
            
            // Convert true anomaly to mean anomaly
            orbital.MeanAnomaly = orbital.TrueToMeanAnomaly(trueAnomaly);
            
            return orbital;
        }
        
        /// <summary>
        /// Convert orbital state to Cartesian position and velocity at given time
        /// </summary>
        public (Double3 position, Double3 velocity) ToCartesian(double time)
        {
            // Calculate mean anomaly at given time
            double meanAnomalyAtTime = MeanAnomaly + MeanMotion * (time - Epoch);
            
            // Solve Kepler's equation for eccentric anomaly
            double eccentricAnomaly = SolveKeplerEquation(meanAnomalyAtTime);
            
            // Convert to true anomaly
            double trueAnomaly = EccentricToTrueAnomaly(eccentricAnomaly);
            
            // Calculate position in orbital plane
            double r = SemiMajorAxis * (1 - Eccentricity * Math.Cos(eccentricAnomaly));
            double x = r * Math.Cos(trueAnomaly);
            double y = r * Math.Sin(trueAnomaly);
            
            // Calculate velocity in orbital plane using vis-viva equation
            double h = Math.Sqrt(GravitationalParameter * SemiMajorAxis * (1 - Eccentricity * Eccentricity));
            double vx = -h * Math.Sin(trueAnomaly) / r;
            double vy = h * (Eccentricity + Math.Cos(trueAnomaly)) / r;
            
            // Transform from orbital plane to inertial coordinates
            return TransformToInertial(new Double3(x, y, 0), new Double3(vx, vy, 0));
        }
        
        /// <summary>
        /// Calculate mean motion (radians per second)
        /// </summary>
        public double MeanMotion => Math.Sqrt(GravitationalParameter / (SemiMajorAxis * SemiMajorAxis * SemiMajorAxis));
        
        /// <summary>
        /// Calculate orbital period in seconds
        /// </summary>
        public double OrbitalPeriod => 2 * Math.PI / MeanMotion;
        
        /// <summary>
        /// Calculate specific orbital energy (energy per unit mass)
        /// </summary>
        public double SpecificOrbitalEnergy => -GravitationalParameter / (2 * SemiMajorAxis);
        
        /// <summary>
        /// Check if orbit is bound (elliptical)
        /// </summary>
        public bool IsBound => Eccentricity < 1.0 && SemiMajorAxis > 0;
        
        /// <summary>
        /// Check if orbit is circular
        /// </summary>
        public bool IsCircular => Eccentricity < UNIVERSE_CONSTANTS.PRECISION_THRESHOLD;
        
        /// <summary>
        /// Check if orbit is equatorial
        /// </summary>
        public bool IsEquatorial => Math.Abs(Inclination) < UNIVERSE_CONSTANTS.PRECISION_THRESHOLD;
        
        /// <summary>
        /// Solve Kepler's equation using Newton-Raphson method
        /// </summary>
        private double SolveKeplerEquation(double meanAnomaly)
        {
            // Normalize mean anomaly to [0, 2π]
            meanAnomaly = meanAnomaly % (2 * Math.PI);
            if (meanAnomaly < 0) meanAnomaly += 2 * Math.PI;
            
            // Initial guess
            double E = meanAnomaly + Eccentricity * Math.Sin(meanAnomaly);
            
            // Newton-Raphson iteration
            for (int i = 0; i < UNIVERSE_CONSTANTS.KEPLER_MAX_ITERATIONS; i++)
            {
                double f = E - Eccentricity * Math.Sin(E) - meanAnomaly;
                double df = 1 - Eccentricity * Math.Cos(E);
                
                double deltaE = f / df;
                E -= deltaE;
                
                if (Math.Abs(deltaE) < UNIVERSE_CONSTANTS.KEPLER_TOLERANCE)
                    break;
            }
            
            return E;
        }
        
        /// <summary>
        /// Convert eccentric anomaly to true anomaly
        /// </summary>
        private double EccentricToTrueAnomaly(double eccentricAnomaly)
        {
            double cosE = Math.Cos(eccentricAnomaly);
            double sinE = Math.Sin(eccentricAnomaly);
            
            double cosNu = (cosE - Eccentricity) / (1 - Eccentricity * cosE);
            double sinNu = Math.Sqrt(1 - Eccentricity * Eccentricity) * sinE / (1 - Eccentricity * cosE);
            
            return Math.Atan2(sinNu, cosNu);
        }
        
        /// <summary>
        /// Convert true anomaly to mean anomaly
        /// </summary>
        private double TrueToMeanAnomaly(double trueAnomaly)
        {
            // Convert to eccentric anomaly
            double cosNu = Math.Cos(trueAnomaly);
            double cosE = (Eccentricity + cosNu) / (1 + Eccentricity * cosNu);
            double E = Math.Acos(Math.Max(-1, Math.Min(1, cosE)));
            
            if (trueAnomaly > Math.PI)
                E = 2 * Math.PI - E;
            
            // Convert to mean anomaly
            return E - Eccentricity * Math.Sin(E);
        }
        
        /// <summary>
        /// Transform position and velocity from orbital plane to inertial coordinates
        /// </summary>
        private (Double3 position, Double3 velocity) TransformToInertial(Double3 positionOrbital, Double3 velocityOrbital)
        {
            // Rotation matrices for orbital element transformations
            double cosO = Math.Cos(LongitudeOfAscendingNode);
            double sinO = Math.Sin(LongitudeOfAscendingNode);
            double cosI = Math.Cos(Inclination);
            double sinI = Math.Sin(Inclination);
            double cosW = Math.Cos(ArgumentOfPeriapsis);
            double sinW = Math.Sin(ArgumentOfPeriapsis);
            
            // Combined transformation matrix elements
            double P11 = cosO * cosW - sinO * sinW * cosI;
            double P12 = -cosO * sinW - sinO * cosW * cosI;
            double P21 = sinO * cosW + cosO * sinW * cosI;
            double P22 = -sinO * sinW + cosO * cosW * cosI;
            double P31 = sinW * sinI;
            double P32 = cosW * sinI;
            
            // Transform position
            Double3 position = new Double3(
                P11 * positionOrbital.X + P12 * positionOrbital.Y,
                P21 * positionOrbital.X + P22 * positionOrbital.Y,
                P31 * positionOrbital.X + P32 * positionOrbital.Y
            );
            
            // Transform velocity
            Double3 velocity = new Double3(
                P11 * velocityOrbital.X + P12 * velocityOrbital.Y,
                P21 * velocityOrbital.X + P22 * velocityOrbital.Y,
                P31 * velocityOrbital.X + P32 * velocityOrbital.Y
            );
            
            return (position, velocity);
        }
        
        /// <summary>
        /// Propagate orbital state to a new time
        /// </summary>
        public OrbitalState PropagateToTime(double newTime)
        {
            var newState = this;
            newState.MeanAnomaly = MeanAnomaly + MeanMotion * (newTime - Epoch);
            newState.Epoch = newTime;
            return newState;
        }
        
        /// <summary>
        /// Validate orbital state for numerical stability
        /// </summary>
        public bool IsValid()
        {
            return UNIVERSE_CONSTANTS.IsValidOrbitalValue(SemiMajorAxis) &&
                   UNIVERSE_CONSTANTS.IsValidOrbitalValue(GravitationalParameter) &&
                   Eccentricity >= 0 &&
                   Inclination >= 0 && Inclination <= Math.PI &&
                   !double.IsNaN(MeanAnomaly) && !double.IsInfinity(MeanAnomaly);
        }
        
        // Equality and comparison
        public bool Equals(OrbitalState other)
        {
            const double tolerance = UNIVERSE_CONSTANTS.PRECISION_THRESHOLD;
            return Math.Abs(SemiMajorAxis - other.SemiMajorAxis) < tolerance &&
                   Math.Abs(Eccentricity - other.Eccentricity) < tolerance &&
                   Math.Abs(Inclination - other.Inclination) < tolerance &&
                   Math.Abs(LongitudeOfAscendingNode - other.LongitudeOfAscendingNode) < tolerance &&
                   Math.Abs(ArgumentOfPeriapsis - other.ArgumentOfPeriapsis) < tolerance &&
                   Math.Abs(MeanAnomaly - other.MeanAnomaly) < tolerance &&
                   Math.Abs(Epoch - other.Epoch) < tolerance &&
                   Math.Abs(GravitationalParameter - other.GravitationalParameter) < tolerance;
        }
        
        public override bool Equals(object? obj) => obj is OrbitalState other && Equals(other);
        
        public override int GetHashCode() => HashCode.Combine(SemiMajorAxis, Eccentricity, Inclination, 
                                                             LongitudeOfAscendingNode, ArgumentOfPeriapsis, 
                                                             MeanAnomaly, Epoch, GravitationalParameter);
        
        public static bool operator ==(OrbitalState a, OrbitalState b) => a.Equals(b);
        public static bool operator !=(OrbitalState a, OrbitalState b) => !a.Equals(b);
        
        public override string ToString()
        {
            return $"OrbitalState(a={SemiMajorAxis/1000:F1}km, e={Eccentricity:F4}, i={Inclination*180/Math.PI:F1}°, " +
                   $"Ω={LongitudeOfAscendingNode*180/Math.PI:F1}°, ω={ArgumentOfPeriapsis*180/Math.PI:F1}°, " +
                   $"M={MeanAnomaly*180/Math.PI:F1}°, T={Epoch:F1}s)";
        }
    }
}