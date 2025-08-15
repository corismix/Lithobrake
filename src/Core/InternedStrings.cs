using System;

namespace Lithobrake.Core
{
    /// <summary>
    /// Centralized string interning for common identifiers to optimize memory usage.
    /// Interned strings are cached in a global pool to reduce memory allocations.
    /// </summary>
    public static class InternedStrings
    {
        // Part Type Names
        public static readonly string ENGINE_TYPE = string.Intern("Engine");
        public static readonly string FUEL_TANK_TYPE = string.Intern("FuelTank");
        public static readonly string COMMAND_POD_TYPE = string.Intern("CommandPod");
        
        // Common Part IDs
        public static readonly string ENGINE_ID = string.Intern("engine");
        public static readonly string FUEL_TANK_ID = string.Intern("fuel-tank");
        public static readonly string COMMAND_POD_ID = string.Intern("command-pod");
        
        // Common Part Names
        public static readonly string ENGINE_NAME = string.Intern("Basic Engine");
        public static readonly string FUEL_TANK_NAME = string.Intern("Fuel Tank");
        public static readonly string COMMAND_POD_NAME = string.Intern("Command Pod");
        
        // Component Names
        public static readonly string RIGID_BODY_SUFFIX = string.Intern("_RigidBody");
        public static readonly string COLLISION_SUFFIX = string.Intern("_Collision");
        public static readonly string MESH_SUFFIX = string.Intern("_Mesh");
        
        // Performance diagnostic strings
        public static readonly string PHYSICS_VESSEL_PREFIX = string.Intern("PhysicsVessel");
        public static readonly string PART_PREFIX = string.Intern("Part");
        
        // Status and error strings
        public static readonly string NOT_FOUND = string.Intern("not found");
        public static readonly string INVALID = string.Intern("invalid");
        public static readonly string DISPOSED = string.Intern("disposed");
        
        /// <summary>
        /// Intern a string if it's commonly used, otherwise return the original
        /// </summary>
        public static string InternIfCommon(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;
                
            // Only intern strings that are likely to be repeated
            return value.Length <= 50 ? string.Intern(value) : value;
        }
    }
}