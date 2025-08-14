using Godot;
using System;

namespace Lithobrake.Core
{
    /// <summary>
    /// Double precision 3D vector struct for high-precision orbital mechanics calculations.
    /// Provides conversion utilities to/from Godot.Vector3 for rendering operations.
    /// </summary>
    public struct Double3 : IEquatable<Double3>
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Double3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Double3(double value)
        {
            X = Y = Z = value;
        }

        // Vector operations
        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
        public double LengthSquared => X * X + Y * Y + Z * Z;
        public Double3 Normalized => this / Length;

        // Conversion utilities for Godot interop
        public Vector3 ToVector3() => new Vector3((float)X, (float)Y, (float)Z);
        public static Double3 FromVector3(Vector3 v) => new Double3(v.X, v.Y, v.Z);

        // Performance-critical batch conversion methods
        public static void ConvertToVector3Array(Double3[] source, Vector3[] destination, int count)
        {
            for (int i = 0; i < count; i++)
            {
                destination[i] = source[i].ToVector3();
            }
        }

        public static void ConvertFromVector3Array(Vector3[] source, Double3[] destination, int count)
        {
            for (int i = 0; i < count; i++)
            {
                destination[i] = FromVector3(source[i]);
            }
        }
        
        // Optimized conversion methods for minimal overhead
        /// <summary>
        /// Fast conversion with pre-allocated arrays to minimize GC pressure
        /// </summary>
        public static void FastConvertToVector3Array(Double3[] source, Vector3[] destination, int startIndex, int count)
        {
            int endIndex = startIndex + count;
            for (int i = startIndex; i < endIndex; i++)
            {
                var src = source[i];
                destination[i] = new Vector3((float)src.X, (float)src.Y, (float)src.Z);
            }
        }
        
        /// <summary>
        /// In-place conversion avoiding temporary allocations
        /// </summary>
        public static unsafe void UnsafeConvertToVector3Array(Double3[] source, Vector3[] destination, int count)
        {
            for (int i = 0; i < count; i++)
            {
                ref var src = ref source[i];
                destination[i] = new Vector3((float)src.X, (float)src.Y, (float)src.Z);
            }
        }
        
        /// <summary>
        /// Benchmark conversion performance for target <0.1ms per 1000 operations
        /// </summary>
        public static double BenchmarkConversions(int iterations = 1000)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var double3Array = new Double3[iterations];
            var vector3Array = new Vector3[iterations];
            
            // Initialize with orbital-scale values
            var random = new Random(42);
            for (int i = 0; i < iterations; i++)
            {
                double3Array[i] = new Double3(
                    random.NextDouble() * 1000000,  // Large orbital distances
                    random.NextDouble() * 1000000,
                    random.NextDouble() * 1000000
                );
            }
            
            stopwatch.Restart();
            
            // Test round-trip conversions
            for (int i = 0; i < iterations; i++)
            {
                vector3Array[i] = double3Array[i].ToVector3();
                double3Array[i] = FromVector3(vector3Array[i]);
            }
            
            stopwatch.Stop();
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        // Arithmetic operators
        public static Double3 operator +(Double3 a, Double3 b) => new Double3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Double3 operator -(Double3 a, Double3 b) => new Double3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Double3 operator -(Double3 a) => new Double3(-a.X, -a.Y, -a.Z);
        public static Double3 operator *(Double3 a, double s) => new Double3(a.X * s, a.Y * s, a.Z * s);
        public static Double3 operator *(double s, Double3 a) => new Double3(a.X * s, a.Y * s, a.Z * s);
        public static Double3 operator /(Double3 a, double s) => new Double3(a.X / s, a.Y / s, a.Z / s);

        // Vector operations
        public static double Dot(Double3 a, Double3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static Double3 Cross(Double3 a, Double3 b) => new Double3(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X
        );

        // Orbital mechanics utilities
        public static double Distance(Double3 a, Double3 b) => (a - b).Length;
        public static Double3 Lerp(Double3 a, Double3 b, double t) => a + (b - a) * t;
        public static Double3 Project(Double3 vector, Double3 onNormal) => onNormal * (Dot(vector, onNormal) / onNormal.LengthSquared);
        public static Double3 Reflect(Double3 vector, Double3 normal) => vector - 2 * Project(vector, normal);
        
        /// <summary>
        /// Calculate orbital velocity magnitude at given radius for circular orbit
        /// </summary>
        public static double CircularVelocity(double radius, double gravitationalParameter)
        {
            return Math.Sqrt(gravitationalParameter / radius);
        }
        
        /// <summary>
        /// Calculate specific orbital energy (energy per unit mass)
        /// </summary>
        public static double SpecificOrbitalEnergy(Double3 position, Double3 velocity, double gravitationalParameter)
        {
            double r = position.Length;
            double v = velocity.Length;
            return (v * v) / 2 - gravitationalParameter / r;
        }
        
        /// <summary>
        /// Calculate angular momentum vector for orbital mechanics
        /// </summary>
        public static Double3 AngularMomentum(Double3 position, Double3 velocity)
        {
            return Cross(position, velocity);
        }
        
        /// <summary>
        /// Calculate eccentricity vector for orbital mechanics
        /// </summary>
        public static Double3 EccentricityVector(Double3 position, Double3 velocity, double gravitationalParameter)
        {
            double r = position.Length;
            double v_squared = velocity.LengthSquared;
            Double3 h = AngularMomentum(position, velocity);
            
            return (Cross(velocity, h) / gravitationalParameter) - (position / r);
        }

        // Equality and comparison
        public bool Equals(Double3 other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object? obj) => obj is Double3 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        public static bool operator ==(Double3 a, Double3 b) => a.Equals(b);
        public static bool operator !=(Double3 a, Double3 b) => !a.Equals(b);

        // Common vectors
        public static readonly Double3 Zero = new Double3(0, 0, 0);
        public static readonly Double3 One = new Double3(1, 1, 1);
        public static readonly Double3 Up = new Double3(0, 1, 0);
        public static readonly Double3 Right = new Double3(1, 0, 0);
        public static readonly Double3 Forward = new Double3(0, 0, -1);

        public override string ToString() => $"({X:F6}, {Y:F6}, {Z:F6})";
        public string ToString(string format) => $"({X.ToString(format)}, {Y.ToString(format)}, {Z.ToString(format)})";
    }
}