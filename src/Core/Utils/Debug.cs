using System.Diagnostics;
using Godot;

namespace Lithobrake.Core.Utils;

/// <summary>
/// Conditional logging utility that only outputs in debug builds.
/// Fixes issues #44 (excessive print statements) and #46 (information disclosure).
/// </summary>
public static class Debug
{
    /// <summary>
    /// Log a message. Only outputs in DEBUG builds.
    /// </summary>
    /// <param name="message">Message to log</param>
    [Conditional("DEBUG")]
    public static void Log(object message)
    {
        GD.Print(message);
    }

    /// <summary>
    /// Log an error message. Only outputs in DEBUG builds.
    /// </summary>
    /// <param name="message">Error message to log</param>
    [Conditional("DEBUG")]
    public static void LogError(object message)
    {
        GD.PrintErr(message);
    }

    /// <summary>
    /// Log a warning message. Only outputs in DEBUG builds.
    /// </summary>
    /// <param name="message">Warning message to log</param>
    [Conditional("DEBUG")]
    public static void LogWarning(object message)
    {
        GD.PrintErr($"[WARNING] {message}");
    }

    /// <summary>
    /// Log a formatted message with category. Only outputs in DEBUG builds.
    /// </summary>
    /// <param name="category">Category/system name</param>
    /// <param name="message">Message to log</param>
    [Conditional("DEBUG")]
    public static void LogWithCategory(string category, object message)
    {
        GD.Print($"[{category}] {message}");
    }
}