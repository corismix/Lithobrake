using System;

namespace Lithobrake.Core
{
    /// <summary>
    /// Celestial body definition with physical parameters and gravitational field calculation.
    /// Provides homeworld constants and gravitational force computation for orbital mechanics.
    /// Immutable design for deterministic physics simulation.
    /// </summary>
    public class CelestialBody
    {
        // Physical properties (immutable for deterministic simulation)
        public string Name { get; }
        public double Radius { get; }                    // Surface radius in meters
        public double Mass { get; }                      // Mass in kg
        public double GravitationalParameter { get; }    // μ = GM in m³/s²
        public double AtmosphereHeight { get; }          // Atmosphere height in meters
        public double ScaleHeight { get; }               // Atmospheric scale height in meters
        public double SurfacePressure { get; }           // Sea level pressure in Pa
        public double SurfaceDensity { get; }            // Sea level density in kg/m³
        public double SurfaceGravity { get; }            // Surface gravity in m/s²
        
        // World position (mutable for floating origin system)
        public Double3 Position { get; set; }           // Current world position
        
        /// <summary>
        /// Create celestial body with specified parameters
        /// </summary>
        public CelestialBody(string name, double radius, double gravitationalParameter, 
                           double atmosphereHeight = 0, double scaleHeight = 0,
                           double surfacePressure = 0, double surfaceDensity = 0,
                           Double3 position = default)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Radius = radius;
            GravitationalParameter = gravitationalParameter;
            AtmosphereHeight = atmosphereHeight;
            ScaleHeight = scaleHeight;
            SurfacePressure = surfacePressure;
            SurfaceDensity = surfaceDensity;
            Position = position;
            
            // Calculate derived properties
            Mass = gravitationalParameter / UNIVERSE_CONSTANTS.GRAVITATIONAL_CONSTANT;
            SurfaceGravity = gravitationalParameter / (radius * radius);
            
            // Validate parameters
            if (radius <= 0)
                throw new ArgumentException("Radius must be positive", nameof(radius));
            if (gravitationalParameter <= 0)
                throw new ArgumentException("Gravitational parameter must be positive", nameof(gravitationalParameter));
        }
        
        /// <summary>
        /// Create Kerbin homeworld with standard parameters
        /// </summary>
        public static CelestialBody CreateKerbin(Double3 position = default)
        {
            return new CelestialBody(
                name: "Kerbin",
                radius: UNIVERSE_CONSTANTS.KERBIN_RADIUS,
                gravitationalParameter: UNIVERSE_CONSTANTS.KERBIN_GRAVITATIONAL_PARAMETER,
                atmosphereHeight: UNIVERSE_CONSTANTS.KERBIN_ATMOSPHERE_HEIGHT,
                scaleHeight: UNIVERSE_CONSTANTS.KERBIN_SCALE_HEIGHT,
                surfacePressure: UNIVERSE_CONSTANTS.KERBIN_SEA_LEVEL_PRESSURE,
                surfaceDensity: UNIVERSE_CONSTANTS.KERBIN_SEA_LEVEL_DENSITY,
                position: position
            );
        }
        
        /// <summary>
        /// Calculate gravitational force on a mass at given position
        /// F = GMm/r² in direction from position to body center
        /// </summary>
        /// <param name="objectPosition">Position of object in world coordinates</param>
        /// <param name="objectMass">Mass of object in kg</param>
        /// <returns>Gravitational force vector in Newtons</returns>
        public Double3 CalculateGravitationalForce(Double3 objectPosition, double objectMass)
        {
            Double3 displacement = Position - objectPosition;
            double distanceSquared = displacement.LengthSquared;
            
            if (distanceSquared < UNIVERSE_CONSTANTS.PRECISION_THRESHOLD)
            {
                // Prevent singularity at center
                return Double3.Zero;
            }
            
            double distance = Math.Sqrt(distanceSquared);
            double forceMagnitude = (GravitationalParameter * objectMass) / distanceSquared;
            
            return displacement.Normalized * forceMagnitude;
        }
        
        /// <summary>
        /// Calculate gravitational acceleration at given position
        /// a = GM/r² in direction from position to body center
        /// </summary>
        /// <param name="objectPosition">Position of object in world coordinates</param>
        /// <returns>Gravitational acceleration vector in m/s²</returns>
        public Double3 CalculateGravitationalAcceleration(Double3 objectPosition)
        {
            Double3 displacement = Position - objectPosition;
            double distanceSquared = displacement.LengthSquared;
            
            if (distanceSquared < UNIVERSE_CONSTANTS.PRECISION_THRESHOLD)
            {
                // Prevent singularity at center
                return Double3.Zero;
            }
            
            double distance = Math.Sqrt(distanceSquared);
            double accelerationMagnitude = GravitationalParameter / distanceSquared;
            
            return displacement.Normalized * accelerationMagnitude;
        }
        
