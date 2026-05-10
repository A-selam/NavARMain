using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NavAR.Core.Entities;
using NavAR.Core.Interfaces;

namespace NavAR.Core.Navigation
{
    /// <summary>
    /// Dijkstra-based graph path router for multi-floor navigation.
    /// Detects floor transitions and constructs paths for rendering.
    /// </summary>
    public class DijkstraGraphRouter : IGraphPathRouter
    {
        private readonly IMapRepository _repository;
        private readonly bool _enableDiagnostics;

        public DijkstraGraphRouter(IMapRepository repository, bool enableDiagnostics = false)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _enableDiagnostics = enableDiagnostics;
        }

        // Backwards-compatible interface implementation (keeps original signature)
        public GraphRoutingResult CalculateGraphPath(
            Vector3 startPosition,
            Vector3 endPosition,
            int currentFloorId
        )
        {
            return CalculateGraphPath(startPosition, endPosition, currentFloorId, null);
        }

        // Extended implementation supporting an optional destination floor preference
        public GraphRoutingResult CalculateGraphPath(
            Vector3 startPosition,
            Vector3 endPosition,
            int currentFloorId,
            int? destinationFloorId = null
        )
        {
            var result = new GraphRoutingResult();

            try
            {
                if (_enableDiagnostics)
                {
                    Debug.Log(
                        $"[DijkstraGraphRouter] CalculateGraphPath called: " +
                        $"start={startPosition}, end={endPosition}, floor={currentFloorId}, destFloor={destinationFloorId}"
                    );
                }

                // 1. Get all nodes on the current floor
                var nodesOnFloor = _repository.GetGraphNodes(currentFloorId);
                if (nodesOnFloor.Count == 0)
                {
                    result.ErrorMessage = $"No graph nodes found on floor {currentFloorId}.";
                    Debug.LogWarning($"[DijkstraGraphRouter] {result.ErrorMessage}");
                    return result;
                }

                if (_enableDiagnostics)
                {
                    Debug.Log($"[DijkstraGraphRouter] Found {nodesOnFloor.Count} nodes on floor {currentFloorId}");
                }

                // 2. Find the nearest start node
                var startNode = FindNearestNode(startPosition, nodesOnFloor);
                if (startNode == null)
                {
                    result.ErrorMessage = "Could not find a valid starting node.";
                    Debug.LogWarning($"[DijkstraGraphRouter] {result.ErrorMessage}");
                    return result;
                }

                // 3. Find the nearest end node (PREFER destination floor, then current floor, then global)
                var endNode = null as GraphNode;
                
                // If destination floor is specified and different from current floor, search there first
                if (destinationFloorId.HasValue && destinationFloorId.Value != currentFloorId)
                {
                    var nodesOnDestFloor = _repository.GetGraphNodes(destinationFloorId.Value);
                    endNode = FindNearestNode(endPosition, nodesOnDestFloor);
                    if (_enableDiagnostics && endNode != null)
                    {
                        Debug.Log($"[DijkstraGraphRouter] Found end node on destination floor {destinationFloorId.Value}");
                    }
                }
                
                // Fallback: search current floor
                if (endNode == null)
                {
                    endNode = FindNearestNode(endPosition, nodesOnFloor);
                }
                
                // Fallback: search globally
                if (endNode == null)
                {
                    endNode = FindNearestNodeGlobal(endPosition);
                }
                if (endNode == null)
                {
                    result.ErrorMessage = "Could not find a valid destination node.";
                    Debug.LogWarning($"[DijkstraGraphRouter] {result.ErrorMessage}");
                    return result;
                }

                if (_enableDiagnostics)
                {
                    Debug.Log(
                        $"[DijkstraGraphRouter] Start node: {startNode.node_id} (floor {startNode.floor_id}), " +
                        $"End node: {endNode.node_id} (floor {endNode.floor_id})"
                    );
                }

                // 4. Compute shortest path through the graph
                var nodePath = ComputeShortestPath(startNode, endNode);
                if (nodePath == null || nodePath.Count == 0)
                {
                    result.ErrorMessage = "No path found between start and end nodes.";
                    Debug.LogWarning($"[DijkstraGraphRouter] {result.ErrorMessage}");
                    return result;
                }

                if (_enableDiagnostics)
                {
                    Debug.Log($"[DijkstraGraphRouter] Computed path with {nodePath.Count} nodes");
                    Debug.Log($"[DijkstraGraphRouter] Dijkstra node route: {FormatNodePath(nodePath)}");
                }

                result.NodePath = nodePath;

                // 5. Detect floor transitions
                var transitionInfo = DetectFloorTransition(nodePath);
                if (transitionInfo.HasTransition)
                {
                    result.HasFloorTransition = true;
                    result.TransitionTargetFloorId = transitionInfo.TargetFloorId;
                    result.TransitionTargetLabel = transitionInfo.TargetLabel;
                    result.TransitionNodeId = transitionInfo.TransitionNodeId;
                    result.TransitionNodeIndex = transitionInfo.TransitionNodeIndex;

                    if (_enableDiagnostics)
                    {
                        Debug.Log(
                            $"[DijkstraGraphRouter] Floor transition detected to floor {transitionInfo.TargetFloorId} " +
                            $"at node {transitionInfo.TransitionNodeId}"
                        );
                    }
                }

                // 6. Convert node path to rendering path (Vector3 corners)
                result.PathCorners = ConvertNodePathToCorners(nodePath);
                SplitPathByTransition(result, nodePath);

                // 7. Calculate total distance
                result.TotalDistance = CalculateTotalDistance(nodePath);

                if (_enableDiagnostics)
                {
                    Debug.Log(
                        $"[DijkstraGraphRouter] Path computed: {result.PathCorners.Count} corners, " +
                        $"distance {result.TotalDistance:F2}m"
                    );
                    Debug.Log($"[DijkstraGraphRouter] Render corners: {FormatCorners(result.PathCorners)}");
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Graph routing error: {ex.Message}";
                Debug.LogError($"[DijkstraGraphRouter] Exception: {ex}");
            }

            return result;
        }

        /// <summary>
        /// Finds the nearest node to a position within a given node set.
        /// </summary>
        private GraphNode FindNearestNode(Vector3 position, List<GraphNode> nodes)
        {
            if (nodes == null || nodes.Count == 0)
            {
                return null;
            }

            return nodes
                .OrderBy(n => Vector3.Distance(position, new Vector3(n.x, n.y, n.z)))
                .FirstOrDefault();
        }

        /// <summary>
        /// Finds the nearest node globally across all floors.
        /// </summary>
        private GraphNode FindNearestNodeGlobal(Vector3 position)
        {
            var allFloors = new[] { 0, 1, 2, 3, 4, 5 }; // Adjust based on your building
            GraphNode bestNode = null;
            float bestDistance = float.MaxValue;

            foreach (var floorId in allFloors)
            {
                var nodesOnFloor = _repository.GetGraphNodes(floorId);
                var nearest = FindNearestNode(position, nodesOnFloor);

                if (nearest != null)
                {
                    float dist = Vector3.Distance(position, new Vector3(nearest.x, nearest.y, nearest.z));
                    if (dist < bestDistance)
                    {
                        bestDistance = dist;
                        bestNode = nearest;
                    }
                }
            }

            return bestNode;
        }

        /// <summary>
        /// Dijkstra's shortest path algorithm on the graph.
        /// </summary>
        private List<GraphNode> ComputeShortestPath(GraphNode start, GraphNode end)
        {
            // Collect all nodes reachable from start (including cross-floor edges)
            var allReachableNodes = new Dictionary<string, GraphNode>();
            var edgesByFloor = new Dictionary<int, List<GraphEdge>>();

            // Build edge lookup by source node
            var outgoingEdges = new Dictionary<string, List<GraphEdge>>();

            // First collect all nodes for each floor so we have the full node set
            for (int floorId = 0; floorId <= 5; floorId++)
            {
                var nodesOnFloor = _repository.GetGraphNodes(floorId);
                foreach (var node in nodesOnFloor)
                {
                    allReachableNodes[node.node_id] = node;
                }
            }

            // Load all edges (including cross-floor) and attach those whose source node exists
            var allEdges = _repository.GetAllGraphEdges();
            foreach (var edge in allEdges)
            {
                if (!allReachableNodes.ContainsKey(edge.from_node_id))
                {
                    // Source node not in our reachable set (shouldn't happen if nodes were seeded), skip
                    continue;
                }

                if (!outgoingEdges.ContainsKey(edge.from_node_id))
                {
                    outgoingEdges[edge.from_node_id] = new List<GraphEdge>();
                }
                outgoingEdges[edge.from_node_id].Add(edge);
            }

            // Dijkstra's algorithm
            var distances = new Dictionary<string, float>();
            var previous = new Dictionary<string, string>();
            var unvisited = new HashSet<string>();

            foreach (var nodeId in allReachableNodes.Keys)
            {
                distances[nodeId] = float.MaxValue;
                previous[nodeId] = null;
                unvisited.Add(nodeId);
            }

            distances[start.node_id] = 0;

            while (unvisited.Count > 0)
            {
                var current = unvisited
                    .OrderBy(id => distances[id])
                    .FirstOrDefault();

                if (current == null)
                {
                    break;
                }

                if (distances[current] == float.MaxValue)
                {
                    break; // Unreachable nodes
                }

                unvisited.Remove(current);

                if (current == end.node_id)
                {
                    break; // Found the shortest path to end
                }

                // Check all outgoing edges from current node
                if (outgoingEdges.ContainsKey(current))
                {
                    foreach (var edge in outgoingEdges[current])
                    {
                        if (!unvisited.Contains(edge.to_node_id))
                        {
                            continue;
                        }

                        float alt = distances[current] + edge.distance;
                        if (alt < distances[edge.to_node_id])
                        {
                            distances[edge.to_node_id] = alt;
                            previous[edge.to_node_id] = current;
                        }
                    }
                }
            }

            // Reconstruct path
            var path = new List<string>();
            var currentNodeId = end.node_id;

            while (currentNodeId != null)
            {
                path.Insert(0, currentNodeId);
                currentNodeId = previous.ContainsKey(currentNodeId) ? previous[currentNodeId] : null;
            }

            if (path.Count == 0 || path[0] != start.node_id)
            {
                return null; // No path found
            }

            // Convert node IDs to node objects
            var nodePath = path
                .Select(id => allReachableNodes.ContainsKey(id) ? allReachableNodes[id] : null)
                .Where(n => n != null)
                .ToList();

            return nodePath.Count > 0 ? nodePath : null;
        }

        /// <summary>
        /// Detects if a path crosses floors and returns transition info.
        /// </summary>
        private FloorTransitionInfo DetectFloorTransition(List<GraphNode> nodePath)
        {
            var info = new FloorTransitionInfo { HasTransition = false };

            if (nodePath == null || nodePath.Count < 2)
            {
                return info;
            }

            for (int i = 0; i < nodePath.Count - 1; i++)
            {
                var currentFloor = nodePath[i].floor_id;
                var nextFloor = nodePath[i + 1].floor_id;

                if (currentFloor != nextFloor)
                {
                    info.HasTransition = true;
                    info.TargetFloorId = nextFloor;
                    info.TransitionNodeId = nodePath[i].node_id;
                    info.TransitionNodeIndex = i;
                    info.TargetLabel = $"Floor {nextFloor}";
                    return info; // Return on first transition
                }
            }

            return info;
        }

        /// <summary>
        /// Converts a list of graph nodes to Vector3 corners for rendering.
        /// </summary>
        private List<Vector3> ConvertNodePathToCorners(List<GraphNode> nodePath)
        {
            return nodePath
                .Select(n => new Vector3(n.x, n.y, n.z))
                .ToList();
        }

        /// <summary>
        /// Splits the path into current-floor and continuation segments when a floor transition exists.
        /// </summary>
        private void SplitPathByTransition(GraphRoutingResult result, List<GraphNode> nodePath)
        {
            if (result == null || nodePath == null || nodePath.Count == 0)
            {
                return;
            }

            if (!result.HasFloorTransition || result.TransitionNodeIndex < 0 || result.TransitionNodeIndex >= nodePath.Count)
            {
                result.PrimaryStageCorners = ConvertNodePathToCorners(nodePath);
                result.ContinuationStageCorners = new List<Vector3>();
                return;
            }

            var primaryNodes = nodePath.Take(result.TransitionNodeIndex + 1).ToList();
            var continuationNodes = nodePath.Skip(result.TransitionNodeIndex).ToList();

            result.PrimaryStageCorners = ConvertNodePathToCorners(primaryNodes);
            result.ContinuationStageCorners = ConvertNodePathToCorners(continuationNodes);
        }

        /// <summary>
        /// Calculates total distance along the node path.
        /// </summary>
        private float CalculateTotalDistance(List<GraphNode> nodePath)
        {
            float total = 0;
            for (int i = 0; i < nodePath.Count - 1; i++)
            {
                var p1 = new Vector3(nodePath[i].x, nodePath[i].y, nodePath[i].z);
                var p2 = new Vector3(nodePath[i + 1].x, nodePath[i + 1].y, nodePath[i + 1].z);
                total += Vector3.Distance(p1, p2);
            }
            return total;
        }

        private static string FormatNodePath(List<GraphNode> nodePath)
        {
            if (nodePath == null || nodePath.Count == 0)
            {
                return "<empty>";
            }

            return string.Join(" -> ", nodePath.Select(node => $"{node.node_id}[F{node.floor_id}]"));
        }

        private static string FormatCorners(List<Vector3> corners)
        {
            if (corners == null || corners.Count == 0)
            {
                return "<empty>";
            }

            return string.Join(" -> ", corners.Select(corner => $"({corner.x:F2},{corner.y:F2},{corner.z:F2})"));
        }

        private class FloorTransitionInfo
        {
            public bool HasTransition { get; set; }
            public int TargetFloorId { get; set; }
            public string TargetLabel { get; set; }
            public string TransitionNodeId { get; set; }
            public int TransitionNodeIndex { get; set; } = -1;
        }
    }
}
