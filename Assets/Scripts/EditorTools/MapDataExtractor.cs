using UnityEngine;
using System.Collections.Generic;
using System.IO;
using NavAR.Core.Entities;

namespace NavAR.EditorTools
{
    // A wrapper class just to hold both lists for easy JSON export
    [System.Serializable]
    public class MapDataExport
    {
        public List<QRAnchor> anchors = new List<QRAnchor>();
        public List<Destination> destinations = new List<Destination>();
        public List<DestinationEntrance> entrances = new List<DestinationEntrance>();
        public List<GraphNode> nodes = new List<GraphNode>();
        public List<GraphEdge> edges = new List<GraphEdge>();
    }

    public class MapDataExtractor : MonoBehaviour
    {
        [Header("Drag your parent folders here")]
        public Transform qrsParent;
        public Transform targetsParent;
        public Transform nodesParent;
        public int currentFloorId = 0;

        [Header("Node Generation")]
        public bool autoGenerateNodes = true;
        public bool generateNodesFromQrs = true;
        public bool generateNodesFromTargets = true;
        public bool clearExistingNodes = false;
        public string nodeIdPrefix = "Block-H-F{floor}-N-";
        public NodeType nodeTypeForQrs = NodeType.Corridor;
        public NodeType nodeTypeForTargets = NodeType.Entrance;

        [Header("Auto Edge Generation")]
        public bool autoGenerateEdges = false;
        public bool autoConnectQrToTargets = true;
        public bool autoConnectWithinGroups = true;
        public bool autoBidirectionalEdges = true;
        public EdgeType autoEdgeType = EdgeType.Corridor;
        public bool autoEdgeAccessible = true;

        // The [ContextMenu] attribute allows us to run this method directly 
        // from the Unity Editor without pressing Play!
        [ContextMenu("Extract Data to JSON")]
        public void ExtractData()
        {
            MapDataExport exportData = new MapDataExport();

            EnsureNodesParent();

            if (autoGenerateNodes)
            {
                AutoGenerateNodes();
            }

            // 1. Extract QRs
            if (qrsParent != null)
            {
                foreach (Transform child in qrsParent)
                {
                    exportData.anchors.Add(new QRAnchor
                    {
                        qr_id = child.name, // Uses the GameObject's name
                        floor_id = currentFloorId,
                        location_name = child.name,
                        // CRITICAL: We use localPosition so it stays relative to the floor origin!
                        x = child.localPosition.x,
                        y = child.localPosition.y, 
                        z = child.localPosition.z,
                        rotation_y = child.localEulerAngles.y
                    });
                }
            }

            // 2. Extract Destinations
            if (targetsParent != null)
            {
                foreach (Transform child in targetsParent)
                {
                    exportData.destinations.Add(new Destination
                    {
                        destination_id = child.name,
                        floor_id = currentFloorId,
                        name = child.name,
                        category = "Extracted", // You can update categories later
                        target_x = child.localPosition.x,
                        target_y = child.localPosition.y,
                        target_z = child.localPosition.z
                    });

                    var entrance = BuildEntranceFromTarget(child);
                    if (entrance != null)
                    {
                        exportData.entrances.Add(entrance);
                    }
                }
            }

            // 3. Extract Nodes
            if (nodesParent != null)
            {
                foreach (Transform child in nodesParent)
                {
                    var marker = child.GetComponent<NodeMarker>();
                    if (marker == null)
                    {
                        continue;
                    }

                    var nodeId = EnsureNodeId(marker);
                    exportData.nodes.Add(new GraphNode
                    {
                        node_id = nodeId,
                        floor_id = currentFloorId,
                        x = child.localPosition.x,
                        y = child.localPosition.y,
                        z = child.localPosition.z,
                        node_type = marker.node_type,
                        is_accessible = marker.is_accessible
                    });
                }
            }

            // 4. Extract Edges
            if (nodesParent != null)
            {
                var edgeIds = new HashSet<string>();
                foreach (Transform child in nodesParent)
                {
                    var marker = child.GetComponent<NodeMarker>();
                    var link = child.GetComponent<NodeEdgeLink>();
                    if (marker == null || link == null)
                    {
                        continue;
                    }

                    var fromId = EnsureNodeId(marker);
                    foreach (var target in link.targets)
                    {
                        if (target == null)
                        {
                            continue;
                        }

                        var toId = EnsureNodeId(target);
                        if (string.IsNullOrWhiteSpace(fromId) || string.IsNullOrWhiteSpace(toId))
                        {
                            continue;
                        }

                        var edgeId = $"{fromId}->{toId}";
                        if (edgeIds.Contains(edgeId))
                        {
                            continue;
                        }

                        edgeIds.Add(edgeId);
                        exportData.edges.Add(new GraphEdge
                        {
                            edge_id = edgeId,
                            from_node_id = fromId,
                            to_node_id = toId,
                            distance = Vector3.Distance(child.localPosition, target.transform.localPosition),
                            edge_type = link.edge_type,
                            is_accessible = link.is_accessible
                        });
                    }
                }

                if (autoGenerateEdges)
                {
                    AddAutoEdges(exportData, edgeIds);
                }
            }

            // 5. Convert to JSON and Save
            string json = JsonUtility.ToJson(exportData, true); // 'true' makes it pretty-print
            string path = Application.dataPath + $"/Floor{currentFloorId}_Data.json";
            
            File.WriteAllText(path, json);
            
            Debug.Log($"[MapDataExtractor] Success! Data saved to: {path}");
            Debug.Log(json); // Also print to console so you can see it instantly
        }

