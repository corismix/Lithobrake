using Godot;
using System;

namespace Lithobrake.Core
{
    /// <summary>
    /// Atmospheric conditions calculator for Kerbin-scale atmospheric model.
    /// Provides density, pressure, and dynamic pressure calculations for the anti-wobble system.
    /// Uses exponential atmosphere model with 70km height and 7.5km scale height.
    /// </summary>
    public static class AtmosphericConditions
    {
        // Atmospheric constants for Kerbin-scale atmosphere (from UNIVERSE_CONSTANTS)
        private const double AtmosphereHeight = UNIVERSE_CONSTANTS.KERBIN_ATMOSPHERE_HEIGHT;
        private const double ScaleHeight = UNIVERSE_CONSTANTS.KERBIN_SCALE_HEIGHT;
        private const double SeaLevelPressure = UNIVERSE_CONSTANTS.KERBIN_SEA_LEVEL_PRESSURE;
        private const double SeaLevelDensity = UNIVERSE_CONSTANTS.KERBIN_SEA_LEVEL_DENSITY;
        private const double GasConstant = 287.0;         // J/(kg·K) for air
        private const double SeaLevelTemperature = 288.15; // 15°C = 288.15K
        
        /// <summary>
        /// Calculate atmospheric density at given altitude
        /// </summary>
        /// <param name="altitude">Altitude above sea level in meters</param>
        /// <returns>Atmospheric density in kg/m³</returns>
        public static double GetDensity(double altitude)
        {
            if (altitude >= AtmosphereHeight)
                return 0.0; // No atmosphere above 70km
                
            if (altitude < 0.0)
                altitude = 0.0; // Clamp to sea level
                
            // Exponential atmosphere model: ρ = ρ₀ * e^(-h/H)
            // where ρ₀ = sea level density, h = altitude, H = scale height
            var densityRatio = FastMath.FastAtmosphericExp(altitude);
            return SeaLevelDensity * densityRatio;
        }
        
        /// <summary>
        /// Calculate atmospheric pressure at given altitude
        /// </summary>
        /// <param name="altitude">Altitude above sea level in meters</param>
        /// <returns>Atmospheric pressure in Pascals</returns>
        public static double GetPressure(double altitude)
        {
            if (altitude >= AtmosphereHeight)
                return 0.0; // No atmosphere above 70km
                
            if (altitude < 0.0)
                altitude = 0.0; // Clamp to sea level
                
            // Exponential atmosphere model: p = p₀ * e^(-h/H)
            var pressureRatio = FastMath.FastAtmosphericExp(altitude);
            return SeaLevelPressure * pressureRatio;
        }
        
        /// <summary>
        /// Calculate dynamic pressure (Q = 0.5 * ρ * v²)
        /// This is the key parameter used by the anti-wobble system
        /// </summary>
        /// <param name="altitude">Altitude above sea level in meters</param>
        /// <param name="velocity">Velocity magnitude in m/s</param>
        /// <returns>Dynamic pressure in Pascals (Pa)</returns>
        public static double GetDynamicPressure(double altitude, double velocity)
        {
            var density = GetDensity(altitude);
            var velocitySquared = velocity * velocity;
            return 0.5 * density * velocitySquared;
        }
        
        /// <summary>
        /// Calculate dynamic pressure from velocity vector
        /// </summary>
        /// <param name="altitude">Altitude above sea level in meters</param>
        /// <param name="velocity">Velocity vector in m/s</param>
        /// <returns>Dynamic pressure in Pascals (Pa)</returns>
        public static double GetDynamicPressure(double altitude, Double3 velocity)
        {
            return GetDynamicPressure(altitude, velocity.Length);
        }
        
        /// <summary>
        /// Calculate dynamic pressure from Godot Vector3
        /// </summary>
        /// <param name="altitude">Altitude above sea level in meters</param>
        /// <param name="velocity">Velocity vector in m/s</param>
        /// <returns>Dynamic pressure in Pascals (Pa)</returns>
        public static double GetDynamicPressure(double altitude, Vector3 velocity)
        {
            return GetDynamicPressure(altitude, velocity.Length());
        }
        
        /// <summary>
        /// Get atmospheric conditions at given altitude
        /// </summary>
        /// <param name="altitude">Altitude above sea level in meters</param>
        /// <returns>Complete atmospheric conditions</returns>
        public static AtmosphericState GetAtmosphericState(double altitude)
        {
            return new AtmosphericState
            {
                Altitude = Math.Max(0.0, altitude),
                Density = GetDensity(altitude),
                Pressure = GetPressure(altitude),
                Temperature = CalculateTemperature(altitude),
                HasAtmosphere = altitude < AtmosphereHeight
            };
        }
        
        /// <summary>
        /// Calculate temperature at given altitude (simplified linear model)
        /// </summary>
        private static double CalculateTemperature(double altitude)
        {
            if (altitude >= AtmosphereHeight)
                return 0.0; // Space temperature (essentially vacuum)
                
            // Simple linear temperature model: -6.5°C per 1000m
            const double TemperatureLapseRate = -0.0065; // K/m
            return SeaLevelTemperature + (TemperatureLapseRate * altitude);
        }
        
        /// <summary>
        /// Check if given dynamic pressure exceeds anti-wobble thresholds
        /// </summary>
        /// <param name="dynamicPressure">Dynamic pressure in Pascals</param>
        /// <param name="currentlyEnabled">Current anti-wobble state</param>
        /// <returns>Whether anti-wobble should be enabled</returns>
        public static bool ShouldEnableAntiWobble(double dynamicPressure, bool currentlyEnabled)
        {
            const double QEnable = 12000.0;  // 12 kPa - enable threshold
            const double QDisable = 8000.0;  // 8 kPa - disable threshold (hysteresis)
            
            if (currentlyEnabled)
            {
                // Use lower threshold to prevent oscillation
                return dynamicPressure >= QDisable;
            }
            else
            {
                // Use higher threshold to enable
                return dynamicPressure >= QEnable;
            }
        }
    }
    
    /// <summary>
    /// Complete atmospheric state at a given altitude
    /// </summary>
    public struct AtmosphericState
    {
        /// <summary>
        /// Altitude above sea level in meters
        /// </summary>
        public double Altitude;
        
        /// <summary>
        /// Atmospheric density in kg/m³
        /// </summary>
        public double Density;
        
        /// <summary>
        /// Atmospheric pressure in Pascals
        /// </summary>
        public double Pressure;
        
        /// <summary>
        /// Temperature in Kelvin
        /// </summary>
        public double Temperature;
        
        /// <summary>
        /// Whether atmosphere exists at this altitude
        /// </summary>
        public bool HasAtmosphere;
        
        /// <summary>
        /// Calculate dynamic pressure for given velocity
        /// </summary>
        public double GetDynamicPressure(double velocity)
        {
            return 0.5 * Density * velocity * velocity;
        }
        
        /// <summary>
        /// Get pressure in kPa for display purposes
        /// </summary>
        public double PressureKPa => Pressure / 1000.0;
    }
}