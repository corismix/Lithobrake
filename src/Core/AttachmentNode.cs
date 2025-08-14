using Godot;
using System;

namespace Lithobrake.Core
{
    /// <summary>
    /// Represents an attachment point on a part where other parts can be connected.
    /// Handles position, orientation, attachment rules, and connection state.
    /// </summary>
    public class AttachmentNode
    {
        /// <summary>
        /// Local position of the attachment node relative to the part's origin
        /// </summary>
        public Vector3 Position { get; set; } = Vector3.Zero;
        
        /// <summary>
        /// Direction vector indicating the attachment orientation (normalized)
        /// </summary>
        public Vector3 Orientation { get; set; } = Vector3.Up;
        
        /// <summary>
        /// Maximum mass (in kg) that can be attached to this node
        /// </summary>
        public double MaxMass { get; set; } = 10000.0; // 10 tons default
        
        /// <summary>
        /// Whether this attachment node is currently occupied by another part
        /// </summary>
        public bool IsOccupied { get; set; } = false;
        
        /// <summary>
        /// Type of attachment node (affects what can connect)
        /// </summary>
        public AttachmentNodeType NodeType { get; set; } = AttachmentNodeType.Stack;
        
        /// <summary>
        /// Size category of the attachment node (affects connection compatibility)
        /// </summary>
        public AttachmentNodeSize NodeSize { get; set; } = AttachmentNodeSize.Size1;
        