        private void EnsureNodesParent()
        {
            if (nodesParent != null)
            {
                return;
            }

            var parent = new GameObject("[NODE_GRAPH]");
            parent.transform.SetParent(transform, false);
            nodesParent = parent.transform;
            Debug.LogWarning("[MapDataExtractor] nodesParent was not set. Created [NODE_GRAPH] under the extractor.");
        }

        private void AutoGenerateNodes()
        {
            if (nodesParent == null)
            {
                return;
            }

            if (clearExistingNodes)
            {
                var children = new List<Transform>();
                foreach (Transform child in nodesParent)
                {
                    children.Add(child);
                }

                foreach (var child in children)
                {
                    DestroyImmediate(child.gameObject);
                }
            }
            else
            {
                foreach (Transform child in nodesParent)
                {
                    if (child.GetComponent<NodeMarker>() != null)
                    {
                        Debug.LogWarning("[MapDataExtractor] Nodes already exist under nodesParent. Auto-generation skipped.");
                        return;
                    }
                }
            }

            if (generateNodesFromQrs && qrsParent != null)
            {
                foreach (Transform child in qrsParent)
                {
                    CreateNodeFromSource(child, nodeTypeForQrs);
                }
            }

            if (generateNodesFromTargets && targetsParent != null)
            {
                foreach (Transform child in targetsParent)
                {
                    CreateNodeFromSource(child, nodeTypeForTargets);
                }
            }
        }

        private NodeMarker CreateNodeFromSource(Transform source, NodeType nodeType)
        {
            var node = new GameObject();
            node.transform.SetParent(nodesParent, false);
            node.transform.localPosition = source.localPosition;
            node.transform.localRotation = source.localRotation;

            var marker = node.AddComponent<NodeMarker>();
            marker.node_type = nodeType;
            marker.is_accessible = true;
            marker.source_name = source.name;
            marker.node_id = GenerateNodeId();
            node.name = marker.node_id;
            return marker;
        }

        private string GenerateNodeId()
        {
            var prefix = ResolveNodePrefix();
            var maxIndex = 0;

            foreach (Transform child in nodesParent)
            {
                var marker = child.GetComponent<NodeMarker>();
                if (marker == null || string.IsNullOrWhiteSpace(marker.node_id))
                {
                    continue;
                }

                if (!marker.node_id.StartsWith(prefix))
                {
                    continue;
                }

                var suffix = marker.node_id.Substring(prefix.Length);
                if (int.TryParse(suffix, out var parsed))
                {
                    if (parsed > maxIndex)
                    {
                        maxIndex = parsed;
                    }
                }
            }

            return $"{prefix}{(maxIndex + 1):D3}";
        }

