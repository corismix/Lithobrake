using System;
using System.Collections.Generic;
using System.Threading;
using Godot;

namespace Lithobrake.Core
{
    /// <summary>
    /// Conditional logging system that compiles out in release builds for performance.
    /// Replaces all GD.Print() calls with debug-only equivalents.
    /// </summary>
    public static class DebugLog
    {
        private static readonly object _throttleLock = new();
        private static readonly Dictionary<string, DateTime> _lastLogTimes = new();
        private static readonly TimeSpan _throttleInterval = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Logs a message only in debug builds. Compiled out in release builds.
        /// </summary>
        /// <param name="message">The message to log</param>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void Log(object message)
        {
            GD.Print(message);
        }

        /// <summary>
        /// Logs an error message only in debug builds. Compiled out in release builds.
        /// </summary>
        /// <param name="message">The error message to log</param>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void LogError(object message)
        {
            GD.PrintErr(message);
        }

        /// <summary>
        /// Logs a warning message only in debug builds. Compiled out in release builds.
        /// </summary>
        /// <param name="message">The warning message to log</param>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void LogWarning(object message)
        {
            GD.PrintErr($"⚠️ {message}");
        }

        /// <summary>
        /// Logs a message with performance timing information only in debug builds.
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="elapsedMs">Elapsed time in milliseconds</param>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void LogPerformance(object message, double elapsedMs)
        {
            GD.Print($"[PERF] {message} ({elapsedMs:F2}ms)");
        }

        /// <summary>
        /// Logs a message only once per throttle interval to prevent spam.
        /// Useful for messages that could be logged every frame.
        /// </summary>
        /// <param name="key">Unique key to identify the message type</param>
        /// <param name="message">The message to log</param>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void LogThrottled(string key, object message)
        {
            var now = DateTime.UtcNow;
            
            lock (_throttleLock)
            {
                if (!_lastLogTimes.TryGetValue(key, out var lastTime) || 
                    now - lastTime > _throttleInterval)
                {
                    _lastLogTimes[key] = now;
                    GD.Print($"[THROTTLED] {message}");
                }
            }
        }

        /// <summary>
        /// Logs physics-related information with a specific prefix.
        /// Only compiled in debug builds.
        /// </summary>
        /// <param name="message">The physics message to log</param>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void LogPhysics(object message)
        {
            GD.Print($"[PHYSICS] {message}");
        }

        /// <summary>
        /// Logs orbital mechanics information with a specific prefix.
        /// Only compiled in debug builds.
        /// </summary>
        /// <param name="message">The orbital message to log</param>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void LogOrbital(object message)
        {
            GD.Print($"[ORBITAL] {message}");
        }

        /// <summary>
        /// Logs memory management information with a specific prefix.
        /// Only compiled in debug builds.
        /// </summary>
        /// <param name="message">The memory message to log</param>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void LogMemory(object message)
        {
            GD.Print($"[MEMORY] {message}");
        }

        /// <summary>
        /// Logs resource management information with a specific prefix.
        /// Only compiled in debug builds.
        /// </summary>
        /// <param name="message">The resource message to log</param>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void LogResource(object message)
        {
            GD.Print($"[RESOURCE] {message}");
        }

        /// <summary>
        /// Clears throttled message history. Useful for tests.
        /// </summary>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void ClearThrottleHistory()
        {
            lock (_throttleLock)
            {
                _lastLogTimes.Clear();
            }
        }
    }
}