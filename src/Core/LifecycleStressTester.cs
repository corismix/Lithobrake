using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text;
using Godot;

namespace Lithobrake.Core
{
    /// <summary>
    /// Comprehensive stress tester for the object lifecycle management system.
    /// Validates proper behavior under high load, memory leak prevention, and concurrency safety.
    /// </summary>
    public partial class LifecycleStressTester : Node3D
    {
        private readonly Random _random = new(42); // Deterministic seed for reproducible tests
        private readonly List<TestVessel> _testVessels = new();
        private readonly StringBuilder _testResults = new();
        
        private PhysicsManager? _physicsManager;
        private PerformanceMonitor? _performanceMonitor;
        
        // Test configuration
        private const int STRESS_TEST_CYCLES = 1000;
        private const int MEMORY_TEST_ITERATIONS = 100;
        private const int CONCURRENCY_THREADS = 4;
        private const int VESSELS_PER_THREAD = 25;
        
        public override void _Ready()
        {
            // Initialize managers
            _physicsManager = PhysicsManager.Instance;
            _performanceMonitor = PerformanceMonitor.Instance;
            
            GD.Print("LifecycleStressTester: Initialized for comprehensive lifecycle testing");
        }
        
        /// <summary>
        /// Runs a comprehensive stress test with rapid vessel creation/destruction cycles.
        /// Tests the lifecycle management system under high load.
        /// </summary>
        public async Task<string> RunStressTest(int cycles = STRESS_TEST_CYCLES)
        {
            var stopwatch = Stopwatch.StartNew();
            _testResults.Clear();
            
            long initialMemory = GC.GetTotalMemory(true);
            var initialStats = ObjectLifecycleManager.GetStatistics();
            int initialGCCount = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
            
            _testResults.AppendLine($"=== LIFECYCLE STRESS TEST ({cycles} cycles) ===");
            _testResults.AppendLine($"Initial Memory: {initialMemory / 1024.0 / 1024.0:F2} MB");
            _testResults.AppendLine($"Initial Tracked Objects: {initialStats.TrackedObjectCount}");
            _testResults.AppendLine($"Initial GC Count: {initialGCCount}");
            _testResults.AppendLine();
            
            int successfulCycles = 0;
            int failedCycles = 0;
            var exceptionCounts = new Dictionary<string, int>();
            
            try
            {
                for (int i = 0; i < cycles; i++)
                {
                    try
                    {
                        // Create test vessel
                        var vessel = CreateTestVessel($"StressTest_Vessel_{i}");
                        if (vessel != null)
                        {
                            _testVessels.Add(vessel);
                            
                            // Random operations
                            PerformRandomOperations(vessel);
                            
                            // Cleanup every 10 cycles to prevent excessive memory usage
                            if (i % 10 == 9)
                            {
                                CleanupTestVessels();
                            }
                            
                            successfulCycles++;
                        }
                        else
                        {
                            failedCycles++;
                        }
                    }
                    catch (Exception ex)
                    {
                        failedCycles++;
                        string exType = ex.GetType().Name;
                        exceptionCounts[exType] = exceptionCounts.GetValueOrDefault(exType, 0) + 1;
                        DebugLog.LogError($"Stress test cycle {i} failed: {ex.Message}");
                    }
                    
                    // Yield occasionally to prevent blocking
                    if (i % 100 == 99)
                    {
                        await Task.Yield();
                    }
                }
                
                // Final cleanup
                CleanupTestVessels();
                
                // Force garbage collection and wait for cleanup
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                // Allow lifecycle manager to process cleanup
                await Task.Yield();
                await Task.Yield();
                ObjectLifecycleManager.PerformCleanup();
            }
            catch (Exception ex)
            {
                _testResults.AppendLine($"CRITICAL FAILURE: {ex.Message}");
                return _testResults.ToString();
            }
            
            stopwatch.Stop();
            
            // Collect final statistics
            long finalMemory = GC.GetTotalMemory(true);
            var finalStats = ObjectLifecycleManager.GetStatistics();
            int finalGCCount = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
            
            // Calculate results
            long memoryDelta = finalMemory - initialMemory;
            long objectsDelta = finalStats.TrackedObjectCount - initialStats.TrackedObjectCount;
            int gcDelta = finalGCCount - initialGCCount;
            
            _testResults.AppendLine("=== STRESS TEST RESULTS ===");
            _testResults.AppendLine($"Test Duration: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
            _testResults.AppendLine($"Successful Cycles: {successfulCycles}/{cycles} ({(double)successfulCycles / cycles * 100:F1}%)");
            _testResults.AppendLine($"Failed Cycles: {failedCycles}");
            _testResults.AppendLine();
            
            _testResults.AppendLine("=== MEMORY ANALYSIS ===");
            _testResults.AppendLine($"Memory Delta: {memoryDelta / 1024.0 / 1024.0:F2} MB");
            _testResults.AppendLine($"Tracked Objects Delta: {objectsDelta}");
            _testResults.AppendLine($"GC Collections: {gcDelta}");
            _testResults.AppendLine($"Final Tracked Objects: {finalStats.TrackedObjectCount}");
            _testResults.AppendLine($"Total Disposed Objects: {finalStats.DisposedObjectCount}");
            _testResults.AppendLine();
            
            if (exceptionCounts.Count > 0)
            {
                _testResults.AppendLine("=== EXCEPTIONS ===");
                foreach (var kvp in exceptionCounts)
                {
                    _testResults.AppendLine($"{kvp.Key}: {kvp.Value} occurrences");
                }
                _testResults.AppendLine();
            }
            
            // Analyze test results
            bool memoryLeakDetected = memoryDelta > 50 * 1024 * 1024; // 50MB threshold
            bool objectLeakDetected = objectsDelta > 100; // Objects not cleaned up
            bool tooManyFailures = failedCycles > cycles * 0.05; // >5% failure rate
            
            _testResults.AppendLine("=== ANALYSIS ===");
            _testResults.AppendLine($"Memory Leak Detected: {(memoryLeakDetected ? "⚠️ YES" : "✅ NO")}");
            _testResults.AppendLine($"Object Leak Detected: {(objectLeakDetected ? "⚠️ YES" : "✅ NO")}");
            _testResults.AppendLine($"Excessive Failures: {(tooManyFailures ? "⚠️ YES" : "✅ NO")}");
            
            if (!memoryLeakDetected && !objectLeakDetected && !tooManyFailures)
            {
                _testResults.AppendLine("✅ STRESS TEST PASSED - No issues detected");
            }
            else
            {
                _testResults.AppendLine("❌ STRESS TEST FAILED - Issues detected");
            }
            
            return _testResults.ToString();
        }
        
        /// <summary>
        /// Tests for memory leaks by monitoring memory usage over multiple allocation cycles.
        /// </summary>
        public async Task<string> RunMemoryLeakTest()
        {
            _testResults.Clear();
            _testResults.AppendLine("=== MEMORY LEAK TEST ===");
            
            var memoryReadings = new List<long>();
            var objectCounts = new List<long>();
            
            // Baseline measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            for (int iteration = 0; iteration < MEMORY_TEST_ITERATIONS; iteration++)
            {
                // Create and destroy vessels
                var vessels = new List<TestVessel>();
                for (int i = 0; i < 20; i++)
                {
                    var vessel = CreateTestVessel($"MemTest_{iteration}_{i}");
                    if (vessel != null)
                        vessels.Add(vessel);
                }
                
                // Use vessels briefly
                foreach (var vessel in vessels)
                {
                    PerformRandomOperations(vessel);
                }
                
                // Clean up
                foreach (var vessel in vessels)
                {
                    vessel.Dispose();
                }
                vessels.Clear();
                
                // Force cleanup
                GC.Collect();
                GC.WaitForPendingFinalizers();
                ObjectLifecycleManager.PerformCleanup();
                
                // Record metrics
                long memory = GC.GetTotalMemory(false);
                var stats = ObjectLifecycleManager.GetStatistics();
                
                memoryReadings.Add(memory);
                objectCounts.Add(stats.TrackedObjectCount);
                
                if (iteration % 10 == 9)
                {
                    _testResults.AppendLine($"Iteration {iteration + 1}: {memory / 1024.0 / 1024.0:F2} MB, {stats.TrackedObjectCount} objects");
                }
            }
            
            // Analyze trend
            long initialMemory = memoryReadings[0];
            long finalMemory = memoryReadings[memoryReadings.Count - 1];
            long memoryGrowth = finalMemory - initialMemory;
            
            long initialObjects = objectCounts[0];
            long finalObjects = objectCounts[objectCounts.Count - 1];
            long objectGrowth = finalObjects - initialObjects;
            
            _testResults.AppendLine();
            _testResults.AppendLine("=== MEMORY LEAK ANALYSIS ===");
            _testResults.AppendLine($"Memory Growth: {memoryGrowth / 1024.0 / 1024.0:F2} MB");
            _testResults.AppendLine($"Object Growth: {objectGrowth} objects");
            
            bool memoryLeak = memoryGrowth > 10 * 1024 * 1024; // 10MB threshold
            bool objectLeak = objectGrowth > 50; // 50 objects threshold
            
            _testResults.AppendLine($"Memory Leak: {(memoryLeak ? "⚠️ DETECTED" : "✅ NOT DETECTED")}");
            _testResults.AppendLine($"Object Leak: {(objectLeak ? "⚠️ DETECTED" : "✅ NOT DETECTED")}");
            
            if (!memoryLeak && !objectLeak)
            {
                _testResults.AppendLine("✅ MEMORY LEAK TEST PASSED");
            }
            else
            {
                _testResults.AppendLine("❌ MEMORY LEAK TEST FAILED");
            }
            
            return _testResults.ToString();
        }
        
        /// <summary>
        /// Tests concurrent access to the lifecycle management system.
        /// </summary>
        public async Task<string> RunConcurrencyTest()
        {
            _testResults.Clear();
            _testResults.AppendLine("=== CONCURRENCY TEST ===");
            
            var tasks = new List<Task<TestResult>>();
            var stopwatch = Stopwatch.StartNew();
            
            // Create concurrent tasks
            for (int thread = 0; thread < CONCURRENCY_THREADS; thread++)
            {
                int threadId = thread;
                var task = Task.Run(() => ConcurrentWorker(threadId, VESSELS_PER_THREAD));
                tasks.Add(task);
            }
            
            // Wait for all tasks
            try
            {
                Task.WaitAll(tasks.ToArray());
            }
            catch (Exception ex)
            {
                _testResults.AppendLine($"CONCURRENCY TEST FAILED: {ex.Message}");
                return _testResults.ToString();
            }
            
            stopwatch.Stop();
            
            // Aggregate results
            int totalOperations = 0;
            int totalExceptions = 0;
            var exceptionTypes = new Dictionary<string, int>();
            
            foreach (var task in tasks)
            {
                var result = task.Result;
                totalOperations += result.OperationCount;
                totalExceptions += result.ExceptionCount;
                
                foreach (var kvp in result.ExceptionTypes)
                {
                    exceptionTypes[kvp.Key] = exceptionTypes.GetValueOrDefault(kvp.Key, 0) + kvp.Value;
                }
            }
            
            _testResults.AppendLine($"Duration: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
            _testResults.AppendLine($"Threads: {CONCURRENCY_THREADS}");
            _testResults.AppendLine($"Total Operations: {totalOperations}");
            _testResults.AppendLine($"Total Exceptions: {totalExceptions}");
            _testResults.AppendLine($"Success Rate: {(double)(totalOperations - totalExceptions) / totalOperations * 100:F1}%");
            
            if (exceptionTypes.Count > 0)
            {
                _testResults.AppendLine("\nException Types:");
                foreach (var kvp in exceptionTypes)
                {
                    _testResults.AppendLine($"  {kvp.Key}: {kvp.Value}");
                }
            }
            
            bool testPassed = totalExceptions < totalOperations * 0.01; // <1% failure rate
            _testResults.AppendLine($"\n{(testPassed ? "✅ CONCURRENCY TEST PASSED" : "❌ CONCURRENCY TEST FAILED")}");
            
            return _testResults.ToString();
        }
        
        private TestResult ConcurrentWorker(int threadId, int vesselCount)
        {
            var result = new TestResult();
            var localVessels = new List<TestVessel>();
            
            try
            {
                for (int i = 0; i < vesselCount; i++)
                {
                    try
                    {
                        var vessel = CreateTestVessel($"ConcurrentTest_{threadId}_{i}");
                        if (vessel != null)
                        {
                            localVessels.Add(vessel);
                            PerformRandomOperations(vessel);
                        }
                        result.OperationCount++;
                    }
                    catch (Exception ex)
                    {
                        result.ExceptionCount++;
                        string exType = ex.GetType().Name;
                        result.ExceptionTypes[exType] = result.ExceptionTypes.GetValueOrDefault(exType, 0) + 1;
                    }
                }
                
                // Cleanup
                foreach (var vessel in localVessels)
                {
                    try
                    {
                        vessel.Dispose();
                        result.OperationCount++;
                    }
                    catch (Exception ex)
                    {
                        result.ExceptionCount++;
                        string exType = ex.GetType().Name;
                        result.ExceptionTypes[exType] = result.ExceptionTypes.GetValueOrDefault(exType, 0) + 1;
                    }
                }
            }
            catch (Exception ex)
            {
                result.ExceptionCount++;
                result.ExceptionTypes[ex.GetType().Name] = result.ExceptionTypes.GetValueOrDefault(ex.GetType().Name, 0) + 1;
            }
            
            return result;
        }
        
        private TestVessel? CreateTestVessel(string name)
        {
            try
            {
                var rigidBody = new RigidBody3D();
                rigidBody.Name = name;
                
                var shape = new BoxShape3D();
                shape.Size = Vector3.One;
                
                var collisionShape = new CollisionShape3D();
                collisionShape.Shape = shape;
                rigidBody.AddChild(collisionShape);
                
                AddChild(rigidBody);
                
                var vessel = new TestVessel(rigidBody, name);
                return vessel;
            }
            catch (Exception ex)
            {
                DebugLog.LogError($"Failed to create test vessel {name}: {ex.Message}");
                return null;
            }
        }
        
        private void PerformRandomOperations(TestVessel vessel)
        {
            try
            {
                // Random operations to stress test the lifecycle system
                int operations = _random.Next(1, 5);
                
                for (int i = 0; i < operations; i++)
                {
                    int operation = _random.Next(0, 4);
                    
                    switch (operation)
                    {
                        case 0: // Access managed object
                            vessel.GetPosition();
                            break;
                        case 1: // Modify properties
                            vessel.SetActive(_random.Next(0, 2) == 0);
                            break;
                        case 2: // Check usability
                            vessel.IsUsable();
                            break;
                        case 3: // Add/remove from physics
                            vessel.TogglePhysics();
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog.LogError($"Error in random operations: {ex.Message}");
            }
        }
        
        private void CleanupTestVessels()
        {
            foreach (var vessel in _testVessels)
            {
                SafeOperations.SafeDispose(vessel, "CleanupTestVessels");
            }
            _testVessels.Clear();
        }
        
        public override void _ExitTree()
        {
            CleanupTestVessels();
            base._ExitTree();
        }
        
        private class TestResult
        {
            public int OperationCount { get; set; }
            public int ExceptionCount { get; set; }
            public Dictionary<string, int> ExceptionTypes { get; } = new();
        }
        
        /// <summary>
        /// Test vessel wrapper for stress testing
        /// </summary>
        private class TestVessel : IDisposable
        {
            private readonly ManagedGodotObject<RigidBody3D> _managedRigidBody;
            private readonly string _name;
            private bool _disposed = false;
            private bool _active = true;
            private int _physicsId = -1;
            
            public TestVessel(RigidBody3D rigidBody, string name)
            {
                _name = name;
                _managedRigidBody = SafeOperations.CreateManaged(rigidBody, name) ?? 
                    throw new ArgumentException("Failed to create managed rigid body");
            }
            
            public Vector3 GetPosition()
            {
                Vector3 position = Vector3.Zero;
                _managedRigidBody.TryExecute(body => position = body.GlobalPosition);
                return position;
            }
            
            public void SetActive(bool active)
            {
                _active = active;
                _managedRigidBody.TryExecute(body => body.Freeze = !active);
            }
            
            public bool IsUsable()
            {
                return !_disposed && _managedRigidBody.IsUsable;
            }
            
            public void TogglePhysics()
            {
                var physicsManager = PhysicsManager.Instance;
                if (physicsManager == null)
                    return;
                    
                if (_physicsId == -1)
                {
                    // Create a minimal physics vessel for testing
                    _managedRigidBody.TryExecute(body =>
                    {
                        var physicsVessel = new PhysicsVessel();
                        body.GetParent()?.AddChild(physicsVessel);
                        _physicsId = physicsManager.RegisterVessel(physicsVessel);
                    });
                }
                else
                {
                    physicsManager.UnregisterVessel(_physicsId);
                    _physicsId = -1;
                }
            }
            
            public void Dispose()
            {
                if (_disposed)
                    return;
                    
                _disposed = true;
                
                // Cleanup physics registration
                if (_physicsId != -1)
                {
                    var physicsManager = PhysicsManager.Instance;
                    physicsManager?.UnregisterVessel(_physicsId);
                }
                
                // Dispose managed object
                SafeOperations.SafeDispose(_managedRigidBody, $"TestVessel[{_name}]");
                
                GC.SuppressFinalize(this);
            }
            
            ~TestVessel()
            {
                Dispose();
            }
        }
    }
}