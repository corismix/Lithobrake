using Godot;
using System;
using System.Collections.Generic;

namespace Lithobrake.Core
{
    /// <summary>
    /// Comprehensive atmospheric physics system for Kerbin-scale atmosphere.
    /// Provides exponential density and pressure models, temperature gradients,
    /// and efficient atmospheric property calculations with caching.
    /// Integrates with UNIVERSE_CONSTANTS for deterministic atmospheric simulation.
    /// </summary>
    public partial class Atmosphere : Node
    {
        // Singleton pattern for global access
        private static Atmosphere? _instance;
        public static Atmosphere Instance => _instance ?? throw new InvalidOperationException("Atmosphere not initialized");
        
        // Atmospheric model constants
        private const double SEA_LEVEL_TEMPERATURE = 288.15; // 15°C in Kelvin
        private const double TEMPERATURE_LAPSE_RATE = 0.0065; // K/m standard atmospheric lapse rate
        private const double STRATOSPHERE_START = 11000.0; // 11km tropopause
        private const double STRATOSPHERE_TEMPERATURE = 216.65; // -56.5°C constant in stratosphere
        
        // Performance optimization - atmospheric property cache
        private readonly Dictionary<double, AtmosphericProperties> _altitudeCache = new();
        private const double CACHE_RESOLUTION = 100.0; // Cache every 100m
        private const int MAX_CACHE_ENTRIES = 1000; // Limit memory usage
        
        // Performance monitoring
        private double _lastCalculationTime = 0.0;
        private int _cacheHits = 0;
        private int _cacheMisses = 0;
        
        // Performance targets from current-task.md
        private const double DENSITY_CALCULATION_BUDGET = 0.1; // ms per frame per vessel
        
        public override void _Ready()
        {
            if (_instance == null)
            {
                _instance = this;
                PrecomputeAtmosphericTable();
                GD.Print("Atmosphere: Initialized with exponential density model and temperature gradients");
            }
            else
            {
                GD.PrintErr("Atmosphere: Multiple instances detected!");
                QueueFree();
            }
        }
        
        /// <summary>
        /// Precompute atmospheric properties table for common altitudes
        /// </summary>
        private void PrecomputeAtmosphericTable()
        {
            var startTime = Time.GetTicksMsec();
            
            // Precompute properties every 100m up to 80km
            for (double altitude = 0; altitude <= 80000; altitude += CACHE_RESOLUTION)
            {
                var cacheKey = Math.Round(altitude / CACHE_RESOLUTION) * CACHE_RESOLUTION;
                _altitudeCache[cacheKey] = CalculateAtmosphericProperties(altitude);
            }
            
            var duration = Time.GetTicksMsec() - startTime;
            GD.Print($"Atmosphere: Precomputed {_altitudeCache.Count} atmospheric properties in {duration:F1}ms");
        }
        
        /// <summary>
        /// Get atmospheric density at given altitude using exponential model
        /// ρ = 1.225 * exp(-h/7500) with altitude boundaries and caching
        /// </summary>
        /// <param name="altitude">Altitude above sea level in meters</param>
        /// <returns>Atmospheric density in kg/m³</returns>
        public static double GetDensity(double altitude)
        {
            var startTime = Time.GetTicksMsec();
            
            double density;
            
            // Handle boundary conditions
            if (altitude >= UNIVERSE_CONSTANTS.KERBIN_ATMOSPHERE_HEIGHT)
            {
                density = 0.0;
            }
            else if (altitude < 0)
            {
                // Below sea level - increase density slightly
                density = UNIVERSE_CONSTANTS.KERBIN_SEA_LEVEL_DENSITY * Math.Exp(-altitude / UNIVERSE_CONSTANTS.KERBIN_SCALE_HEIGHT);
            }
            else
            {
                // Use UNIVERSE_CONSTANTS for consistency
                density = UNIVERSE_CONSTANTS.AtmosphericDensity(altitude);
            }
            
            // Performance monitoring
            if (Instance != null)
            {
                Instance._lastCalculationTime = Time.GetTicksMsec() - startTime;
            }
            
            return density;
        }
        
