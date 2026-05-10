using System;
using System.Collections.Generic;
using UnityEngine;
using NavAR.Core.Interfaces;
using NavAR.Core.Entities;

namespace NavAR.Core.Navigation
{
    /// <summary>
    /// Hybrid path calculator that tries graph routing first, then falls back to NavMesh.
    /// Also handles floor transition detection and UI prompts.
    /// </summary>
    public class HybridGraphPathCalculator : IPathCalculator
    {
        private readonly IPathCalculator _navMeshCalculator;
        private readonly IGraphPathRouter _graphRouter;
        private readonly Action<int, string, string> _onFloorTransitionDetected;
        private readonly bool _enableDiagnostics;
        private GraphRoutingResult _lastGraphResult;

        public HybridGraphPathCalculator(
            IPathCalculator navMeshCalculator,
            IGraphPathRouter graphRouter,
            Action<int, string, string> onFloorTransitionDetected,
            bool enableDiagnostics = false
        )
        {
            _navMeshCalculator = navMeshCalculator ?? throw new ArgumentNullException(nameof(navMeshCalculator));
            _graphRouter = graphRouter ?? throw new ArgumentNullException(nameof(graphRouter));
            _onFloorTransitionDetected = onFloorTransitionDetected;
            _enableDiagnostics = enableDiagnostics;
        }

        public List<Vector3> CalculatePath(Vector3 startPosition, Vector3 endPosition)
        {
            // This is a compatibility override. Use CalculatePathWithContext instead.
            return _navMeshCalculator.CalculatePath(startPosition, endPosition);
        }

        /// <summary>
        /// Calculate path with floor context, using graph routing and floor transition detection.
        /// </summary>
        public List<Vector3> CalculatePathWithContext(
            Vector3 startPosition,
            Vector3 endPosition,
            int currentFloorId,
            int? destinationFloorId = null
        )
        {
            try
            {
                if (_enableDiagnostics)
                {
                    Debug.Log(
                        $"[HybridGraphPathCalculator] Starting path calculation: " +
                        $"start={startPosition}, end={endPosition}, floor={currentFloorId}, destFloor={destinationFloorId}"
                    );
                }

                // Try graph routing first, passing destination floor hint
                var graphResult = _graphRouter.CalculateGraphPath(startPosition, endPosition, currentFloorId, destinationFloorId);

                if (_enableDiagnostics)
                {
                    Debug.Log(
                        $"[HybridGraphPathCalculator] Graph router result: " +
                        $"valid={graphResult.IsValid}, corners={graphResult.PathCorners.Count}, " +
                        $"nodes={graphResult.NodePath.Count}, floorTransition={graphResult.HasFloorTransition}"
                    );
                    if (graphResult.NodePath != null && graphResult.NodePath.Count > 0)
                    {
                        Debug.Log($"[HybridGraphPathCalculator] Dijkstra route: {FormatNodePath(graphResult.NodePath)}");
                    }
                }

                _lastGraphResult = graphResult;

                if (graphResult.IsValid)
                {
                    // NOTE: do not trigger transition prompt here.
                    // UI should prompt only when the user reaches the transition node.

                    // Keep rendered path constrained to current floor NavMesh.
                    // Graph routing decides stage/transition, NavMesh decides walkable path geometry.
                    Vector3 stageTarget = endPosition;
                    if (graphResult.HasFloorTransition && graphResult.PrimaryStageCorners.Count > 0)
                    {
                        stageTarget = graphResult.PrimaryStageCorners[graphResult.PrimaryStageCorners.Count - 1];
                    }

                    var navMeshStagePath = _navMeshCalculator.CalculatePath(startPosition, stageTarget);
                    if (navMeshStagePath != null && navMeshStagePath.Count > 1)
                    {
                        if (_enableDiagnostics)
                        {
                            Debug.Log(
                                $"[HybridGraphPathCalculator] Returning NavMesh-constrained stage path " +
                                $"with {navMeshStagePath.Count} corners (target={stageTarget})."
                            );
                            Debug.Log($"[HybridGraphPathCalculator] Returned path corners: {FormatCorners(navMeshStagePath)}");
                        }
                        return navMeshStagePath;
                    }

                    // Fallback: if NavMesh failed, return graph stage for transition routes,
                    // otherwise return full graph path as last resort.
                    if (graphResult.HasFloorTransition && graphResult.PrimaryStageCorners.Count > 0)
                    {
                        return graphResult.PrimaryStageCorners;
                    }

                    return graphResult.PathCorners;
                }

                if (_enableDiagnostics)
                {
                    Debug.LogWarning(
                        $"[HybridGraphPathCalculator] Graph routing failed: {graphResult.ErrorMessage}. " +
                        $"Falling back to NavMesh."
                    );
                }

                _lastGraphResult = graphResult;

                // Fallback to NavMesh
                var navMeshPath = _navMeshCalculator.CalculatePath(startPosition, endPosition);
                
                if (_enableDiagnostics)
                {
                    Debug.Log(
                        $"[HybridGraphPathCalculator] NavMesh fallback returned {navMeshPath?.Count ?? 0} corners."
                    );
                    if (navMeshPath != null && navMeshPath.Count > 0)
                    {
                        Debug.Log($"[HybridGraphPathCalculator] Fallback path corners: {FormatCorners(navMeshPath)}");
                    }
                }

                return navMeshPath;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HybridGraphPathCalculator] Exception during path calculation: {ex}");
                return _navMeshCalculator.CalculatePath(startPosition, endPosition);
            }
        }

