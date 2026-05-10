using System.Collections.Generic;
using System.Linq;
using SQLite4Unity3d;
using UnityEngine;
using NavAR.Core.Entities;
using NavAR.Core.Interfaces;

namespace NavAR.Data.SQLite
{
    public class SQLiteMapRepository : IMapRepository
    {
        private readonly SQLiteConnection _db;

        public SQLiteMapRepository()
        {
            var dbPath = SQLitePaths.GetDatabasePath();
            _db = new SQLiteConnection(dbPath);

            CreateTables();

            var seeder = new SQLiteSeeder(_db);
            seeder.SeedIfNeeded();
        }

        public List<Destination> GetAllDestinations()
        {
            return _db.Table<DbDestination>()
                .ToList()
                .Select(MapDestination)
                .ToList();
        }

        public List<Destination> GetDestinationsByCategory(string category)
        {
            return _db.Table<DbDestination>()
                .Where(d => d.category == category)
                .ToList()
                .Select(MapDestination)
                .ToList();
        }

        public List<DestinationEntrance> GetDestinationEntrances(string destinationId)
        {
            return _db.Table<DbDestinationEntrance>()
                .Where(e => e.destination_id == destinationId)
                .ToList()
                .Select(MapEntrance)
                .ToList();
        }

        public List<GraphNode> GetGraphNodes(int floorId)
        {
            return _db.Table<DbGraphNode>()
                .Where(n => n.floor_id == floorId)
                .ToList()
                .Select(MapNode)
                .ToList();
        }

        public List<GraphEdge> GetGraphEdges(int floorId)
        {
            var nodeIds = _db.Table<DbGraphNode>()
                .Where(n => n.floor_id == floorId)
                .ToList()
                .Select(n => n.node_id)
                .ToList();

            if (nodeIds.Count == 0)
            {
                return new List<GraphEdge>();
            }

            var nodeIdSet = new HashSet<string>(nodeIds);
            return _db.Table<DbGraphEdge>()
                .ToList()
                .Where(e => nodeIdSet.Contains(e.from_node_id) && nodeIdSet.Contains(e.to_node_id))
                .Select(MapEdge)
                .ToList();
        }

        // Returns all graph edges in the database (including cross-floor edges)
        public List<GraphEdge> GetAllGraphEdges()
        {
            return _db.Table<DbGraphEdge>()
                .ToList()
                .Select(MapEdge)
                .ToList();
        }

        public QRAnchor GetQRAnchor(string qrPayload)
        {
            var dbAnchor = _db.Table<DbQRAnchor>()
                .FirstOrDefault(a => a.qr_payload == qrPayload || a.qr_id == qrPayload);

            if (dbAnchor == null)
            {
                Debug.LogWarning($"[SQLiteMapRepository] Could not find QR Anchor with ID: {qrPayload}");
                return null;
            }

            return MapAnchor(dbAnchor);
        }

        private void CreateTables()
        {
            _db.CreateTable<DbQRAnchor>();
            _db.CreateTable<DbDestination>();
            _db.CreateTable<DbDestinationEntrance>();
            _db.CreateTable<DbGraphNode>();
            _db.CreateTable<DbGraphEdge>();
            _db.CreateTable<DbNavigationSession>();
            _db.CreateTable<DbTelemetryRecord>();
        }

        private static Destination MapDestination(DbDestination db)
        {
            return new Destination
            {
                destination_id = db.destination_id,
                floor_id = db.floor_id,
                name = db.name,
                category = db.category,
                target_x = db.target_x,
                target_y = db.target_y,
                target_z = db.target_z
            };
        }

        private static DestinationEntrance MapEntrance(DbDestinationEntrance db)
        {
            return new DestinationEntrance
            {
                entrance_id = db.entrance_id,
                destination_id = db.destination_id,
                node_id = db.node_id,
                is_primary = db.is_primary,
                tags = db.tags
            };
        }

        private static GraphNode MapNode(DbGraphNode db)
        {
            return new GraphNode
            {
                node_id = db.node_id,
                floor_id = db.floor_id,
                x = db.x,
                y = db.y,
                z = db.z,
                node_type = (NodeType)db.node_type,
                is_accessible = db.is_accessible
            };
        }

        private static GraphEdge MapEdge(DbGraphEdge db)
        {
            return new GraphEdge
            {
                edge_id = db.edge_id,
                from_node_id = db.from_node_id,
                to_node_id = db.to_node_id,
                distance = db.distance,
                edge_type = (EdgeType)db.edge_type,
                is_accessible = db.is_accessible
            };
        }

        private static QRAnchor MapAnchor(DbQRAnchor db)
        {
            return new QRAnchor
            {
                qr_id = db.qr_id,
                floor_id = db.floor_id,
                location_name = db.location_name,
                x = db.x,
                y = db.y,
                z = db.z,
                rotation_y = db.rotation_y
            };
        }
    }
}