        /// <summary>
        /// Get atmospheric pressure at given altitude using exponential model
        /// P = 101325 * exp(-h/7500) with altitude boundaries
        /// </summary>
        /// <param name="altitude">Altitude above sea level in meters</param>
        /// <returns>Atmospheric pressure in Pa</returns>
        public static double GetPressure(double altitude)
        {
            // Handle boundary conditions
            if (altitude >= UNIVERSE_CONSTANTS.KERBIN_ATMOSPHERE_HEIGHT)
                return 0.0;
            
            if (altitude < 0)
            {
                // Below sea level - increase pressure
                return UNIVERSE_CONSTANTS.KERBIN_SEA_LEVEL_PRESSURE * Math.Exp(-altitude / UNIVERSE_CONSTANTS.KERBIN_SCALE_HEIGHT);
            }
            
            // Use UNIVERSE_CONSTANTS for consistency
            return UNIVERSE_CONSTANTS.AtmosphericPressure(altitude);
        }
        
        /// <summary>
        /// Get atmospheric temperature at given altitude using temperature lapse rate
        /// Implements standard atmospheric model with troposphere and stratosphere
        /// </summary>
        /// <param name="altitude">Altitude above sea level in meters</param>
        /// <returns>Temperature in Kelvin</returns>
        public static double GetTemperature(double altitude)
        {
            // Handle boundary conditions
            if (altitude >= UNIVERSE_CONSTANTS.KERBIN_ATMOSPHERE_HEIGHT)
                return 2.7; // Near absolute zero in space
            
            if (altitude < 0)
            {
                // Below sea level - increase temperature slightly
                return SEA_LEVEL_TEMPERATURE - (altitude * TEMPERATURE_LAPSE_RATE);
            }
            
            if (altitude <= STRATOSPHERE_START)
            {
                // Troposphere: temperature decreases with altitude
                return SEA_LEVEL_TEMPERATURE - (altitude * TEMPERATURE_LAPSE_RATE);
            }
            else
            {
                // Stratosphere: constant temperature (simplified model)
                return STRATOSPHERE_TEMPERATURE;
            }
        }
        
        /// <summary>
        /// Get cached atmospheric properties for given altitude with performance optimization
        /// </summary>
        /// <param name="altitude">Altitude above sea level in meters</param>
        /// <returns>Cached atmospheric properties</returns>
        public static AtmosphericProperties GetCachedProperties(double altitude)
        {
            if (Instance == null)
                return CalculateAtmosphericProperties(altitude);
            
            var cacheKey = Math.Round(altitude / CACHE_RESOLUTION) * CACHE_RESOLUTION;
            
            if (Instance._altitudeCache.TryGetValue(cacheKey, out var cachedProperties))
            {
                Instance._cacheHits++;
                return cachedProperties;
            }
            
            // Cache miss - calculate and cache if within reasonable bounds
            Instance._cacheMisses++;
            var properties = CalculateAtmosphericProperties(altitude);
            
            if (Instance._altitudeCache.Count < MAX_CACHE_ENTRIES && altitude >= -1000 && altitude <= 80000)
            {
                Instance._altitudeCache[cacheKey] = properties;
            }
            
            return properties;
        }
        
        /// <summary>
        /// Calculate complete atmospheric properties for given altitude
        /// </summary>
        /// <param name="altitude">Altitude above sea level in meters</param>
        /// <returns>Complete atmospheric properties</returns>
        private static AtmosphericProperties CalculateAtmosphericProperties(double altitude)
        {
            return new AtmosphericProperties
            {
                Altitude = altitude,
                Density = GetDensity(altitude),
                Pressure = GetPressure(altitude),
                Temperature = GetTemperature(altitude),
                ScaleHeight = UNIVERSE_CONSTANTS.KERBIN_SCALE_HEIGHT,
                SoundSpeed = CalculateSoundSpeed(GetTemperature(altitude))
            };
        }
        
        /// <summary>
        /// Calculate speed of sound at given temperature
        /// </summary>
        /// <param name="temperature">Temperature in Kelvin</param>
        /// <returns>Speed of sound in m/s</returns>
        private static double CalculateSoundSpeed(double temperature)
        {
            const double GAMMA = 1.4; // Specific heat ratio for air
            const double R = 287.0; // Specific gas constant for air (J/kg·K)
            
            return Math.Sqrt(GAMMA * R * temperature);
        }
        
        /// <summary>
        /// Get atmospheric properties at vessel position with performance optimization
        /// </summary>
        /// <param name="vessel">Vessel to get atmospheric properties for</param>
        /// <returns>Atmospheric properties at vessel position</returns>
        public static AtmosphericProperties GetVesselAtmosphericProperties(PhysicsVessel vessel)
        {
            if (vessel?.RootPart?.RigidBody == null)
                return new AtmosphericProperties(); // Return default (vacuum) properties
            
            // Calculate altitude from vessel position
            var vesselPosition = vessel.RootPart.RigidBody.GlobalPosition;
            var altitude = vesselPosition.Length() - UNIVERSE_CONSTANTS.KERBIN_RADIUS;
            
            return GetCachedProperties(altitude);
        }
        
