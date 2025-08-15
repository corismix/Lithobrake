using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using Godot;

namespace Lithobrake.Core
{
    /// <summary>
    /// Centralized exception handling system for critical operations.
    /// Provides safe execution wrappers to prevent crashes and implement graceful degradation.
    /// </summary>
    public static class SafeOperations
    {
        private static readonly object _logLock = new();
        private static readonly Queue<ErrorInfo> _recentErrors = new(100);
        private static readonly Dictionary<string, DateTime> _lastLogTimes = new();
        private static readonly TimeSpan _logThrottleInterval = TimeSpan.FromSeconds(1);
        
        /// <summary>
        /// Maximum number of errors to keep in memory for diagnostics
        /// </summary>
        private const int MAX_ERROR_HISTORY = 100;

        /// <summary>
        /// Executes an action safely, catching and logging any exceptions.
        /// Returns true if the operation succeeded, false otherwise.
        /// </summary>
        /// <param name="operation">The operation to execute</param>
        /// <param name="context">Context information for error reporting</param>
        /// <returns>True if successful, false if an exception occurred</returns>
        public static bool TryExecute(Action operation, string context = "")
        {
            try
            {
                operation();
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Operation failed in {context}: {ex.Message}", ex, context);
                return false;
            }
        }

        /// <summary>
        /// Executes a function safely, catching and logging any exceptions.
        /// Returns the result if successful, null if an exception occurred.
        /// </summary>
        /// <typeparam name="T">Return type of the function</typeparam>
        /// <param name="operation">The function to execute</param>
        /// <param name="context">Context information for error reporting</param>
        /// <returns>Function result if successful, null if an exception occurred</returns>
        public static T? TryExecute<T>(Func<T> operation, string context = "") where T : class
        {
            try
            {
                return operation();
            }
            catch (Exception ex)
            {
                LogError($"Operation failed in {context}: {ex.Message}", ex, context);
                return null;
            }
        }

        /// <summary>
        /// Executes a function safely, returning a default value on failure.
        /// </summary>
        /// <typeparam name="T">Return type of the function</typeparam>
        /// <param name="operation">The function to execute</param>
        /// <param name="defaultValue">Value to return on failure</param>
        /// <param name="context">Context information for error reporting</param>
        /// <returns>Function result if successful, defaultValue if an exception occurred</returns>
        public static T TryExecuteWithDefault<T>(Func<T> operation, T defaultValue, string context = "")
        {
            try
            {
                return operation();
            }
            catch (Exception ex)
            {
                LogError($"Operation failed in {context}: {ex.Message}", ex, context);
                return defaultValue;
            }
        }

        /// <summary>
        /// Executes an operation with throttled logging to prevent log spam.
        /// Only logs errors once per throttle interval per context.
        /// </summary>
        /// <param name="operation">The operation to execute</param>
        /// <param name="context">Context information for error reporting</param>
        /// <returns>True if successful, false if an exception occurred</returns>
        public static bool TryExecuteThrottled(Action operation, string context = "")
        {
            try
            {
                operation();
                return true;
            }
            catch (Exception ex)
            {
                LogErrorThrottled($"Operation failed in {context}: {ex.Message}", ex, context);
                return false;
            }
        }

        /// <summary>
        /// Gets recent error history for diagnostics.
        /// Thread-safe read-only access to error information.
        /// </summary>
        /// <returns>Array of recent error information</returns>
        public static ErrorInfo[] GetRecentErrors()
        {
            lock (_logLock)
            {
                return _recentErrors.ToArray();
            }
        }

        /// <summary>
        /// Clears the error history. Useful for tests or diagnostics.
        /// </summary>
        public static void ClearErrorHistory()
        {
            lock (_logLock)
            {
                _recentErrors.Clear();
                _lastLogTimes.Clear();
            }
        }

