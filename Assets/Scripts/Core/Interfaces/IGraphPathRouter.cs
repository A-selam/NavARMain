using System.Collections.Generic;
using UnityEngine;
using NavAR.Core.Entities;

namespace NavAR.Core.Interfaces
{
    /// <summary>
    /// Computes paths through a directed graph of navigation nodes.
    /// Detects floor transitions and returns routing metadata for UI handling.
    /// </summary>
    public interface IGraphPathRouter
    {
        /// <summary>
        /// Computes a path from start position to end position via graph nodes.
        /// Returns path corners as Vector3 points for rendering.
        /// </summary>
        GraphRoutingResult CalculateGraphPath(
            Vector3 startPosition,
            Vector3 endPosition,
            int currentFloorId
        );

        /// <summary>
        /// Computes a path with optional destination floor awareness.
        /// Used to prefer end nodes on the destination floor when routing across floors.
        /// </summary>
        GraphRoutingResult CalculateGraphPath(
            Vector3 startPosition,
            Vector3 endPosition,
            int currentFloorId,
            int? destinationFloorId
        );
    }

    /// <summary>
    /// Result of graph routing including path, transitions, and metadata.
    /// </summary>
    public class GraphRoutingResult
    {
        /// <summary>
        /// Ordered list of positions from start to end (for rendering the line).
        /// </summary>
        public List<Vector3> PathCorners { get; set; } = new List<Vector3>();

        /// <summary>
        /// Path corners from the current floor start to the floor transition node.
        /// If no transition exists, this is the same as PathCorners.
        /// </summary>
        public List<Vector3> PrimaryStageCorners { get; set; } = new List<Vector3>();

        /// <summary>
        /// Path corners from the floor transition node to the destination floor target.
        /// Empty when the route stays on the same floor.
        /// </summary>
        public List<Vector3> ContinuationStageCorners { get; set; } = new List<Vector3>();

        /// <summary>
        /// All nodes traversed along the path, in order.
        /// </summary>
        public List<GraphNode> NodePath { get; set; } = new List<GraphNode>();

        /// <summary>
        /// If true, path includes a floor transition and the UI should prompt the user.
        /// </summary>
        public bool HasFloorTransition { get; set; }

        /// <summary>
        /// If HasFloorTransition is true, this is the target floor ID.
        /// </summary>
        public int TransitionTargetFloorId { get; set; }

        /// <summary>
        /// If HasFloorTransition is true, this is a human-readable label for the target floor.
        /// </summary>
        public string TransitionTargetLabel { get; set; }

        /// <summary>
        /// If HasFloorTransition is true, this is the node ID of the transition point.
        /// </summary>
        public string TransitionNodeId { get; set; }

        /// <summary>
        /// Zero-based index of the transition node within NodePath, or -1 when no transition exists.
        /// </summary>
        public int TransitionNodeIndex { get; set; } = -1;

        /// <summary>
        /// Total distance along the path.
        /// </summary>
        public float TotalDistance { get; set; }

        /// <summary>
        /// If routing failed, this error describes why.
        /// </summary>
        public string ErrorMessage { get; set; }

        public bool IsValid => string.IsNullOrEmpty(ErrorMessage) && PathCorners.Count > 0;
    }
}
