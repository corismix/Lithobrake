using Godot;
using System;

namespace Lithobrake.Core
{
    /// <summary>
    /// Separation event data structure for tracking joint separation mechanics.
    /// Used by PhysicsVessel for atomic separation operations with precise impulse application.
    /// Provides validation and state management for staging operations.
    /// </summary>
    public struct SeparationEvent
    {
        /// <summary>
        /// Unique identifier for this separation event
        /// </summary>
        public int EventId { get; set; }
        
        /// <summary>
        /// ID of the joint being separated
        /// </summary>
        public int JointId { get; set; }
        
        /// <summary>
        /// ID of the primary part (remains with vessel)
        /// </summary>
        public int PartA { get; set; }
        
        /// <summary>
        /// ID of the separated part
        /// </summary>
        public int PartB { get; set; }
        
        /// <summary>
        /// Precise separation impulse magnitude (default 500 N·s)
        /// </summary>
        public float SeparationImpulse { get; set; }
        
        /// <summary>
        /// Direction of separation impulse in world coordinates
        /// </summary>
        public Double3 SeparationDirection { get; set; }
        
        /// <summary>
        /// Position where separation impulse is applied (world coordinates)
        /// </summary>
        public Double3 SeparationPosition { get; set; }
        
        /// <summary>
        /// Pre-separation mass of vessel before separation
        /// </summary>
        public double PreSeparationMass { get; set; }
        
        /// <summary>
        /// Post-separation mass of remaining vessel
        /// </summary>
        public double PostSeparationMass { get; set; }
        
        /// <summary>
        /// Pre-separation center of mass
        /// </summary>
        public Double3 PreSeparationCenterOfMass { get; set; }
        
        /// <summary>
        /// Post-separation center of mass
        /// </summary>
        public Double3 PostSeparationCenterOfMass { get; set; }
        
        /// <summary>
        /// Pre-separation moment of inertia
        /// </summary>
        public Double3 PreSeparationMomentOfInertia { get; set; }
        
        /// <summary>
        /// Post-separation moment of inertia
        /// </summary>
        public Double3 PostSeparationMomentOfInertia { get; set; }
        
        /// <summary>
        /// Timestamp when separation event occurred (engine time)
        /// </summary>
        public double EventTime { get; set; }
        
        /// <summary>
        /// Duration of separation operation in milliseconds
        /// </summary>
        public double OperationDuration { get; set; }
        
        /// <summary>
        /// Whether separation was successful
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Physics validation result
        /// </summary>
        public SeparationValidationResult ValidationResult { get; set; }
        
        /// <summary>
        /// Error message if separation failed
        /// </summary>
        public string ErrorMessage { get; set; }
        
        /// <summary>
        /// Create a standard separation event with default 500 N·s impulse
        /// </summary>
        public static SeparationEvent Create(int eventId, int jointId, int partA, int partB, Double3 position, Double3 direction)
        {
            return new SeparationEvent
            {
                EventId = eventId,
                JointId = jointId,
                PartA = partA,
                PartB = partB,
                SeparationImpulse = 500f, // Standard separation impulse from CLAUDE.md
                SeparationDirection = direction.Normalized,
                SeparationPosition = position,
                EventTime = Time.GetTicksUsec() / 1000000.0, // Convert microseconds to seconds
                Success = false,
                ValidationResult = SeparationValidationResult.Pending,
                ErrorMessage = string.Empty
            };
        }
        
        /// <summary>
        /// Create separation event with custom impulse magnitude
        /// </summary>
        public static SeparationEvent CreateCustom(int eventId, int jointId, int partA, int partB, 
            Double3 position, Double3 direction, float customImpulse)
        {
            return new SeparationEvent
            {
                EventId = eventId,
                JointId = jointId,
                PartA = partA,
                PartB = partB,
                SeparationImpulse = customImpulse,
                SeparationDirection = direction.Normalized,
                SeparationPosition = position,
                EventTime = Time.GetTicksUsec() / 1000000.0, // Convert microseconds to seconds
                Success = false,
                ValidationResult = SeparationValidationResult.Pending,
                ErrorMessage = string.Empty
            };
        }
        
        /// <summary>
        /// Mark separation as completed successfully
        /// </summary>
        public void MarkSuccess(double operationDuration, VesselMassProperties preMass, VesselMassProperties postMass)
        {
            Success = true;
            OperationDuration = operationDuration;
            ValidationResult = SeparationValidationResult.Valid;
            
            // Store mass properties for validation
            PreSeparationMass = preMass.TotalMass;
            PostSeparationMass = postMass.TotalMass;
            PreSeparationCenterOfMass = preMass.CenterOfMass;
            PostSeparationCenterOfMass = postMass.CenterOfMass;
            PreSeparationMomentOfInertia = preMass.MomentOfInertia;
            PostSeparationMomentOfInertia = postMass.MomentOfInertia;
        }
        
        /// <summary>
        /// Mark separation as failed with error details
        /// </summary>
        public void MarkFailure(string errorMessage, SeparationValidationResult validationResult)
        {
            Success = false;
            ErrorMessage = errorMessage;
            ValidationResult = validationResult;
            OperationDuration = -1;
        }
        
        /// <summary>
        /// Validate separation event physics consistency
        /// </summary>
        public bool ValidatePhysics(double tolerancePercent = 1.0)
        {
            // Validate mass conservation (allow small tolerance for floating point)
            if (PostSeparationMass > PreSeparationMass * (1.0 + tolerancePercent / 100.0))
            {
                return false;
            }
            
            // Validate center of mass shift is reasonable
            double comShift = Double3.Distance(PreSeparationCenterOfMass, PostSeparationCenterOfMass);
            if (comShift > PreSeparationMass * 0.1) // Arbitrary but reasonable limit
            {
                return false;
            }
            
            // Validate moment of inertia is reasonable
            if (PostSeparationMomentOfInertia.Length > PreSeparationMomentOfInertia.Length * (1.0 + tolerancePercent / 100.0))
            {
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Get performance metrics for this separation
        /// </summary>
        public SeparationMetrics GetMetrics()
        {
            return new SeparationMetrics
            {
                OperationTime = OperationDuration,
                MassRatio = PostSeparationMass / PreSeparationMass,
                CenterOfMassShift = Double3.Distance(PreSeparationCenterOfMass, PostSeparationCenterOfMass),
                ImpulseMagnitude = SeparationImpulse,
                Success = Success,
                ValidationResult = ValidationResult
            };
        }
    }
    
    /// <summary>
    /// Validation result for separation operations
    /// </summary>
    public enum SeparationValidationResult
    {
        Pending,          // Validation not yet performed
        Valid,            // Separation passed all validation checks
        InvalidJoint,     // Joint ID is invalid or inactive
        InvalidParts,     // One or more part IDs are invalid
        PhysicsError,     // Physics system reported an error
        MassInconsistent, // Mass conservation violated
        ForceExceeded,    // Applied force exceeds safe limits
        TimeoutExceeded   // Operation took too long to complete
    }
    
    /// <summary>
    /// Performance metrics for separation operations
    /// </summary>
    public struct SeparationMetrics
    {
        public double OperationTime;          // Time in milliseconds
        public double MassRatio;             // Post/pre mass ratio
        public double CenterOfMassShift;     // Distance COM moved
        public float ImpulseMagnitude;       // Applied impulse magnitude
        public bool Success;                 // Whether operation succeeded
        public SeparationValidationResult ValidationResult; // Validation outcome
    }
}