using SQLite4Unity3d;

namespace NavAR.Data.SQLite
{
    [Table("qr_anchors")]
    public class DbQRAnchor
    {
        [PrimaryKey]
        public string qr_id { get; set; }
        [Indexed]
        public int floor_id { get; set; }
        public string location_name { get; set; }
        public string qr_payload { get; set; }
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }
        public float rotation_y { get; set; }
    }

    [Table("destinations")]
    public class DbDestination
    {
        [PrimaryKey]
        public string destination_id { get; set; }
        [Indexed]
        public int floor_id { get; set; }
        public string name { get; set; }
        public string category { get; set; }
        public float target_x { get; set; }
        public float target_y { get; set; }
        public float target_z { get; set; }
    }

    [Table("destination_entrances")]
    public class DbDestinationEntrance
    {
        [PrimaryKey]
        public string entrance_id { get; set; }
        [Indexed]
        public string destination_id { get; set; }
        public string node_id { get; set; }
        public bool is_primary { get; set; }
        public string tags { get; set; }
    }

    [Table("graph_nodes")]
    public class DbGraphNode
    {
        [PrimaryKey]
        public string node_id { get; set; }
        [Indexed]
        public int floor_id { get; set; }
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }
        public int node_type { get; set; }
        public bool is_accessible { get; set; }
    }

    [Table("graph_edges")]
    public class DbGraphEdge
    {
        [PrimaryKey]
        public string edge_id { get; set; }
        public string from_node_id { get; set; }
        public string to_node_id { get; set; }
        public float distance { get; set; }
        public int edge_type { get; set; }
        public bool is_accessible { get; set; }
    }

    [Table("navigation_sessions")]
    public class DbNavigationSession
    {
        [PrimaryKey]
        public string session_id { get; set; }
        [Indexed]
        public int floor_id { get; set; }
        public string start_qr_id { get; set; }
        public string destination_id { get; set; }
        public long start_time { get; set; }
        public long end_time { get; set; }
        public int completion_status { get; set; }
    }

    [Table("telemetry_records")]
    public class DbTelemetryRecord
    {
        [PrimaryKey, AutoIncrement]
        public int telemetry_id { get; set; }
        [Indexed]
        public string session_id { get; set; }
        public long timestamp { get; set; }
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }
        public float pose_confidence { get; set; }
    }
}
