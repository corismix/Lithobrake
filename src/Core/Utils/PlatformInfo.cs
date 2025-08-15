using System;
using System.Runtime.InteropServices;

namespace Lithobrake.Core.Utils;

/// <summary>
/// Platform detection utility for runtime optimization decisions.
/// Addresses audit issue #47 - Missing Rosetta detection for performance optimization.
/// </summary>
public static class PlatformInfo
{
    /// <summary>
    /// True if running under Rosetta 2 emulation on Apple Silicon.
    /// Used for optimization decisions - native ARM64 code can use more aggressive optimizations.
    /// </summary>
    public static readonly bool IsRunningUnderRosetta = 
        RuntimeInformation.ProcessArchitecture != RuntimeInformation.OSArchitecture;

    /// <summary>
    /// True if running on Apple Silicon (ARM64) macOS.
    /// Can be used to enable Apple-specific optimizations.
    /// </summary>
    public static readonly bool IsAppleSilicon = 
        RuntimeInformation.OSArchitecture == Architecture.Arm64 && 
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    /// <summary>
    /// True if running on native ARM64 Apple Silicon (not under Rosetta).
    /// Best performance scenario for M1/M2/M3/M4 chips.
    /// </summary>
    public static readonly bool IsNativeAppleSilicon = IsAppleSilicon && !IsRunningUnderRosetta;

    /// <summary>
    /// Get a string description of the current runtime environment.
    /// </summary>
    public static string GetRuntimeDescription()
    {
        var osArch = RuntimeInformation.OSArchitecture.ToString();
        var processArch = RuntimeInformation.ProcessArchitecture.ToString();
        
        if (IsRunningUnderRosetta)
        {
            return $"Rosetta 2 ({processArch} on {osArch})";
        }
        
        if (IsNativeAppleSilicon)
        {
            return $"Native Apple Silicon ({osArch})";
        }
        
        return $"{RuntimeInformation.OSDescription} ({osArch})";
    }
}