        /// <summary>
        /// Check if altitude is within atmosphere
        /// </summary>
        /// <param name="altitude">Altitude in meters</param>
        /// <returns>True if within atmosphere</returns>
        public static bool IsWithinAtmosphere(double altitude)
        {
            return altitude < UNIVERSE_CONSTANTS.KERBIN_ATMOSPHERE_HEIGHT;
        }
        
        /// <summary>
        /// Get atmospheric thickness factor (0 = vacuum, 1 = sea level)
        /// </summary>
        /// <param name="altitude">Altitude in meters</param>
        /// <returns>Atmospheric thickness factor 0-1</returns>
        public static double GetAtmosphericThicknessFactor(double altitude)
        {
            if (!IsWithinAtmosphere(altitude))
                return 0.0;
            
            var density = GetDensity(altitude);
            return Math.Clamp(density / UNIVERSE_CONSTANTS.KERBIN_SEA_LEVEL_DENSITY, 0.0, 1.0);
        }
        
        /// <summary>
        /// Get atmosphere performance metrics for monitoring
        /// </summary>
        /// <returns>Performance statistics</returns>
        public AtmospherePerformanceMetrics GetPerformanceMetrics()
        {
            return new AtmospherePerformanceMetrics
            {
                LastCalculationTime = _lastCalculationTime,
                CacheHits = _cacheHits,
                CacheMisses = _cacheMisses,
                CacheSize = _altitudeCache.Count,
                CacheHitRatio = _cacheHits + _cacheMisses > 0 ? (double)_cacheHits / (_cacheHits + _cacheMisses) : 0.0,
                CalculationBudget = DENSITY_CALCULATION_BUDGET,
                IsWithinBudget = _lastCalculationTime <= DENSITY_CALCULATION_BUDGET
            };
        }
        
        /// <summary>
        /// Clear atmospheric cache (for testing or memory management)
        /// </summary>
        public void ClearCache()
        {
            _altitudeCache.Clear();
            _cacheHits = 0;
            _cacheMisses = 0;
            GD.Print("Atmosphere: Cache cleared");
        }
        
        /// <summary>
        /// Cleanup on exit
        /// </summary>
        public override void _ExitTree()
        {
            if (_instance == this)
            {
                _instance = null;
            }
            
            base._ExitTree();
        }
    }
    
    /// <summary>
    /// Complete atmospheric properties at a specific altitude
    /// </summary>
    public struct AtmosphericProperties
    {
        public double Altitude; // Altitude in meters
        public double Density; // Atmospheric density in kg/m³
        public double Pressure; // Atmospheric pressure in Pa
        public double Temperature; // Temperature in Kelvin
        public double ScaleHeight; // Scale height in meters
        public double SoundSpeed; // Speed of sound in m/s
        
        /// <summary>
        /// Get dynamic pressure Q = 0.5 * ρ * v²
        /// </summary>
        /// <param name="velocity">Velocity vector in m/s</param>
        /// <returns>Dynamic pressure in Pa</returns>
        public readonly double GetDynamicPressure(Vector3 velocity)
        {
            var velocityMagnitudeSquared = velocity.LengthSquared();
            return 0.5 * Density * velocityMagnitudeSquared;
        }
        
        /// <summary>
        /// Get Mach number for given velocity
        /// </summary>
        /// <param name="velocity">Velocity vector in m/s</param>
        /// <returns>Mach number (dimensionless)</returns>
        public readonly double GetMachNumber(Vector3 velocity)
        {
            if (SoundSpeed <= 0)
                return 0.0;
            
            return velocity.Length() / SoundSpeed;
        }
        
        /// <summary>
        /// Check if velocity is supersonic
        /// </summary>
        /// <param name="velocity">Velocity vector in m/s</param>
        /// <returns>True if supersonic (Mach > 1)</returns>
        public readonly bool IsSupersonic(Vector3 velocity)
        {
            return GetMachNumber(velocity) > 1.0;
        }
    }
    
    /// <summary>
    /// Atmosphere performance monitoring metrics
    /// </summary>
    public struct AtmospherePerformanceMetrics
    {
        public double LastCalculationTime;
        public int CacheHits;
        public int CacheMisses;
        public int CacheSize;
        public double CacheHitRatio;
        public double CalculationBudget;
        public bool IsWithinBudget;
    }
}