        /// <summary>
        /// Validates a Godot object before use.
        /// Returns true if the object is valid and can be safely used.
        /// </summary>
        /// <param name="godotObject">The Godot object to validate</param>
        /// <param name="objectName">Name of the object for error reporting</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool IsValid(GodotObject? godotObject, string objectName = "Object")
        {
            if (godotObject == null)
            {
                DebugLog.LogError($"{objectName} is null");
                return false;
            }

            if (!GodotObject.IsInstanceValid(godotObject))
            {
                DebugLog.LogError($"{objectName} is not a valid instance");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates a node before use, including null and instance validity checks.
        /// </summary>
        /// <param name="node">The node to validate</param>
        /// <param name="nodeName">Name of the node for error reporting</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool IsValidNode(Node? node, string nodeName = "Node")
        {
            if (!IsValid(node, nodeName))
                return false;

            // Additional node-specific validation could be added here
            // For example, checking if node is in the scene tree
            
            return true;
        }

        /// <summary>
        /// Creates a managed Godot object with automatic lifecycle tracking.
        /// Eliminates the need for manual IsInstanceValid() checks.
        /// </summary>
        /// <typeparam name="T">Type of Godot object</typeparam>
        /// <param name="obj">The object to wrap</param>
        /// <param name="name">Optional name for debugging</param>
        /// <returns>Managed wrapper or null if obj is invalid</returns>
        public static ManagedGodotObject<T>? CreateManaged<T>(T? obj, string name = "") where T : GodotObject
        {
            if (!IsValid(obj, name))
                return null;
                
            return ObjectLifecycleManager.CreateManagedObject(obj!, name);
        }

        /// <summary>
        /// Safely accesses a managed object and executes an action if the object is usable.
        /// </summary>
        /// <typeparam name="T">Type of Godot object</typeparam>
        /// <param name="managedObj">The managed object wrapper</param>
        /// <param name="action">Action to execute</param>
        /// <param name="context">Context for error reporting</param>
        /// <returns>True if action was executed successfully</returns>
        public static bool TryUseManaged<T>(ManagedGodotObject<T>? managedObj, Action<T> action, string context = "") where T : GodotObject
        {
            if (managedObj == null || !managedObj.IsUsable)
                return false;
                
            return managedObj.TryExecute(action);
        }

        /// <summary>
        /// Safely accesses a managed object and executes a function if the object is usable.
        /// </summary>
        /// <typeparam name="T">Type of Godot object</typeparam>
        /// <typeparam name="TResult">Return type</typeparam>
        /// <param name="managedObj">The managed object wrapper</param>
        /// <param name="func">Function to execute</param>
        /// <param name="defaultValue">Value to return if object not usable</param>
        /// <param name="context">Context for error reporting</param>
        /// <returns>Function result or default value</returns>
        public static TResult TryUseManagedWithDefault<T, TResult>(ManagedGodotObject<T>? managedObj, Func<T, TResult> func, TResult defaultValue, string context = "") where T : GodotObject where TResult : class
        {
            if (managedObj == null || !managedObj.IsUsable)
                return defaultValue;
                
            var result = managedObj.TryExecute(func);
            return result ?? defaultValue;
        }

        /// <summary>
        /// Safely accesses a managed object and executes a function if the object is usable (for value types).
        /// </summary>
        /// <typeparam name="T">Type of Godot object</typeparam>
        /// <typeparam name="TResult">Return type (value type)</typeparam>
        /// <param name="managedObj">The managed object wrapper</param>
        /// <param name="func">Function to execute</param>
        /// <param name="defaultValue">Value to return if object not usable</param>
        /// <param name="context">Context for error reporting</param>
        /// <returns>Function result or default value</returns>
        public static TResult TryUseManagedWithDefaultValue<T, TResult>(ManagedGodotObject<T>? managedObj, Func<T, TResult> func, TResult defaultValue, string context = "") where T : GodotObject where TResult : struct
        {
            if (managedObj == null || !managedObj.IsUsable)
                return defaultValue;
                
            TResult result = defaultValue;
            managedObj.TryExecute(obj => result = func(obj));
            return result;
        }

        /// <summary>
        /// Performs a safe disposal operation with proper lifecycle tracking.
        /// </summary>
        /// <param name="disposable">Object to dispose</param>
        /// <param name="context">Context for error reporting</param>
        /// <returns>True if disposal succeeded</returns>
        public static bool SafeDispose(IDisposable? disposable, string context = "")
        {
            if (disposable == null)
                return true;
                
            return TryExecute(() => disposable.Dispose(), $"SafeDispose[{context}]");
        }

        /// <summary>
        /// Safely queues a Godot object for deletion with lifecycle tracking.
        /// </summary>
        /// <param name="obj">Object to queue for deletion</param>
        /// <param name="context">Context for error reporting</param>
        /// <returns>True if queuing succeeded</returns>
        public static bool SafeQueueFree(GodotObject? obj, string context = "")
        {
            if (obj == null)
                return true;
                
            if (!IsValid(obj, context))
                return false;
                
            return TryExecute(() => 
            {
                if (obj is Node node)
                    node.QueueFree();
            }, $"SafeQueueFree[{context}]");
        }

        /// <summary>
        /// Validates that a collection of Godot objects are all valid.
        /// </summary>
        /// <typeparam name="T">Type of Godot object</typeparam>
        /// <param name="objects">Collection to validate</param>
        /// <param name="context">Context for error reporting</param>
        /// <returns>True if all objects are valid</returns>
        public static bool AreAllValid<T>(IEnumerable<T?> objects, string context = "") where T : GodotObject
        {
            foreach (var obj in objects)
            {
                if (!IsValid(obj, context))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Filters a collection to only include valid Godot objects.
        /// </summary>
        /// <typeparam name="T">Type of Godot object</typeparam>
        /// <param name="objects">Collection to filter</param>
        /// <param name="context">Context for error reporting</param>
        /// <returns>Filtered collection with only valid objects</returns>
        public static IEnumerable<T> FilterValid<T>(IEnumerable<T?> objects, string context = "") where T : GodotObject
        {
            foreach (var obj in objects)
            {
                if (IsValid(obj, context))
                    yield return obj!;
            }
        }

        private static void LogError(string message, Exception ex, string context)
        {
            var errorInfo = new ErrorInfo
            {
                Timestamp = DateTime.UtcNow,
                Message = message,
                Context = context,
                ExceptionType = ex.GetType().Name,
                StackTrace = ex.StackTrace ?? "No stack trace available"
            };

            lock (_logLock)
            {
                if (_recentErrors.Count >= MAX_ERROR_HISTORY)
                    _recentErrors.Dequeue();
                
                _recentErrors.Enqueue(errorInfo);
            }

            // Only log to console in debug mode to avoid performance impact
            DebugLog.LogError($"[SafeOperations] {message}");
        }

        private static void LogErrorThrottled(string message, Exception ex, string context)
        {
            var now = DateTime.UtcNow;
            var throttleKey = $"{context}:{ex.GetType().Name}";

            lock (_logLock)
            {
                if (!_lastLogTimes.TryGetValue(throttleKey, out var lastTime) || 
                    now - lastTime > _logThrottleInterval)
                {
                    _lastLogTimes[throttleKey] = now;
                    LogError(message, ex, context);
                }
            }
        }
    }

    /// <summary>
    /// Information about an error that occurred during safe operation execution.
    /// </summary>
    public struct ErrorInfo
    {
        public DateTime Timestamp { get; set; }
        public string Message { get; set; }
        public string Context { get; set; }
        public string ExceptionType { get; set; }
        public string StackTrace { get; set; }
    }
}