        /// <summary>
        /// Get altitude above surface for given position
        /// </summary>
        /// <param name="objectPosition">Position of object in world coordinates</param>
        /// <returns>Altitude above surface in meters (negative if below surface)</returns>
        public double GetAltitude(Double3 objectPosition)
        {
            double distanceFromCenter = (objectPosition - Position).Length;
            return distanceFromCenter - Radius;
        }
        
        /// <summary>
        /// Check if position is within atmosphere
        /// </summary>
        /// <param name="objectPosition">Position of object in world coordinates</param>
        /// <returns>True if position is within atmospheric boundary</returns>
        public bool IsInAtmosphere(Double3 objectPosition)
        {
            if (AtmosphereHeight <= 0)
                return false;
            
            return GetAltitude(objectPosition) < AtmosphereHeight;
        }
        
        /// <summary>
        /// Calculate atmospheric pressure at given position using exponential model
        /// </summary>
        /// <param name="objectPosition">Position of object in world coordinates</param>
        /// <returns>Atmospheric pressure in Pa</returns>
        public double GetAtmosphericPressure(Double3 objectPosition)
        {
            if (AtmosphereHeight <= 0 || ScaleHeight <= 0)
                return 0.0;
            
            double altitude = GetAltitude(objectPosition);
            if (altitude >= AtmosphereHeight)
                return 0.0;
            
            if (altitude < 0)
                altitude = 0; // Clamp to surface level
            
            return SurfacePressure * Math.Exp(-altitude / ScaleHeight);
        }
        
        /// <summary>
        /// Calculate atmospheric density at given position using exponential model
        /// </summary>
        /// <param name="objectPosition">Position of object in world coordinates</param>
        /// <returns>Atmospheric density in kg/m³</returns>
        public double GetAtmosphericDensity(Double3 objectPosition)
        {
            if (AtmosphereHeight <= 0 || ScaleHeight <= 0)
                return 0.0;
            
            double altitude = GetAltitude(objectPosition);
            if (altitude >= AtmosphereHeight)
                return 0.0;
            
            if (altitude < 0)
                altitude = 0; // Clamp to surface level
            
            return SurfaceDensity * Math.Exp(-altitude / ScaleHeight);
        }
        
        /// <summary>
        /// Calculate escape velocity at given position
        /// </summary>
        /// <param name="objectPosition">Position of object in world coordinates</param>
        /// <returns>Escape velocity in m/s</returns>
        public double GetEscapeVelocity(Double3 objectPosition)
        {
            double distanceFromCenter = (objectPosition - Position).Length;
            return Math.Sqrt(2 * GravitationalParameter / distanceFromCenter);
        }
        
        /// <summary>
        /// Calculate circular orbital velocity at given position
        /// </summary>
        /// <param name="objectPosition">Position of object in world coordinates</param>
        /// <returns>Circular orbital velocity in m/s</returns>
        public double GetCircularVelocity(Double3 objectPosition)
        {
            double distanceFromCenter = (objectPosition - Position).Length;
            return Math.Sqrt(GravitationalParameter / distanceFromCenter);
        }
        
        /// <summary>
        /// Calculate orbital period for circular orbit at given position
        /// </summary>
        /// <param name="objectPosition">Position of object in world coordinates</param>
        /// <returns>Orbital period in seconds</returns>
        public double GetOrbitalPeriod(Double3 objectPosition)
        {
            double distanceFromCenter = (objectPosition - Position).Length;
            double r_cubed = distanceFromCenter * distanceFromCenter * distanceFromCenter;
            return 2 * Math.PI * Math.Sqrt(r_cubed / GravitationalParameter);
        }
        
        /// <summary>
        /// Create orbital state for circular orbit at given altitude
        /// </summary>
        /// <param name="altitude">Altitude above surface in meters</param>
        /// <param name="inclination">Orbital inclination in radians (default 0)</param>
        /// <param name="epoch">Time reference in seconds (default 0)</param>
        /// <returns>OrbitalState for circular orbit</returns>
        public OrbitalState CreateCircularOrbit(double altitude, double inclination = 0, double epoch = 0)
        {
            double semiMajorAxis = Radius + altitude;
            
            return new OrbitalState(
                semiMajorAxis: semiMajorAxis,
                eccentricity: 0.0,  // Circular orbit
                inclination: inclination,
                longitudeOfAscendingNode: 0.0,
                argumentOfPeriapsis: 0.0,
                meanAnomaly: 0.0,
                epoch: epoch,
                gravitationalParameter: GravitationalParameter
            );
        }
        
        /// <summary>
        /// Check if object position is above surface (safe for orbital mechanics)
        /// </summary>
        /// <param name="objectPosition">Position of object in world coordinates</param>
        /// <returns>True if position is above surface</returns>
        public bool IsAboveSurface(Double3 objectPosition)
        {
            return GetAltitude(objectPosition) > 0;
        }
        
        public override string ToString()
        {
            return $"{Name} (R={Radius/1000:F0}km, μ={GravitationalParameter:E3} m³/s², g={SurfaceGravity:F2} m/s²)";
        }
    }
}