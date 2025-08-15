using Godot;

namespace Lithobrake.Core
{
    /// <summary>
    /// Wrapper class to make lifecycle tests accessible from GDScript.
    /// Provides a simple interface for running all test categories.
    /// </summary>
    public partial class LifecycleTestWrapper : Node
    {
        /// <summary>
        /// Runs all lifecycle management unit tests and returns the results.
        /// </summary>
        public string RunAllLifecycleTests()
        {
            try
            {
                DebugLog.Log("LifecycleTestWrapper: Starting comprehensive unit test suite");
                var results = LifecycleTests.RunAllTests();
                DebugLog.Log("LifecycleTestWrapper: Unit test suite completed");
                return results;
            }
            catch (System.Exception ex)
            {
                var errorMessage = $"Failed to run lifecycle tests: {ex.Message}";
                DebugLog.LogError($"LifecycleTestWrapper: {errorMessage}");
                return errorMessage;
            }
        }
        
        /// <summary>
        /// Gets the current statistics from the object lifecycle manager.
        /// </summary>
        public string GetLifecycleStatistics()
        {
            try
            {
                var stats = ObjectLifecycleManager.GetStatistics();
                return $"Tracked Objects: {stats.TrackedObjectCount}, " +
                       $"Disposed Objects: {stats.DisposedObjectCount}, " +
                       $"Active Objects: {stats.ActiveObjectCount}, " +
                       $"Callbacks: {stats.CallbackCount}";
            }
            catch (System.Exception ex)
            {
                return $"Error getting statistics: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Performs a cleanup of the lifecycle manager for testing purposes.
        /// </summary>
        public string PerformTestCleanup()
        {
            try
            {
                ObjectLifecycleManager.PerformCleanup();
                return "Lifecycle cleanup completed successfully";
            }
            catch (System.Exception ex)
            {
                return $"Cleanup failed: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Validates that the core systems are working properly.
        /// </summary>
        public string ValidateCoreSystemsIntegration()
        {
            var results = new System.Text.StringBuilder();
            results.AppendLine("=== CORE SYSTEMS INTEGRATION VALIDATION ===");
            
            try
            {
                // Check PhysicsManager
                var physicsManager = PhysicsManager.Instance;
                if (physicsManager != null && PhysicsManager.IsInstanceValid)
                {
                    results.AppendLine("✅ PhysicsManager: Available and valid");
                    var metrics = physicsManager.GetPhysicsMetrics();
                    results.AppendLine($"   - Physics Budget: {metrics.PhysicsBudget}ms");
                    results.AppendLine($"   - Average Physics Time: {metrics.AveragePhysicsTime:F2}ms");
                }
                else
                {
                    results.AppendLine("❌ PhysicsManager: Not available or invalid");
                }
                
                // Check PerformanceMonitor
                var performanceMonitor = PerformanceMonitor.Instance;
                if (performanceMonitor != null && PerformanceMonitor.IsInstanceValid)
                {
                    results.AppendLine("✅ PerformanceMonitor: Available and valid");
                    var perfMetrics = performanceMonitor.GetCurrentMetrics();
                    results.AppendLine($"   - FPS: {perfMetrics.FPS:F1}");
                    results.AppendLine($"   - Memory Usage: {perfMetrics.MemoryUsage:F1}MB");
                }
                else
                {
                    results.AppendLine("❌ PerformanceMonitor: Not available or invalid");
                }
                
                // Check ObjectLifecycleManager
                var lifecycleStats = ObjectLifecycleManager.GetStatistics();
                results.AppendLine("✅ ObjectLifecycleManager: Available");
                results.AppendLine($"   - Tracked Objects: {lifecycleStats.TrackedObjectCount}");
                results.AppendLine($"   - Active Objects: {lifecycleStats.ActiveObjectCount}");
                
                // Check SafeOperations
                bool safeOpsWorking = SafeOperations.TryExecute(() => {
                    // Simple test operation
                }, "IntegrationTest");
                results.AppendLine($"{(safeOpsWorking ? "✅" : "❌")} SafeOperations: Working properly");
                
                results.AppendLine();
                results.AppendLine("Core systems integration validation completed.");
                
            }
            catch (System.Exception ex)
            {
                results.AppendLine($"❌ Integration validation failed: {ex.Message}");
            }
            
            return results.ToString();
        }
    }
}