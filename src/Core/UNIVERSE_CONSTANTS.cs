using System;

namespace Lithobrake.Core
{
    /// <summary>
    /// Central constants definition for all celestial body parameters and universe simulation.
    /// All values locked for deterministic simulation consistency across sessions.
    /// </summary>
    public static class UNIVERSE_CONSTANTS
    {
        // Kerbin (Homeworld) Parameters - Scaled for game simulation
        public const double KERBIN_RADIUS = 600_000.0; // 600 km radius in meters
        public const double KERBIN_MASS = 5.2915793e22; // kg (calculated from μ and G)
        public const double KERBIN_GRAVITATIONAL_PARAMETER = 3.5316e12; // μ = GM in m³/s²
        
        // Surface and atmospheric properties
        public const double KERBIN_STANDARD_GRAVITY = 9.81; // m/s² at surface
        public const double KERBIN_ATMOSPHERE_HEIGHT = 70_000.0; // 70 km atmosphere in meters
        public const double KERBIN_SCALE_HEIGHT = 7_500.0; // 7.5 km scale height in meters
        public const double KERBIN_SEA_LEVEL_PRESSURE = 101_325.0; // Pa (standard atmosphere)
        public const double KERBIN_SEA_LEVEL_DENSITY = 1.225; // kg/m³ air density at sea level
        
        // Universal constants
        public const double GRAVITATIONAL_CONSTANT = 6.67430e-11; // G in m³/(kg⋅s²)
        public const double SPEED_OF_LIGHT = 299_792_458.0; // m/s (not used in simulation but available)
        
        // Simulation constants
        public const double PHYSICS_TIMESTEP = 1.0 / 60.0; // 60Hz physics in seconds
        public const double FLOATING_ORIGIN_THRESHOLD = 20_000.0; // 20 km threshold for coordinate shifts
        public const double SOI_KERBIN = double.PositiveInfinity; // Infinite SOI for single body system
        
        // Orbital mechanics calculation constants
        public const double KEPLER_TOLERANCE = 1e-10; // Tolerance for Kepler solver convergence
        public const int KEPLER_MAX_ITERATIONS = 100; // Maximum iterations for Kepler solver
        public const double ORBITAL_DRIFT_THRESHOLD = 0.01; // 1% maximum allowed orbital drift
        
        // Performance limits
        public const int MAX_PARTS_PER_VESSEL = 75; // Performance constraint from CLAUDE.md
        public const double PHYSICS_BUDGET_MS = 5.0; // Maximum physics time per frame in milliseconds
        public const double ORBITAL_CALC_BUDGET_MS = 0.5; // Maximum orbital calculations time per frame
        
        // Coordinate system constants
        public const double PRECISION_THRESHOLD = 1e-15; // Double precision numerical threshold
        public const double METERS_TO_KM = 0.001; // Conversion factor
        public const double KM_TO_METERS = 1000.0; // Conversion factor
        
        /// <summary>
        /// Calculate surface gravity at given radius from center
        /// </summary>
        /// <param name="radius">Distance from center in meters</param>
        /// <returns>Gravitational acceleration in m/s²</returns>
        public static double SurfaceGravity(double radius)
        {
            return KERBIN_GRAVITATIONAL_PARAMETER / (radius * radius);
        }
        
        /// <summary>
        /// Calculate atmospheric pressure at given altitude using exponential model
        /// </summary>
        /// <param name="altitude">Altitude above sea level in meters</param>
        /// <returns>Atmospheric pressure in Pa</returns>
        public static double AtmosphericPressure(double altitude)
        {
            if (altitude >= KERBIN_ATMOSPHERE_HEIGHT)
                return 0.0;
            
            return KERBIN_SEA_LEVEL_PRESSURE * Math.Exp(-altitude / KERBIN_SCALE_HEIGHT);
        }
        
        /// <summary>
        /// Calculate atmospheric density at given altitude using exponential model
        /// </summary>
        /// <param name="altitude">Altitude above sea level in meters</param>
        /// <returns>Atmospheric density in kg/m³</returns>
        public static double AtmosphericDensity(double altitude)
        {
            if (altitude >= KERBIN_ATMOSPHERE_HEIGHT)
                return 0.0;
            
            return KERBIN_SEA_LEVEL_DENSITY * Math.Exp(-altitude / KERBIN_SCALE_HEIGHT);
        }
        
        /// <summary>
        /// Calculate escape velocity at given radius
        /// </summary>
        /// <param name="radius">Distance from center in meters</param>
        /// <returns>Escape velocity in m/s</returns>
        public static double EscapeVelocity(double radius)
        {
            return Math.Sqrt(2 * KERBIN_GRAVITATIONAL_PARAMETER / radius);
        }
        
        /// <summary>
        /// Calculate circular orbital velocity at given radius
        /// </summary>
        /// <param name="radius">Orbital radius from center in meters</param>
        /// <returns>Circular orbital velocity in m/s</returns>
        public static double CircularVelocity(double radius)
        {
            return Math.Sqrt(KERBIN_GRAVITATIONAL_PARAMETER / radius);
        }
        
        /// <summary>
        /// Calculate orbital period for circular orbit at given radius
        /// </summary>
        /// <param name="radius">Orbital radius from center in meters</param>
        /// <returns>Orbital period in seconds</returns>
        public static double OrbitalPeriod(double radius)
        {
            return 2 * Math.PI * Math.Sqrt((radius * radius * radius) / KERBIN_GRAVITATIONAL_PARAMETER);
        }
        
        /// <summary>
        /// Validate that orbital mechanics calculations stay within precision bounds
        /// </summary>
        /// <param name="value">Value to check</param>
        /// <returns>True if value is within valid precision bounds</returns>
        public static bool IsValidOrbitalValue(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && Math.Abs(value) > PRECISION_THRESHOLD;
        }
    }
}