        /// <summary>
        /// Name identifier for this attachment node (for debugging and save/load)
        /// </summary>
        public string NodeName { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether this node can accept surface-attached parts
        /// </summary>
        public bool AllowSurfaceAttach { get; set; } = false;
        
        /// <summary>
        /// The part that is currently attached to this node (if any)
        /// </summary>
        public Part? AttachedPart { get; set; } = null;
        
        /// <summary>
        /// Minimum distance required for attachment (for collision prevention)
        /// </summary>
        public float MinAttachDistance { get; set; } = 0.1f;
        
        /// <summary>
        /// Maximum distance allowed for attachment (snap range)
        /// </summary>
        public float MaxAttachDistance { get; set; } = 2.0f;
        
        /// <summary>
        /// Whether this node requires a matching orientation to connect
        /// </summary>
        public bool RequireMatchingOrientation { get; set; } = true;
        
        /// <summary>
        /// Angular tolerance for orientation matching (in radians)
        /// </summary>
        public float OrientationTolerance { get; set; } = Mathf.Pi * 0.25f; // 45 degrees
        
        /// <summary>
        /// Create a new attachment node with default settings
        /// </summary>
        public AttachmentNode()
        {
            // Default constructor with standard values
        }
        
        /// <summary>
        /// Create a new attachment node with specified position and orientation
        /// </summary>
        public AttachmentNode(Vector3 position, Vector3 orientation, string nodeName = "")
        {
            Position = position;
            Orientation = orientation.Normalized();
            NodeName = nodeName;
        }
        
        /// <summary>
        /// Create a new attachment node with full specification
        /// </summary>
        public AttachmentNode(Vector3 position, Vector3 orientation, AttachmentNodeType nodeType, 
                            AttachmentNodeSize nodeSize, double maxMass, string nodeName = "")
        {
            Position = position;
            Orientation = orientation.Normalized();
            NodeType = nodeType;
            NodeSize = nodeSize;
            MaxMass = maxMass;
            NodeName = nodeName;
        }
        
        /// <summary>
        /// Check if this attachment node is compatible with another attachment node
        /// </summary>
        public bool IsCompatibleWith(AttachmentNode other)
        {
            // Check if either node is already occupied
            if (IsOccupied || other.IsOccupied)
                return false;
            
            // Check node type compatibility
            if (!AreNodeTypesCompatible(NodeType, other.NodeType))
                return false;
            
            // Check size compatibility
            if (!AreNodeSizesCompatible(NodeSize, other.NodeSize))
                return false;
            
            // Check orientation compatibility if required
            if (RequireMatchingOrientation && other.RequireMatchingOrientation)
            {
                var angle = Orientation.AngleTo(-other.Orientation); // Opposite orientations should match
                if (angle > OrientationTolerance)
                    return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Check if two node types are compatible for connection
        /// </summary>
        private static bool AreNodeTypesCompatible(AttachmentNodeType type1, AttachmentNodeType type2)
        {
            return type1 switch
            {
                AttachmentNodeType.Stack => type2 == AttachmentNodeType.Stack,
                AttachmentNodeType.Radial => type2 == AttachmentNodeType.Surface || type2 == AttachmentNodeType.Radial,
                AttachmentNodeType.Surface => type2 == AttachmentNodeType.Radial || type2 == AttachmentNodeType.Surface,
                AttachmentNodeType.Dock => type2 == AttachmentNodeType.Dock,
                _ => false
            };
        }
        
        /// <summary>
        /// Check if two node sizes are compatible for connection
        /// </summary>
        private static bool AreNodeSizesCompatible(AttachmentNodeSize size1, AttachmentNodeSize size2)
        {
            // For now, require exact size matches
            // Could be expanded to allow adapters or size ranges
            return size1 == size2;
        }
        
        /// <summary>
        /// Calculate the world position of this attachment node given the part's transform
        /// </summary>
        public Vector3 GetWorldPosition(Transform3D partTransform)
        {
            return partTransform * Position;
        }
        
        /// <summary>
        /// Calculate the world orientation of this attachment node given the part's transform
        /// </summary>
        public Vector3 GetWorldOrientation(Transform3D partTransform)
        {
            return partTransform.Basis * Orientation;
        }
        
        /// <summary>
        /// Check if a world position is within attachment range of this node
        /// </summary>
        public bool IsWithinAttachRange(Vector3 worldPosition, Transform3D partTransform)
        {
            var nodeWorldPos = GetWorldPosition(partTransform);
            var distance = nodeWorldPos.DistanceTo(worldPosition);
            
            return distance >= MinAttachDistance && distance <= MaxAttachDistance;
        }
        
        /// <summary>
        /// Attach a part to this node
        /// </summary>
        public bool AttachPart(Part part)
        {
            if (IsOccupied)
            {
                GD.PrintErr($"AttachmentNode.AttachPart: Node {NodeName} already occupied");
                return false;
            }
            
            if (part.GetTotalMass() > MaxMass)
            {
                GD.PrintErr($"AttachmentNode.AttachPart: Part too heavy ({part.GetTotalMass():F0}kg > {MaxMass:F0}kg)");
                return false;
            }
            
            AttachedPart = part;
            IsOccupied = true;
            
            return true;
        }
        
        /// <summary>
        /// Detach the part from this node
        /// </summary>
        public bool DetachPart()
        {
            if (!IsOccupied || AttachedPart == null)
            {
                GD.PrintErr($"AttachmentNode.DetachPart: No part attached to node {NodeName}");
                return false;
            }
            
            AttachedPart = null;
            IsOccupied = false;
            
            return true;
        }
        
        /// <summary>
        /// Get attachment strength based on node type and size
        /// </summary>
        public float GetAttachmentStrength()
        {
            var baseStrength = NodeType switch
            {
                AttachmentNodeType.Stack => 1.0f,    // Strong axial connection
                AttachmentNodeType.Radial => 0.7f,   // Medium radial connection
                AttachmentNodeType.Surface => 0.5f,  // Weaker surface connection
                AttachmentNodeType.Dock => 1.2f,     // Very strong docking connection
                _ => 0.5f
            };
            
            var sizeMultiplier = NodeSize switch
            {
                AttachmentNodeSize.Size0 => 0.5f,    // Small nodes
                AttachmentNodeSize.Size1 => 1.0f,    // Standard nodes
                AttachmentNodeSize.Size2 => 1.5f,    // Large nodes
                AttachmentNodeSize.Size3 => 2.0f,    // Extra large nodes
                _ => 1.0f
            };
            
            return baseStrength * sizeMultiplier;
        }
        
        /// <summary>
        /// Create a visual debug representation of this attachment node
        /// </summary>
        public MeshInstance3D CreateDebugVisualization()
        {
            var meshInstance = new MeshInstance3D();
            
            // Create a small sphere to show node position
            var sphereMesh = new SphereMesh
            {
                Radius = 0.1f,
                RadialSegments = 8,
                Rings = 4
            };
            meshInstance.Mesh = sphereMesh;
            
            // Color based on node type
            var material = new StandardMaterial3D();
            material.AlbedoColor = NodeType switch
            {
                AttachmentNodeType.Stack => Colors.Green,
                AttachmentNodeType.Radial => Colors.Blue,
                AttachmentNodeType.Surface => Colors.Yellow,
                AttachmentNodeType.Dock => Colors.Purple,
                _ => Colors.Gray
            };
            
            if (IsOccupied)
            {
                material.AlbedoColor = Colors.Red;
            }
            
            meshInstance.MaterialOverride = material;
            meshInstance.Position = Position;
            
            return meshInstance;
        }
        
        /// <summary>
        /// Copy this attachment node
        /// </summary>
        public AttachmentNode Copy()
        {
            return new AttachmentNode
            {
                Position = Position,
                Orientation = Orientation,
                MaxMass = MaxMass,
                NodeType = NodeType,
                NodeSize = NodeSize,
                NodeName = NodeName,
                AllowSurfaceAttach = AllowSurfaceAttach,
                MinAttachDistance = MinAttachDistance,
                MaxAttachDistance = MaxAttachDistance,
                RequireMatchingOrientation = RequireMatchingOrientation,
                OrientationTolerance = OrientationTolerance,
                // Note: Don't copy IsOccupied or AttachedPart - new nodes start empty
            };
        }
        
        /// <summary>
        /// String representation for debugging
        /// </summary>
        public override string ToString()
        {
            var status = IsOccupied ? $"Occupied by {AttachedPart?.PartName ?? "Unknown"}" : "Available";
            return $"AttachmentNode {NodeName}: {NodeType}/{NodeSize} at {Position} - {status}";
        }
    }
    
    /// <summary>
    /// Types of attachment nodes
    /// </summary>
    public enum AttachmentNodeType
    {
        /// <summary>
        /// Stack attachment - parts connected end-to-end vertically
        /// </summary>
        Stack,
        
        /// <summary>
        /// Radial attachment - parts attached to the side at specific points
        /// </summary>
        Radial,
        
        /// <summary>
        /// Surface attachment - parts that can attach anywhere on a surface
        /// </summary>
        Surface,
        
        /// <summary>
        /// Docking port - special connection for vessel-to-vessel docking
        /// </summary>
        Dock
    }
    
    /// <summary>
    /// Sizes of attachment nodes (affects connection compatibility)
    /// </summary>
    public enum AttachmentNodeSize
    {
        /// <summary>
        /// Small parts (0.625m diameter equivalent)
        /// </summary>
        Size0,
        
        /// <summary>
        /// Standard parts (1.25m diameter equivalent)
        /// </summary>
        Size1,
        
        /// <summary>
        /// Large parts (2.5m diameter equivalent)
        /// </summary>
        Size2,
        
        /// <summary>
        /// Extra large parts (3.75m diameter equivalent)
        /// </summary>
        Size3
    }
}