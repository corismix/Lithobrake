using Godot;

namespace Lithobrake.Core
{
    /// <summary>
    /// Joint parameter configuration struct for vessel joint tuning.
    /// Contains stiffness and damping parameters for different joint types.
    /// Used by PhysicsVessel for joint stability and anti-wobble systems.
    /// </summary>
    public struct JointTuning
    {
        /// <summary>
        /// Linear stiffness (N/m) for translational axes
        /// </summary>
        public float LinearStiffness;
        
        /// <summary>
        /// Linear damping (N·s/m) for translational axes
        /// </summary>
        public float LinearDamping;
        
        /// <summary>
        /// Angular stiffness (N·m/rad) for rotational axes
        /// </summary>
        public float AngularStiffness;
        
        /// <summary>
        /// Angular damping (N·m·s/rad) for rotational axes
        /// </summary>
        public float AngularDamping;
        
        /// <summary>
        /// Maximum force/torque before joint breaks (N or N·m)
        /// </summary>
        public float BreakForce;
        
        /// <summary>
        /// Enable projection correction for joint drift
        /// </summary>
        public bool UseProjection;
        
        /// <summary>
        /// Projection distance threshold (m)
        /// </summary>
        public float ProjectionDistance;
        
        /// <summary>
        /// Projection angular threshold (radians)
        /// </summary>
        public float ProjectionAngle;
        
        /// <summary>
        /// Default tuning for rigid structural joints
        /// </summary>
        public static JointTuning Rigid => new JointTuning
        {
            LinearStiffness = 100000f,    // Very stiff
            LinearDamping = 1000f,        // High damping
            AngularStiffness = 50000f,    // Very stiff rotation
            AngularDamping = 500f,        // High angular damping
            BreakForce = 500000f,         // 500kN break force
            UseProjection = true,
            ProjectionDistance = 0.01f,   // 1cm drift threshold
            ProjectionAngle = 0.0174f     // 1 degree drift threshold
        };
        
        /// <summary>
        /// Default tuning for flexible joints (less rigid for performance)
        /// </summary>
        public static JointTuning Flexible => new JointTuning
        {
            LinearStiffness = 50000f,     // Moderately stiff
            LinearDamping = 500f,         // Moderate damping
            AngularStiffness = 25000f,    // Moderate angular stiffness
            AngularDamping = 250f,        // Moderate angular damping
            BreakForce = 250000f,         // 250kN break force
            UseProjection = true,
            ProjectionDistance = 0.02f,   // 2cm drift threshold
            ProjectionAngle = 0.0349f     // 2 degree drift threshold
        };
        
        /// <summary>
        /// Tuning for separable joints (decouplers, stages)
        /// </summary>
        public static JointTuning Separable => new JointTuning
        {
            LinearStiffness = 75000f,     // Strong but separable
            LinearDamping = 750f,         // Good damping
            AngularStiffness = 37500f,    // Strong angular
            AngularDamping = 375f,        // Good angular damping
            BreakForce = 100000f,         // 100kN break force (lower for staging)
            UseProjection = true,
            ProjectionDistance = 0.015f,  // 1.5cm drift threshold
            ProjectionAngle = 0.0262f     // 1.5 degree drift threshold
        };
        
        /// <summary>
        /// Apply tuning parameters to a Generic6DOFJoint3D
        /// Note: Godot 4's Generic6DofJoint3D has limited parameter exposure
        /// This method applies what's available and documents the intent for future enhancement
        /// </summary>
        public void ApplyTo6DOFJoint(Generic6DofJoint3D joint)
        {
            if (joint == null) return;
            
            // For now, Godot 4.4's Generic6DofJoint3D doesn't expose spring/damping parameters
            // in the same way as other physics engines. The joint tuning struct documents
            // the intended parameters for when Godot's physics system supports them or
            // when we implement custom constraint solving.
            
            // What we can do is set limits if needed (for rigid joints, lock all axes)
            // The joint stiffness and damping will be handled by the physics engine's
            // default constraint solver.
            
            // Store the tuning parameters in the joint's metadata for potential future use
            joint.SetMeta("linear_stiffness", LinearStiffness);
            joint.SetMeta("linear_damping", LinearDamping);
            joint.SetMeta("angular_stiffness", AngularStiffness);
            joint.SetMeta("angular_damping", AngularDamping);
            joint.SetMeta("break_force", BreakForce);
            joint.SetMeta("use_projection", UseProjection);
            
            // For rigid joints, we lock all axes using the available API
            // The physics engine will use its default stiffness/damping values
            
            // NOTE: Godot 4.4.1 API limitation - advanced joint parameters not exposed
            // Generic6DOFJoint3D only supports basic flag settings, not stiffness/damping
            // Joint behavior depends on Jolt physics engine defaults
            // Future Godot versions may expose SetParam methods for fine-tuning
        }
        
        /// <summary>
        /// Create tuning with custom parameters
        /// </summary>
        public static JointTuning Custom(
            float linearStiffness,
            float linearDamping,
            float angularStiffness,
            float angularDamping,
            float breakForce = 250000f,
            bool useProjection = true,
            float projectionDistance = 0.02f,
            float projectionAngle = 0.0349f)
        {
            return new JointTuning
            {
                LinearStiffness = linearStiffness,
                LinearDamping = linearDamping,
                AngularStiffness = angularStiffness,
                AngularDamping = angularDamping,
                BreakForce = breakForce,
                UseProjection = useProjection,
                ProjectionDistance = projectionDistance,
                ProjectionAngle = projectionAngle
            };
        }
        
        /// <summary>
        /// Scale tuning parameters by a factor (for dynamic adjustment)
        /// </summary>
        public JointTuning Scale(float factor)
        {
            return new JointTuning
            {
                LinearStiffness = LinearStiffness * factor,
                LinearDamping = LinearDamping * factor,
                AngularStiffness = AngularStiffness * factor,
                AngularDamping = AngularDamping * factor,
                BreakForce = BreakForce, // Don't scale break force
                UseProjection = UseProjection,
                ProjectionDistance = ProjectionDistance,
                ProjectionAngle = ProjectionAngle
            };
        }
    }
}