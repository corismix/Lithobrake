using System;
using System.Runtime.CompilerServices;

namespace Lithobrake.Core
{
    /// <summary>
    /// Fast math operations using lookup tables to replace expensive Math.Exp/Sinh/Cosh in physics loops.
    /// Provides significant performance improvements over transcendental functions at the cost of small accuracy loss.
    /// </summary>
    public static class FastMath
    {
        // Atmospheric exponential lookup table for altitude calculations
        private const int ATMOSPHERE_TABLE_SIZE = 1024;
        private const double MAX_ALTITUDE = 100000.0; // 100km max altitude
        private const double ALTITUDE_STEP = MAX_ALTITUDE / ATMOSPHERE_TABLE_SIZE;
        private static readonly float[] _expLookupTable = new float[ATMOSPHERE_TABLE_SIZE + 1];
        
        // Hyperbolic function lookup tables for orbital mechanics
        private const int HYPERBOLIC_TABLE_SIZE = 512;
        private const double MAX_H_VALUE = 10.0; // Reasonable range for hyperbolic eccentric anomaly
        private const double H_STEP = MAX_H_VALUE / HYPERBOLIC_TABLE_SIZE;
        private static readonly float[] _sinhLookupTable = new float[HYPERBOLIC_TABLE_SIZE + 1];
        private static readonly float[] _coshLookupTable = new float[HYPERBOLIC_TABLE_SIZE + 1];
        
        /// <summary>
        /// Initialize lookup tables with precomputed values
        /// </summary>
        static FastMath()
        {
            InitializeAtmosphereLookupTable();
            InitializeHyperbolicLookupTables();
        }
        
        /// <summary>
        /// Initialize atmospheric exponential lookup table for density/pressure calculations
        /// </summary>
        private static void InitializeAtmosphereLookupTable()
        {
            for (int i = 0; i <= ATMOSPHERE_TABLE_SIZE; i++)
            {
                double altitude = i * ALTITUDE_STEP;
                double expValue = Math.Exp(-altitude / UNIVERSE_CONSTANTS.KERBIN_SCALE_HEIGHT);
                _expLookupTable[i] = (float)expValue;
            }
        }
        
        /// <summary>
        /// Initialize hyperbolic function lookup tables for orbital mechanics
        /// </summary>
        private static void InitializeHyperbolicLookupTables()
        {
            for (int i = 0; i <= HYPERBOLIC_TABLE_SIZE; i++)
            {
                double h = i * H_STEP;
                _sinhLookupTable[i] = (float)Math.Sinh(h);
                _coshLookupTable[i] = (float)Math.Cosh(h);
            }
        }
        
        /// <summary>
        /// Fast atmospheric exponential approximation using lookup table
        /// Replaces Math.Exp(-altitude / scaleHeight) for atmospheric calculations
        /// </summary>
        /// <param name="altitude">Altitude in meters (0 to 100km)</param>
        /// <returns>Exponential decay factor for atmospheric density/pressure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double FastAtmosphericExp(double altitude)
        {
            if (altitude <= 0.0)
                return 1.0;
            if (altitude >= MAX_ALTITUDE)
                return 0.0;
                
            // Linear interpolation in lookup table
            double index = altitude / ALTITUDE_STEP;
            int i = (int)index;
            double fraction = index - i;
            
            if (i >= ATMOSPHERE_TABLE_SIZE)
                return _expLookupTable[ATMOSPHERE_TABLE_SIZE];
                
            return _expLookupTable[i] + fraction * (_expLookupTable[i + 1] - _expLookupTable[i]);
        }
        
        /// <summary>
        /// Fast sinh approximation using lookup table for orbital mechanics
        /// Replaces Math.Sinh(H) in hyperbolic orbit calculations
        /// </summary>
        /// <param name="h">Hyperbolic eccentric anomaly (0 to 10)</param>
        /// <returns>Sinh value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double FastSinh(double h)
        {
            if (h <= 0.0)
                return h; // sinh(0) = 0, and for small values use linear approximation
            if (h >= MAX_H_VALUE)
                return Math.Sinh(h); // Fall back to exact calculation for extreme values
                
            // Linear interpolation in lookup table
            double index = h / H_STEP;
            int i = (int)index;
            double fraction = index - i;
            
            if (i >= HYPERBOLIC_TABLE_SIZE)
                return _sinhLookupTable[HYPERBOLIC_TABLE_SIZE];
                
            return _sinhLookupTable[i] + fraction * (_sinhLookupTable[i + 1] - _sinhLookupTable[i]);
        }
        
        /// <summary>
        /// Fast cosh approximation using lookup table for orbital mechanics
        /// Replaces Math.Cosh(H) in hyperbolic orbit calculations
        /// </summary>
        /// <param name="h">Hyperbolic eccentric anomaly (0 to 10)</param>
        /// <returns>Cosh value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double FastCosh(double h)
        {
            if (h <= 0.0)
                return 1.0; // cosh(0) = 1
            if (h >= MAX_H_VALUE)
                return Math.Cosh(h); // Fall back to exact calculation for extreme values
                
            // Linear interpolation in lookup table
            double index = h / H_STEP;
            int i = (int)index;
            double fraction = index - i;
            
            if (i >= HYPERBOLIC_TABLE_SIZE)
                return _coshLookupTable[HYPERBOLIC_TABLE_SIZE];
                
            return _coshLookupTable[i] + fraction * (_coshLookupTable[i + 1] - _coshLookupTable[i]);
        }
        
        /// <summary>
        /// Get performance statistics for the lookup tables
        /// </summary>
        public static string GetPerformanceInfo()
        {
            return $"FastMath Tables: Atmosphere({ATMOSPHERE_TABLE_SIZE} entries, 0-{MAX_ALTITUDE/1000:F0}km), " +
                   $"Hyperbolic({HYPERBOLIC_TABLE_SIZE} entries, 0-{MAX_H_VALUE:F1})";
        }
    }
}