        private string EnsureNodeId(NodeMarker marker)
        {
            if (!string.IsNullOrWhiteSpace(marker.node_id))
            {
                return marker.node_id;
            }

            marker.node_id = GenerateNodeId();
            marker.gameObject.name = marker.node_id;
            return marker.node_id;
        }

        private string ResolveNodePrefix()
        {
            if (string.IsNullOrWhiteSpace(nodeIdPrefix))
            {
                return $"Block-H-F{currentFloorId}-N-";
            }

            return nodeIdPrefix.Replace("{floor}", currentFloorId.ToString());
        }

        private DestinationEntrance BuildEntranceFromTarget(Transform target)
        {
            if (target == null)
            {
                return null;
            }

            var nodeId = string.Empty;
            if (nodesParent != null)
            {
                foreach (Transform child in nodesParent)
                {
                    var marker = child.GetComponent<NodeMarker>();
                    if (marker != null && marker.source_name == target.name)
                    {
                        nodeId = marker.node_id;
                        break;
                    }
                }
            }

            return new DestinationEntrance
            {
                entrance_id = target.name,
                destination_id = target.name,
                node_id = nodeId,
                is_primary = true,
                tags = "seed"
            };
        }

        private void AddAutoEdges(MapDataExport exportData, HashSet<string> edgeIds)
        {
            if (nodesParent == null)
            {
                return;
            }

            var qrNames = new HashSet<string>();
            if (qrsParent != null)
            {
                foreach (Transform child in qrsParent)
                {
                    qrNames.Add(child.name);
                }
            }

            var targetNames = new HashSet<string>();
            if (targetsParent != null)
            {
                foreach (Transform child in targetsParent)
                {
                    targetNames.Add(child.name);
                }
            }

            var qrNodes = new List<NodeMarker>();
            var targetNodes = new List<NodeMarker>();

            foreach (Transform child in nodesParent)
            {
                var marker = child.GetComponent<NodeMarker>();
                if (marker == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(marker.source_name))
                {
                    if (qrNames.Contains(marker.source_name))
                    {
                        qrNodes.Add(marker);
                        continue;
                    }

                    if (targetNames.Contains(marker.source_name))
                    {
                        targetNodes.Add(marker);
                    }
                }
            }

            if (autoConnectQrToTargets)
            {
                foreach (var qr in qrNodes)
                {
                    foreach (var target in targetNodes)
                    {
                        AddEdge(exportData, edgeIds, qr, target);
                        if (autoBidirectionalEdges)
                        {
                            AddEdge(exportData, edgeIds, target, qr);
                        }
                    }
                }
            }

            if (autoConnectWithinGroups)
            {
                AddEdgesWithinGroup(exportData, edgeIds, qrNodes);
                AddEdgesWithinGroup(exportData, edgeIds, targetNodes);
            }
        }

        private void AddEdgesWithinGroup(MapDataExport exportData, HashSet<string> edgeIds, List<NodeMarker> nodes)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                for (var j = i + 1; j < nodes.Count; j++)
                {
                    AddEdge(exportData, edgeIds, nodes[i], nodes[j]);
                    if (autoBidirectionalEdges)
                    {
                        AddEdge(exportData, edgeIds, nodes[j], nodes[i]);
                    }
                }
            }
        }

        private void AddEdge(MapDataExport exportData, HashSet<string> edgeIds, NodeMarker from, NodeMarker to)
        {
            if (from == null || to == null)
            {
                return;
            }

            var fromId = EnsureNodeId(from);
            var toId = EnsureNodeId(to);
            if (string.IsNullOrWhiteSpace(fromId) || string.IsNullOrWhiteSpace(toId))
            {
                return;
            }

            var edgeId = $"{fromId}->{toId}";
            if (edgeIds.Contains(edgeId))
            {
                return;
            }

            edgeIds.Add(edgeId);
            exportData.edges.Add(new GraphEdge
            {
                edge_id = edgeId,
                from_node_id = fromId,
                to_node_id = toId,
                distance = Vector3.Distance(from.transform.localPosition, to.transform.localPosition),
                edge_type = autoEdgeType,
                is_accessible = autoEdgeAccessible
            });
        }
    }
}