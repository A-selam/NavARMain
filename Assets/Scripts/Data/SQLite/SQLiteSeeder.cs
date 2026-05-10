using System.Collections.Generic;
using SQLite4Unity3d;
using UnityEngine;
using NavAR.Core.Entities;

namespace NavAR.Data.SQLite
{
    public class SQLiteSeeder
    {
        private readonly SQLiteConnection _db;

        public SQLiteSeeder(SQLiteConnection db)
        {
            _db = db;
        }

        public void SeedIfNeeded()
        {
            var destinationCount = _db.Table<DbDestination>().Count();
            if (destinationCount > 0)
            {
                return;
            }

            // Load all TextAssets in Resources and pick those whose names start with "Floor"
            var allTextAssets = Resources.LoadAll<TextAsset>("");
            var floorAssets = new List<TextAsset>();
            foreach (var ta in allTextAssets)
            {
                if (ta == null) continue;
                if (ta.name.StartsWith("Floor"))
                {
                    floorAssets.Add(ta);
                }
            }

            if (floorAssets.Count == 0)
            {
                Debug.LogError("[SQLiteSeeder] Could not find any Floor*_Data.json files in Resources.");
                return;
            }

            // Aggregate all pieces first to avoid ordering problems (nodes referenced by cross-floor edges)
            var aggAnchors = new List<QRAnchor>();
            var aggDestinations = new List<Destination>();
            var aggEntrances = new List<DestinationEntrance>();
            var aggNodes = new List<GraphNode>();
            var aggEdges = new List<GraphEdge>();

            foreach (var asset in floorAssets)
            {
                var parsed = JsonUtility.FromJson<SeedDataWrapper>(asset.text);
                if (parsed == null)
                {
                    Debug.LogWarning($"[SQLiteSeeder] Failed to parse {asset.name}, skipping.");
                    continue;
                }

                if (parsed.anchors != null) aggAnchors.AddRange(parsed.anchors);
                if (parsed.destinations != null) aggDestinations.AddRange(parsed.destinations);
                if (parsed.entrances != null) aggEntrances.AddRange(parsed.entrances);
                if (parsed.nodes != null) aggNodes.AddRange(parsed.nodes);
                if (parsed.edges != null) aggEdges.AddRange(parsed.edges);
            }

            SeedAnchors(aggAnchors);
            SeedDestinations(aggDestinations, aggEntrances);
            SeedNodes(aggNodes);
            SeedEdges(aggEdges);
        }

        private void SeedAnchors(List<QRAnchor> anchors)
        {
            if (anchors == null || anchors.Count == 0)
            {
                return;
            }

            var dbAnchors = new List<DbQRAnchor>();
            foreach (var anchor in anchors)
            {
                dbAnchors.Add(new DbQRAnchor
                {
                    qr_id = anchor.qr_id,
                    floor_id = anchor.floor_id,
                    location_name = anchor.location_name,
                    qr_payload = anchor.qr_id,
                    x = anchor.x,
                    y = anchor.y,
                    z = anchor.z,
                    rotation_y = anchor.rotation_y
                });
            }

            _db.InsertAll(dbAnchors);
        }

        private void SeedDestinations(List<Destination> destinations, List<DestinationEntrance> entrances)
        {
            if (destinations == null || destinations.Count == 0)
            {
                return;
            }

            var dbDestinations = new List<DbDestination>();
            var dbEntrances = new List<DbDestinationEntrance>();

            foreach (var dest in destinations)
            {
                dbDestinations.Add(new DbDestination
                {
                    destination_id = dest.destination_id,
                    floor_id = dest.floor_id,
                    name = dest.name,
                    category = dest.category,
                    target_x = dest.target_x,
                    target_y = dest.target_y,
                    target_z = dest.target_z
                });

                dbEntrances.Add(new DbDestinationEntrance
                {
                    entrance_id = dest.destination_id,
                    destination_id = dest.destination_id,
                    node_id = null,
                    is_primary = true,
                    tags = "seed"
                });
            }

            if (entrances != null && entrances.Count > 0)
            {
                dbEntrances.Clear();
                foreach (var entrance in entrances)
                {
                    dbEntrances.Add(new DbDestinationEntrance
                    {
                        entrance_id = entrance.entrance_id,
                        destination_id = entrance.destination_id,
                        node_id = entrance.node_id,
                        is_primary = entrance.is_primary,
                        tags = entrance.tags
                    });
                }
            }

            _db.InsertAll(dbDestinations);
            _db.InsertAll(dbEntrances);
        }

        private void SeedNodes(List<GraphNode> nodes)
        {
            if (nodes == null || nodes.Count == 0)
            {
                return;
            }

            var dbNodes = new List<DbGraphNode>();
            foreach (var node in nodes)
            {
                dbNodes.Add(new DbGraphNode
                {
                    node_id = node.node_id,
                    floor_id = node.floor_id,
                    x = node.x,
                    y = node.y,
                    z = node.z,
                    node_type = (int)node.node_type,
                    is_accessible = node.is_accessible
                });
            }

            _db.InsertAll(dbNodes);
        }

        private void SeedEdges(List<GraphEdge> edges)
        {
            if (edges == null || edges.Count == 0)
            {
                return;
            }

            var dbEdges = new List<DbGraphEdge>();
            foreach (var edge in edges)
            {
                dbEdges.Add(new DbGraphEdge
                {
                    edge_id = edge.edge_id,
                    from_node_id = edge.from_node_id,
                    to_node_id = edge.to_node_id,
                    distance = edge.distance,
                    edge_type = (int)edge.edge_type,
                    is_accessible = edge.is_accessible
                });
            }

            _db.InsertAll(dbEdges);
        }

        [System.Serializable]
        private class SeedDataWrapper
        {
            public List<QRAnchor> anchors = new List<QRAnchor>();
            public List<Destination> destinations = new List<Destination>();
            public List<DestinationEntrance> entrances = new List<DestinationEntrance>();
            public List<GraphNode> nodes = new List<GraphNode>();
            public List<GraphEdge> edges = new List<GraphEdge>();
        }
    }
}
