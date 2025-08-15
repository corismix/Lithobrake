using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Godot;

namespace Lithobrake.Core
{
    /// <summary>
    /// Comprehensive unit and integration tests for the object lifecycle management system.
    /// Validates proper behavior, memory management, and exception handling.
    /// </summary>
    public static class LifecycleTests
    {
        private static readonly StringBuilder _testOutput = new();
        private static int _testsRun = 0;
        private static int _testsPassed = 0;
        private static int _testsFailed = 0;
        
        /// <summary>
        /// Runs all lifecycle management tests and returns a comprehensive report.
        /// </summary>
        public static string RunAllTests()
        {
            _testOutput.Clear();
            _testsRun = _testsPassed = _testsFailed = 0;
            
            _testOutput.AppendLine("=== OBJECT LIFECYCLE MANAGEMENT TEST SUITE ===");
            _testOutput.AppendLine($"Start Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _testOutput.AppendLine();
            
            // Run all test categories
            RunBasicLifecycleTests();
            RunManagedObjectTests();
            RunSafeOperationsTests();
            RunPhysicsIntegrationTests();
            RunPerformanceTests();
            RunConcurrencyTests();
            RunErrorHandlingTests();
            
            // Generate summary
            _testOutput.AppendLine();
            _testOutput.AppendLine("=== TEST SUMMARY ===");
            _testOutput.AppendLine($"Tests Run: {_testsRun}");
            _testOutput.AppendLine($"Passed: {_testsPassed}");
            _testOutput.AppendLine($"Failed: {_testsFailed}");
            _testOutput.AppendLine($"Success Rate: {(double)_testsPassed / _testsRun * 100:F1}%");
            
            if (_testsFailed == 0)
            {
                _testOutput.AppendLine("✅ ALL TESTS PASSED - Lifecycle management system is working correctly");
            }
            else
            {
                _testOutput.AppendLine("❌ SOME TESTS FAILED - Review failures above");
            }
            
            return _testOutput.ToString();
        }
        
        private static void RunBasicLifecycleTests()
        {
            _testOutput.AppendLine("--- Basic Lifecycle Tests ---");
            
            TestCase("Object Registration", () =>
            {
                var rigidBody = new RigidBody3D();
                uint trackingId = ObjectLifecycleManager.RegisterObject(rigidBody, "TestRigidBody");
                
                Assert(trackingId > 0, "Tracking ID should be valid");
                Assert(ObjectLifecycleManager.IsObjectUsable(trackingId), "Newly registered object should be usable");
                Assert(ObjectLifecycleManager.GetObjectState(trackingId) == ObjectLifecycleManager.ObjectState.Created, 
                       "Object should be in Created state");
                
                rigidBody.QueueFree();
            });
            
            TestCase("Object State Transitions", () =>
            {
                var rigidBody = new RigidBody3D();
                uint trackingId = ObjectLifecycleManager.RegisterObject(rigidBody, "TestRigidBody");
                
                // Test state transitions
                ObjectLifecycleManager.MarkActive(trackingId);
                Assert(ObjectLifecycleManager.GetObjectState(trackingId) == ObjectLifecycleManager.ObjectState.Active,
                       "Object should transition to Active state");
                
                ObjectLifecycleManager.MarkDisposing(trackingId);
                Assert(ObjectLifecycleManager.GetObjectState(trackingId) == ObjectLifecycleManager.ObjectState.Disposing,
                       "Object should transition to Disposing state");
                Assert(!ObjectLifecycleManager.IsObjectUsable(trackingId), "Disposing object should not be usable");
                
                ObjectLifecycleManager.MarkDisposed(trackingId);
                Assert(ObjectLifecycleManager.GetObjectState(trackingId) == ObjectLifecycleManager.ObjectState.Disposed,
                       "Object should transition to Disposed state");
                
                rigidBody.QueueFree();
            });
            
            TestCase("Statistics Tracking", () =>
            {
                var initialStats = ObjectLifecycleManager.GetStatistics();
                
                var rigidBody = new RigidBody3D();
                uint trackingId = ObjectLifecycleManager.RegisterObject(rigidBody, "StatsTest");
                
                var statsAfterRegistration = ObjectLifecycleManager.GetStatistics();
                Assert(statsAfterRegistration.TrackedObjectCount > initialStats.TrackedObjectCount,
                       "Tracked object count should increase after registration");
                
                ObjectLifecycleManager.MarkDisposing(trackingId);
                ObjectLifecycleManager.MarkDisposed(trackingId);
                
                var statsAfterDisposal = ObjectLifecycleManager.GetStatistics();
                Assert(statsAfterDisposal.DisposedObjectCount > initialStats.DisposedObjectCount,
                       "Disposed object count should increase after disposal");
                
                rigidBody.QueueFree();
            });
        }
        
        private static void RunManagedObjectTests()
        {
            _testOutput.AppendLine("--- Managed Object Tests ---");
            
            TestCase("Managed Object Creation", () =>
            {
                var rigidBody = new RigidBody3D();
                var managed = SafeOperations.CreateManaged(rigidBody, "ManagedTest");
                
                Assert(managed != null, "Managed object should be created successfully");
                Assert(managed.IsUsable, "Managed object should be usable");
                Assert(managed.Object == rigidBody, "Managed object should reference original object");
                
                managed?.Dispose();
            });
            
            TestCase("Managed Object Disposal", () =>
            {
                var rigidBody = new RigidBody3D();
                var managed = SafeOperations.CreateManaged(rigidBody, "DisposalTest");
                
                Assert(managed != null, "Managed object should be created");
                
                managed!.Dispose();
                
                Assert(!managed.IsUsable, "Disposed managed object should not be usable");
                Assert(managed.Object == null, "Disposed managed object should return null");
            });
            
            TestCase("Managed Object Safe Execution", () =>
            {
                var rigidBody = new RigidBody3D();
                var managed = SafeOperations.CreateManaged(rigidBody, "ExecutionTest");
                
                bool actionExecuted = false;
                bool result = managed!.TryExecute(body =>
                {
                    actionExecuted = true;
                    body.Name = "TestExecuted";
                });
                
                Assert(result, "Action should execute successfully on usable object");
                Assert(actionExecuted, "Action should have been executed");
                Assert(rigidBody.Name.ToString() == "TestExecuted", "Action should have modified the object");
                
                managed.Dispose();
                
                // Test execution on disposed object
                bool disposedResult = managed.TryExecute(body => 
                {
                    body.Name = "ShouldNotExecute";
                });
                Assert(!disposedResult, "Action should not execute on disposed object");
            });
        }
        
        private static void RunSafeOperationsTests()
        {
            _testOutput.AppendLine("--- Safe Operations Tests ---");
            
            TestCase("Safe Validation", () =>
            {
                var rigidBody = new RigidBody3D();
                Assert(SafeOperations.IsValid(rigidBody, "ValidTest"), "Valid object should pass validation");
                
                Assert(!SafeOperations.IsValid(null, "NullTest"), "Null object should fail validation");
                
                rigidBody.QueueFree();
            });
            
            TestCase("Safe Execution with Exceptions", () =>
            {
                bool result = SafeOperations.TryExecute(() =>
                {
                    throw new InvalidOperationException("Test exception");
                }, "ExceptionTest");
                
                Assert(!result, "Exception should cause TryExecute to return false");
                
                var errors = SafeOperations.GetRecentErrors();
                Assert(errors.Length > 0, "Error should be recorded");
                
                SafeOperations.ClearErrorHistory();
                Assert(SafeOperations.GetRecentErrors().Length == 0, "Error history should be cleared");
            });
            
            TestCase("Safe Collection Operations", () =>
            {
                var validBodies = new List<RigidBody3D>
                {
                    new RigidBody3D(),
                    new RigidBody3D()
                };
                
                var mixedBodies = new List<RigidBody3D?>
                {
                    validBodies[0],
                    null,
                    validBodies[1]
                };
                
                Assert(SafeOperations.AreAllValid(validBodies, "AllValidTest"), "All valid objects should pass");
                Assert(!SafeOperations.AreAllValid(mixedBodies, "MixedTest"), "Mixed collection should fail");
                
                var filtered = SafeOperations.FilterValid(mixedBodies, "FilterTest").ToList();
                Assert(filtered.Count == 2, "Filter should return only valid objects");
                
                foreach (var body in validBodies)
                {
                    body.QueueFree();
                }
            });
        }
        
        private static void RunPhysicsIntegrationTests()
        {
            _testOutput.AppendLine("--- Physics Integration Tests ---");
            
            TestCase("PhysicsManager Integration", () =>
            {
                var physicsManager = PhysicsManager.Instance;
                Assert(physicsManager != null, "PhysicsManager instance should be available");
                Assert(PhysicsManager.IsInstanceValid, "PhysicsManager should be valid");
                
                var metrics = physicsManager.GetPhysicsMetrics();
                Assert(metrics.FixedDeltaTime > 0, "Physics delta time should be positive");
                Assert(metrics.PhysicsBudget > 0, "Physics budget should be positive");
            });
            
            TestCase("Vessel Registration Lifecycle", () =>
            {
                var physicsManager = PhysicsManager.Instance;
                var vessel = new PhysicsVessel();
                
                int vesselId = physicsManager.RegisterVessel(vessel);
                Assert(vesselId > 0, "Vessel registration should return valid ID");
                
                var retrievedVessel = physicsManager.GetVessel(vesselId);
                Assert(retrievedVessel == vessel, "Retrieved vessel should match registered vessel");
                
                physicsManager.UnregisterVessel(vesselId);
                
                var nullVessel = physicsManager.GetVessel(vesselId);
                Assert(nullVessel == null, "Unregistered vessel should return null");
                
                vessel.QueueFree();
            });
        }
        
        private static void RunPerformanceTests()
        {
            _testOutput.AppendLine("--- Performance Tests ---");
            
            TestCase("Lifecycle Performance", () =>
            {
                const int iterations = 1000;
                var stopwatch = Stopwatch.StartNew();
                
                var objects = new List<ManagedGodotObject<RigidBody3D>>();
                
                // Test creation performance
                for (int i = 0; i < iterations; i++)
                {
                    var body = new RigidBody3D();
                    var managed = SafeOperations.CreateManaged(body, $"PerfTest_{i}");
                    if (managed != null)
                        objects.Add(managed);
                }
                
                stopwatch.Stop();
                double creationTime = stopwatch.Elapsed.TotalMilliseconds;
                
                Assert(creationTime < 100, $"Creation of {iterations} objects took too long: {creationTime:F2}ms");
                
                // Test disposal performance
                stopwatch.Restart();
                foreach (var obj in objects)
                {
                    obj.Dispose();
                }
                stopwatch.Stop();
                
                double disposalTime = stopwatch.Elapsed.TotalMilliseconds;
                Assert(disposalTime < 50, $"Disposal of {iterations} objects took too long: {disposalTime:F2}ms");
            });
            
            TestCase("Memory Usage Validation", () =>
            {
                long initialMemory = GC.GetTotalMemory(true);
                
                // Create and dispose many objects
                for (int i = 0; i < 100; i++)
                {
                    var body = new RigidBody3D();
                    var managed = SafeOperations.CreateManaged(body, $"MemTest_{i}");
                    managed?.Dispose();
                }
                
                GC.Collect();
                GC.WaitForPendingFinalizers();
                ObjectLifecycleManager.PerformCleanup();
                
                long finalMemory = GC.GetTotalMemory(true);
                long memoryDelta = finalMemory - initialMemory;
                
                Assert(memoryDelta < 5 * 1024 * 1024, $"Memory usage increased too much: {memoryDelta / 1024.0 / 1024.0:F2} MB");
            });
        }
        
        private static void RunConcurrencyTests()
        {
            _testOutput.AppendLine("--- Concurrency Tests ---");
            
            TestCase("Concurrent Object Creation", () =>
            {
                const int threadsCount = 4;
                const int objectsPerThread = 25;
                var tasks = new List<Task<bool>>();
                
                for (int t = 0; t < threadsCount; t++)
                {
                    int threadId = t;
                    var task = Task.Run(() =>
                    {
                        try
                        {
                            var localObjects = new List<ManagedGodotObject<RigidBody3D>>();
                            
                            for (int i = 0; i < objectsPerThread; i++)
                            {
                                var body = new RigidBody3D();
                                var managed = SafeOperations.CreateManaged(body, $"Concurrent_{threadId}_{i}");
                                if (managed != null)
                                    localObjects.Add(managed);
                            }
                            
                            // Cleanup
                            foreach (var obj in localObjects)
                            {
                                obj.Dispose();
                            }
                            
                            return true;
                        }
                        catch (Exception ex)
                        {
                            DebugLog.LogError($"Concurrency test thread {threadId} failed: {ex.Message}");
                            return false;
                        }
                    });
                    
                    tasks.Add(task);
                }
                
                Task.WaitAll(tasks.ToArray());
                
                bool allSucceeded = tasks.All(t => t.Result);
                Assert(allSucceeded, "All concurrent threads should complete successfully");
            });
        }
        
        private static void RunErrorHandlingTests()
        {
            _testOutput.AppendLine("--- Error Handling Tests ---");
            
            TestCase("Null Reference Protection", () =>
            {
                var managed = SafeOperations.CreateManaged<RigidBody3D>(null, "NullTest");
                Assert(managed == null, "Creating managed object from null should return null");
                
                bool result = SafeOperations.TryUseManaged<RigidBody3D>(null, body => { }, "NullUseTest");
                Assert(!result, "Using null managed object should return false");
            });
            
            TestCase("Invalid Object Handling", () =>
            {
                var body = new RigidBody3D();
                var managed = SafeOperations.CreateManaged(body, "InvalidTest");
                
                // Dispose the managed object
                managed!.Dispose();
                
                bool actionResult = managed.TryExecute(b => 
                {
                    b.Name = "ShouldNotWork";
                });
                Assert(!actionResult, "Action on disposed object should fail");
                
                var funcResult = managed.TryExecute(b => b.Name.ToString());
                Assert(funcResult == null, "Function on disposed object should return null");
            });
        }
        
        private static void TestCase(string testName, Action testAction)
        {
            _testsRun++;
            
            try
            {
                testAction();
                _testsPassed++;
                _testOutput.AppendLine($"✅ {testName}");
            }
            catch (Exception ex)
            {
                _testsFailed++;
                _testOutput.AppendLine($"❌ {testName}: {ex.Message}");
                DebugLog.LogError($"Test '{testName}' failed: {ex.Message}");
            }
        }
        
        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new AssertionException(message);
            }
        }
        
        private class AssertionException : Exception
        {
            public AssertionException(string message) : base(message) { }
        }
    }
}