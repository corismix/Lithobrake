using Godot;
using System;

namespace Lithobrake.Core
{
    /// <summary>
    /// Space-specific physics configuration constants and utilities for optimal simulation
    /// </summary>
    public static class SpacePhysicsConfig
    {
        // Collision Layers (powers of 2 for bitmasking)
        public const uint LAYER_VESSEL_PARTS = 1;      // Layer 1: Vessel parts
        public const uint LAYER_DEBRIS = 2;            // Layer 2: Debris and separated parts
        public const uint LAYER_CELESTIAL_BODIES = 4;  // Layer 3: Planets, moons
        public const uint LAYER_ATMOSPHERE = 8;        // Layer 4: Atmospheric boundaries
        public const uint LAYER_EFFECTS = 16;          // Layer 5: Visual effects (non-colliding)
        
        // Collision Masks - what each layer can collide with
        public const uint MASK_VESSEL_PARTS = LAYER_VESSEL_PARTS | LAYER_DEBRIS | LAYER_CELESTIAL_BODIES;
        public const uint MASK_DEBRIS = LAYER_VESSEL_PARTS | LAYER_DEBRIS | LAYER_CELESTIAL_BODIES;
        public const uint MASK_CELESTIAL_BODIES = LAYER_VESSEL_PARTS | LAYER_DEBRIS;
        public const uint MASK_ATMOSPHERE = 0;         // Atmospheric boundaries don't physically collide
        public const uint MASK_EFFECTS = 0;            // Effects don't collide with anything
        
        // Space-specific physics parameters
        public const float SPACE_LINEAR_DAMP = 0.0f;    // No air resistance in vacuum
        public const float SPACE_ANGULAR_DAMP = 0.0f;   // No rotational damping in vacuum
        public const float ATMOSPHERIC_LINEAR_DAMP = 0.1f;  // Minimal atmospheric drag
        public const float ATMOSPHERIC_ANGULAR_DAMP = 0.05f; // Minimal rotational drag
        
        // Sleep thresholds for space precision
        public const float SPACE_SLEEP_THRESHOLD = 0.01f;  // Very low for orbital precision
        public const float LANDED_SLEEP_THRESHOLD = 0.1f;  // Higher when landed
        
        // Joint parameters for rocket-specific tuning
        public const float ROCKET_JOINT_BREAK_FORCE = 50000.0f;    // 50kN default break force
        public const float ROCKET_JOINT_BREAK_TORQUE = 5000.0f;    // 5kNm default break torque
        public const float ROCKET_JOINT_STIFFNESS = 10000.0f;      // High stiffness for rockets
        public const float ROCKET_JOINT_DAMPING = 1000.0f;         // Moderate damping
        
        /// <summary>
        /// Configure a RigidBody3D for space simulation
        /// </summary>
        public static void ConfigureForSpace(RigidBody3D rigidBody, bool isInAtmosphere = false)
        {
            if (rigidBody == null)
                return;
                
            // Set space-appropriate damping
            if (isInAtmosphere)
            {
                rigidBody.LinearDamp = ATMOSPHERIC_LINEAR_DAMP;
                rigidBody.AngularDamp = ATMOSPHERIC_ANGULAR_DAMP;
            }
            else
            {
                rigidBody.LinearDamp = SPACE_LINEAR_DAMP;
                rigidBody.AngularDamp = SPACE_ANGULAR_DAMP;
            }
            
            // Configure collision layers for vessel parts
            rigidBody.CollisionLayer = LAYER_VESSEL_PARTS;
            rigidBody.CollisionMask = MASK_VESSEL_PARTS;
            
            // Disable sleeping initially for proper orbital mechanics
            rigidBody.Sleeping = false;
            
            // Space-appropriate continuous collision detection
            rigidBody.ContinuousCd = RigidBody3D.CcdMode.CastRay;
        }
        
        /// <summary>
        /// Configure debris-specific physics
        /// </summary>
        public static void ConfigureAsDebris(RigidBody3D rigidBody)
        {
            if (rigidBody == null)
                return;
                
            ConfigureForSpace(rigidBody);
            
            // Override for debris-specific settings
            rigidBody.CollisionLayer = LAYER_DEBRIS;
            rigidBody.CollisionMask = MASK_DEBRIS;
        }
        
        /// <summary>
        /// Validate collision layer configuration at runtime
        /// </summary>
        public static bool ValidateCollisionConfiguration(RigidBody3D rigidBody)
        {
            if (rigidBody == null)
                return false;
                
            // Check if collision layer is set to a valid value
            uint validLayers = LAYER_VESSEL_PARTS | LAYER_DEBRIS | LAYER_CELESTIAL_BODIES | LAYER_ATMOSPHERE | LAYER_EFFECTS;
            
            if ((rigidBody.CollisionLayer & validLayers) == 0)
            {
                GD.PrintErr($"Invalid collision layer configuration for {rigidBody.Name}: {rigidBody.CollisionLayer}");
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Get appropriate sleep threshold based on situation
        /// </summary>
        public static float GetSleepThreshold(bool isLanded, bool isInAtmosphere)
        {
            if (isLanded)
                return LANDED_SLEEP_THRESHOLD;
            else
                return SPACE_SLEEP_THRESHOLD;
        }
    }
}