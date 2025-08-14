using System;

namespace Lithobrake.Core
{
    /// <summary>
    /// Enumeration of different part types in the game.
    /// Used for categorization, filtering, and part-specific behavior.
    /// </summary>
    public enum PartType
    {
        /// <summary>
        /// Unknown or undefined part type
        /// </summary>
        Unknown = 0,
        
        /// <summary>
        /// Command parts - crew pods, probe cores, control systems
        /// </summary>
        Command = 1,
        
        /// <summary>
        /// Fuel tanks - liquid fuel, oxidizer, monopropellant storage
        /// </summary>
        FuelTank = 2,
        
        /// <summary>
        /// Engines - liquid fuel engines, solid rocket boosters, RCS thrusters
        /// </summary>
        Engine = 3,
        
        /// <summary>
        /// Structural parts - decouplers, struts, girders
        /// </summary>
        Structural = 4,
        
        /// <summary>
        /// Aerodynamic parts - wings, fins, fairings, control surfaces
        /// </summary>
        Aerodynamic = 5,
        
        /// <summary>
        /// Utility parts - batteries, solar panels, communication equipment
        /// </summary>
        Utility = 6,
        
        /// <summary>
        /// Science parts - experiments, sensors, data storage
        /// </summary>
        Science = 7,
        
        /// <summary>
        /// Landing gear and wheels
        /// </summary>
        LandingGear = 8,
        
        /// <summary>
        /// Docking ports and connection systems
        /// </summary>
        Coupling = 9,
        
        /// <summary>
        /// Cargo and payload parts
        /// </summary>
        Cargo = 10
    }
    
    /// <summary>
    /// Part category information and utilities
    /// </summary>
    public static class PartTypeInfo
    {
        /// <summary>
        /// Get display name for a part type
        /// </summary>
        public static string GetDisplayName(PartType type)
        {
            return type switch
            {
                PartType.Command => "Command & Control",
                PartType.FuelTank => "Fuel Tanks",
                PartType.Engine => "Propulsion",
                PartType.Structural => "Structural",
                PartType.Aerodynamic => "Aerodynamics",
                PartType.Utility => "Electrical & Utility",
                PartType.Science => "Science",
                PartType.LandingGear => "Ground",
                PartType.Coupling => "Coupling",
                PartType.Cargo => "Cargo & Payload",
                PartType.Unknown => "Unknown",
                _ => "Undefined"
            };
        }
        
        /// <summary>
        /// Get category description for a part type
        /// </summary>
        public static string GetDescription(PartType type)
        {
            return type switch
            {
                PartType.Command => "Parts that provide control authority and crew capacity",
                PartType.FuelTank => "Storage for liquid fuel, oxidizer, and other propellants",
                PartType.Engine => "Thrust-generating parts including engines and boosters",
                PartType.Structural => "Structural elements, decouplers, and support systems",
                PartType.Aerodynamic => "Wings, fins, fairings, and aerodynamic control surfaces",
                PartType.Utility => "Power generation, batteries, and utility systems",
                PartType.Science => "Scientific instruments and data collection equipment",
                PartType.LandingGear => "Landing gear, wheels, and surface mobility systems",
                PartType.Coupling => "Docking ports and vessel connection systems",
                PartType.Cargo => "Cargo bays, payload adapters, and transport systems",
                PartType.Unknown => "Parts with undefined or unknown classification",
                _ => "No description available"
            };
        }
        
        /// <summary>
        /// Get color associated with a part type (for UI display)
        /// </summary>
        public static Godot.Color GetTypeColor(PartType type)
        {
            return type switch
            {
                PartType.Command => Godot.Colors.Gold,
                PartType.FuelTank => Godot.Colors.Blue,
                PartType.Engine => Godot.Colors.Red,
                PartType.Structural => Godot.Colors.Gray,
                PartType.Aerodynamic => Godot.Colors.Cyan,
                PartType.Utility => Godot.Colors.Yellow,
                PartType.Science => Godot.Colors.Green,
                PartType.LandingGear => Godot.Colors.Brown,
                PartType.Coupling => Godot.Colors.Purple,
                PartType.Cargo => Godot.Colors.Orange,
                PartType.Unknown => Godot.Colors.DarkGray,
                _ => Godot.Colors.White
            };
        }
        
        /// <summary>
        /// Check if a part type is essential for vessel functionality
        /// </summary>
        public static bool IsEssentialType(PartType type)
        {
            return type switch
            {
                PartType.Command => true,  // Need control
                PartType.Engine => true,   // Need propulsion
                _ => false
            };
        }
        
        /// <summary>
        /// Check if a part type can contain resources (fuel, etc.)
        /// </summary>
        public static bool CanContainResources(PartType type)
        {
            return type switch
            {
                PartType.FuelTank => true,
                PartType.Engine => true,    // Some engines have fuel
                PartType.Utility => true,   // Batteries, etc.
                _ => false
            };
        }
        
        /// <summary>
        /// Check if a part type generates thrust
        /// </summary>
        public static bool GeneratesThrust(PartType type)
        {
            return type switch
            {
                PartType.Engine => true,
                _ => false
            };
        }
        
        /// <summary>
        /// Get default mass range for a part type (in kg)
        /// </summary>
        public static (double min, double max) GetTypicalMassRange(PartType type)
        {
            return type switch
            {
                PartType.Command => (50, 500),      // Command pods
                PartType.FuelTank => (100, 5000),   // Various tank sizes
                PartType.Engine => (100, 1000),     // Engine masses
                PartType.Structural => (10, 200),   // Light structural parts
                PartType.Aerodynamic => (5, 100),   // Wings and fins
                PartType.Utility => (5, 50),        // Small utility parts
                PartType.Science => (10, 100),      // Science equipment
                PartType.LandingGear => (20, 200),  // Landing systems
                PartType.Coupling => (50, 300),     // Docking ports
                PartType.Cargo => (100, 1000),     // Cargo systems
                _ => (1, 10000)                     // Wide range for unknown
            };
        }
        
        /// <summary>
        /// Get all available part types as an array
        /// </summary>
        public static PartType[] GetAllTypes()
        {
            return (PartType[])Enum.GetValues(typeof(PartType));
        }
        
        /// <summary>
        /// Parse part type from string (case-insensitive)
        /// </summary>
        public static PartType ParseFromString(string typeString)
        {
            if (string.IsNullOrWhiteSpace(typeString))
                return PartType.Unknown;
                
            if (Enum.TryParse<PartType>(typeString, ignoreCase: true, out var result))
                return result;
                
            return PartType.Unknown;
        }
        
        /// <summary>
        /// Get part types suitable for initial 3-part catalog
        /// </summary>
        public static PartType[] GetInitialCatalogTypes()
        {
            return new[]
            {
                PartType.Command,   // CommandPod
                PartType.FuelTank,  // FuelTank
                PartType.Engine     // Engine
            };
        }
        
        /// <summary>
        /// Validate if a part type is implemented in the current system
        /// </summary>
        public static bool IsImplemented(PartType type)
        {
            // For initial implementation, only these 3 types are supported
            return type switch
            {
                PartType.Command => true,
                PartType.FuelTank => true,
                PartType.Engine => true,
                _ => false
            };
        }
    }
}