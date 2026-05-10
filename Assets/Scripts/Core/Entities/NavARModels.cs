using System;

namespace NavAR.Core.Entities
{
    // [Serializable] allows Unity to save this data offline and convert it to/from JSON easily.

    public enum NodeType
    {
        Unknown = 0,
        Corridor = 1,
        Room = 2,
        Stair = 3,
        Elevator = 4,
        Entrance = 5
    }

    public enum EdgeType
    {
        Unknown = 0,
        Corridor = 1,
        Stair = 2,
        Elevator = 3,
        Ramp = 4
    }

    public enum SessionStatus
    {
        Unknown = 0,
        Started = 1,
        Completed = 2,
        Cancelled = 3,
        Failed = 4
    }

    [Serializable]
    public class Building
    {
        public int building_id;
        public string name;
        public string description;
    }

    [Serializable]
    public class Floor
    {
        public int floor_id;
        public int building_id;
        public int floor_number;
        public string floor_label;
        public float elevation_offset;
    }

    [Serializable]
    public class QRAnchor
    {
        public string qr_id; // e.g., "QR-MB-001" (From your thesis page 79)
        public int floor_id;
        public string location_name; // e.g., "Main Entrance Lobby"
        public float x;
        public float y;
        public float z;
        public float rotation_y; // To align the AR world
    }

    [Serializable]
    public class Destination
    {
        public string destination_id;
        public int floor_id;
        public string name; // e.g., "Lab 304"
        public string category; // e.g., "Offices", "Labs", "Restrooms"
        public float target_x;
        public float target_y;
        public float target_z;
    }

    [Serializable]
    public class DestinationEntrance
    {
        public string entrance_id;
        public string destination_id;
        public string node_id;
        public bool is_primary;
        public string tags; // Optional: comma-separated flags like "accessible,staff-only"
    }

    [Serializable]
    public class GraphNode
    {
        public string node_id;
        public int floor_id;
        public float x;
        public float y;
        public float z;
        public NodeType node_type;
        public bool is_accessible;
    }

    [Serializable]
    public class GraphEdge
    {
        public string edge_id;
        public string from_node_id;
        public string to_node_id;
        public float distance;
        public EdgeType edge_type;
        public bool is_accessible;
    }

    [Serializable]
    public class NavigationSession
    {
        public string session_id;
        public int floor_id;
        public string start_qr_id;
        public string destination_id;
        public long start_time;
        public long end_time;
        public SessionStatus completion_status;
    }

    [Serializable]
    public class TelemetryRecord
    {
        public string session_id;
        public long timestamp;
        public float x;
        public float y;
        public float z;
        public float pose_confidence;
    }

    [Serializable]
    public class UserFeedback
    {
        public int rating;
        public string issue_type; // e.g., "Wrong direction", "AR drift"
        public string comments;
        public string location_context;
    }
}