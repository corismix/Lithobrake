using System;
using System.Collections.Generic;
using System.Threading;
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