using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Lithobrake.Core
{
    /// <summary>
    /// Memory management utilities for tracking C# GC impact and implementing object pooling
    /// to maintain 60 FPS performance targets. Validates memory allocation patterns during typical operations.
    /// </summary>
    public static class MemoryManager
    {
        // Object pools to minimize GC pressure
        private static Stack<Double3[]> _double3ArrayPool = new Stack<Double3[]>();
        private static Stack<Vector3[]> _vector3ArrayPool = new Stack<Vector3[]>();
        private static Stack<Godot.Collections.Dictionary> _dictionaryPool = new Stack<Godot.Collections.Dictionary>();
        
        // Memory tracking
        private static long _lastMemoryUsage = 0;
        private static int _lastGCCount = 0;
        private static List<GCMemoryInfo> _gcHistory = new List<GCMemoryInfo>();
        
        // Performance thresholds
        private const long MAX_ALLOCATION_PER_FRAME = 1024 * 1024; // 1MB per frame
        private const int MAX_GC_COLLECTIONS_PER_SECOND = 5;
        private const double GC_WARNING_THRESHOLD = 2.0; // ms
        
        /// <summary>
        /// Initialize memory management system
        /// </summary>
        public static void Initialize()
        {
            _lastMemoryUsage = GC.GetTotalMemory(false);
            _lastGCCount = GetTotalGCCount();
            
            // Pre-populate pools to avoid initial allocations
            PreallocatePools();
            
            GD.Print("MemoryManager: Initialized with object pools");
            LogMemoryStatus();
        }
        
        private static void PreallocatePools()
        {
            // Pre-allocate common array sizes for physics calculations
            for (int i = 0; i < 5; i++)
            {
                _double3ArrayPool.Push(new Double3[100]);  // Common orbital calculation size
                _vector3ArrayPool.Push(new Vector3[100]);
                _dictionaryPool.Push(new Godot.Collections.Dictionary());
            }
            
            GD.Print($"MemoryManager: Pre-allocated {_double3ArrayPool.Count} object pools");
        }
        
        /// <summary>
        /// Get a pooled Double3 array to minimize allocations
        /// </summary>
        public static Double3[] GetDouble3Array(int minSize)
        {
            if (_double3ArrayPool.Count > 0)
            {
                var array = _double3ArrayPool.Pop();
                if (array.Length >= minSize)
                {
                    Array.Clear(array, 0, array.Length);
                    return array;
                }
            }
            
            return new Double3[Math.Max(minSize, 100)];
        }
        
        /// <summary>
        /// Return Double3 array to pool
        /// </summary>
        public static void ReturnDouble3Array(Double3[] array)
        {
            if (array != null && _double3ArrayPool.Count < 10)
            {
                _double3ArrayPool.Push(array);
            }
        }
        
        /// <summary>
        /// Get a pooled Vector3 array to minimize allocations
        /// </summary>
        public static Vector3[] GetVector3Array(int minSize)
        {
            if (_vector3ArrayPool.Count > 0)
            {
                var array = _vector3ArrayPool.Pop();
                if (array.Length >= minSize)
                {
                    Array.Clear(array, 0, array.Length);
                    return array;
                }
            }
            
            return new Vector3[Math.Max(minSize, 100)];
        }
        
        /// <summary>
        /// Return Vector3 array to pool
        /// </summary>
        public static void ReturnVector3Array(Vector3[] array)
        {
            if (array != null && _vector3ArrayPool.Count < 10)
            {
                _vector3ArrayPool.Push(array);
            }
        }
        
        /// <summary>
        /// Get a pooled Dictionary to minimize allocations
        /// </summary>
        public static Godot.Collections.Dictionary GetDictionary()
        {
            if (_dictionaryPool.Count > 0)
            {
                var dict = _dictionaryPool.Pop();
                dict.Clear();
                return dict;
            }
            
            return new Godot.Collections.Dictionary();
        }
        
        /// <summary>
        /// Return Dictionary to pool
        /// </summary>
        public static void ReturnDictionary(Godot.Collections.Dictionary dictionary)
        {
            if (dictionary != null && _dictionaryPool.Count < 10)
            {
                dictionary.Clear();
                _dictionaryPool.Push(dictionary);
            }
        }
        
        /// <summary>
        /// Validate memory allocation patterns and GC impact
        /// </summary>
        public static MemoryValidationResult ValidateMemoryPerformance()
        {
            var stopwatch = Stopwatch.StartNew();
            
            long currentMemory = GC.GetTotalMemory(false);
            int currentGCCount = GetTotalGCCount();
            
            // Calculate memory allocation since last check
            long memoryDelta = currentMemory - _lastMemoryUsage;
            int gcDelta = currentGCCount - _lastGCCount;
            
            // Test allocation patterns
            var testResults = RunAllocationTests();
            
            stopwatch.Stop();
            
            var result = new MemoryValidationResult
            {
                CurrentMemoryUsage = currentMemory,
                MemoryDelta = memoryDelta,
                GcCollectionsDelta = gcDelta,
                ValidationTime = stopwatch.Elapsed.TotalMilliseconds,
                AllocationTestResults = testResults,
                MemoryWithinLimits = memoryDelta < MAX_ALLOCATION_PER_FRAME,
                GcWithinLimits = gcDelta == 0, // No GC during normal frame
                PoolEfficiency = CalculatePoolEfficiency()
            };
            
            _lastMemoryUsage = currentMemory;
            _lastGCCount = currentGCCount;
            
            // Log warnings if thresholds exceeded
            if (!result.MemoryWithinLimits)
            {
                GD.PrintErr($"⚠️ MEMORY WARNING: Allocated {memoryDelta} bytes (limit: {MAX_ALLOCATION_PER_FRAME})");
            }
            
            if (!result.GcWithinLimits)
            {
                GD.PrintErr($"⚠️ GC WARNING: {gcDelta} collections occurred");
            }
            
            return result;
        }
        
        private static AllocationTestResults RunAllocationTests()
        {
            var results = new AllocationTestResults();
            
            // Test 1: Double3 array allocation with pooling
            var sw1 = Stopwatch.StartNew();
            var double3Array = GetDouble3Array(100);
            sw1.Stop();
            results.PooledAllocationTime = sw1.Elapsed.TotalMilliseconds;
            ReturnDouble3Array(double3Array);
            
            // Test 2: Direct allocation (no pooling)
            var sw2 = Stopwatch.StartNew();
            var directArray = new Double3[100];
            sw2.Stop();
            results.DirectAllocationTime = sw2.Elapsed.TotalMilliseconds;
            
            // Test 3: Vector3 conversion performance with pooling
            var sw3 = Stopwatch.StartNew();
            var vector3Array = GetVector3Array(100);
            Double3.FastConvertToVector3Array(double3Array, vector3Array, 0, 100);
            sw3.Stop();
            results.ConversionWithPoolingTime = sw3.Elapsed.TotalMilliseconds;
            ReturnVector3Array(vector3Array);
            
            // Test 4: Dictionary allocation with pooling
            var sw4 = Stopwatch.StartNew();
            var dict = GetDictionary();
            dict["test"] = 42;
            sw4.Stop();
            results.DictionaryPoolingTime = sw4.Elapsed.TotalMilliseconds;
            ReturnDictionary(dict);
            
            return results;
        }
        
        private static double CalculatePoolEfficiency()
        {
            // Simple efficiency metric based on pool utilization
            int totalPoolSize = _double3ArrayPool.Count + _vector3ArrayPool.Count + _dictionaryPool.Count;
            int maxPoolSize = 30; // 3 pools × 10 max items each
            
            return (double)totalPoolSize / maxPoolSize;
        }
        
        private static int GetTotalGCCount()
        {
            return GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
        }
        
        /// <summary>
        /// Force garbage collection and measure impact
        /// </summary>
        public static GCImpactResult MeasureGCImpact()
        {
            var stopwatch = Stopwatch.StartNew();
            long memoryBefore = GC.GetTotalMemory(false);
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            long memoryAfter = GC.GetTotalMemory(true);
            stopwatch.Stop();
            
            var result = new GCImpactResult
            {
                MemoryBefore = memoryBefore,
                MemoryAfter = memoryAfter,
                MemoryReclaimed = memoryBefore - memoryAfter,
                GcTime = stopwatch.Elapsed.TotalMilliseconds,
                WithinTimeLimit = stopwatch.Elapsed.TotalMilliseconds < GC_WARNING_THRESHOLD
            };
            
            GD.Print($"GC Impact: Reclaimed {result.MemoryReclaimed} bytes in {result.GcTime:F3}ms");
            
            if (!result.WithinTimeLimit)
            {
                GD.PrintErr($"⚠️ GC TIME WARNING: {result.GcTime:F3}ms exceeds {GC_WARNING_THRESHOLD}ms threshold");
            }
            
            return result;
        }
        
        /// <summary>
        /// Log current memory status for debugging
        /// </summary>
        public static void LogMemoryStatus()
        {
            long totalMemory = GC.GetTotalMemory(false);
            int totalGC = GetTotalGCCount();
            
            GD.Print($"Memory Status:");
            GD.Print($"  Total Memory: {totalMemory / (1024.0 * 1024.0):F2}MB");
            GD.Print($"  GC Collections: {totalGC}");
            GD.Print($"  Pool Status - Double3: {_double3ArrayPool.Count}, Vector3: {_vector3ArrayPool.Count}, Dict: {_dictionaryPool.Count}");
        }
    }
    
    /// <summary>
    /// Results from memory validation tests
    /// </summary>
    public struct MemoryValidationResult
    {
        public long CurrentMemoryUsage;
        public long MemoryDelta;
        public int GcCollectionsDelta;
        public double ValidationTime;
        public AllocationTestResults AllocationTestResults;
        public bool MemoryWithinLimits;
        public bool GcWithinLimits;
        public double PoolEfficiency;
    }
    
    /// <summary>
    /// Results from allocation performance tests
    /// </summary>
    public struct AllocationTestResults
    {
        public double PooledAllocationTime;
        public double DirectAllocationTime;
        public double ConversionWithPoolingTime;
        public double DictionaryPoolingTime;
    }
    
    /// <summary>
    /// Results from GC impact measurement
    /// </summary>
    public struct GCImpactResult
    {
        public long MemoryBefore;
        public long MemoryAfter;
        public long MemoryReclaimed;
        public double GcTime;
        public bool WithinTimeLimit;
    }
}