        private static string FormatNodePath(List<GraphNode> nodePath)
        {
            if (nodePath == null || nodePath.Count == 0)
            {
                return "<empty>";
            }

            return string.Join(" -> ", nodePath.ConvertAll(node => $"{node.node_id}[F{node.floor_id}]"));
        }

        private static string FormatCorners(List<Vector3> corners)
        {
            if (corners == null || corners.Count == 0)
            {
                return "<empty>";
            }

            return string.Join(" -> ", corners.ConvertAll(corner => $"({corner.x:F2},{corner.y:F2},{corner.z:F2})"));
        }

        public bool TryGetPendingTransition(
            out int targetFloorId,
            out string targetFloorLabel,
            out string transitionNodeId,
            out Vector3 transitionNodePosition,
            out Vector3 transitionLandingPosition)
        {
            targetFloorId = 0;
            targetFloorLabel = null;
            transitionNodeId = null;
            transitionNodePosition = Vector3.zero;
            transitionLandingPosition = Vector3.zero;

            if (_lastGraphResult == null || !_lastGraphResult.IsValid || !_lastGraphResult.HasFloorTransition)
            {
                return false;
            }

            if (_lastGraphResult.PrimaryStageCorners == null || _lastGraphResult.PrimaryStageCorners.Count == 0)
            {
                return false;
            }

            targetFloorId = _lastGraphResult.TransitionTargetFloorId;
            targetFloorLabel = _lastGraphResult.TransitionTargetLabel;
            transitionNodeId = _lastGraphResult.TransitionNodeId;
            transitionNodePosition = _lastGraphResult.PrimaryStageCorners[_lastGraphResult.PrimaryStageCorners.Count - 1];

            var landingNodeIndex = _lastGraphResult.TransitionNodeIndex + 1;
            if (_lastGraphResult.NodePath != null && landingNodeIndex >= 0 && landingNodeIndex < _lastGraphResult.NodePath.Count)
            {
                var landingNode = _lastGraphResult.NodePath[landingNodeIndex];
                transitionLandingPosition = new Vector3(landingNode.x, landingNode.y, landingNode.z);
            }
            else
            {
                transitionLandingPosition = transitionNodePosition;
            }

            return true;
        }
    }
}
