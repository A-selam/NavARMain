using UnityEngine;
using NavAR.Core.Entities;

namespace NavAR.EditorTools
{
    public class NodeMarker : MonoBehaviour
    {
        public string node_id;
        public string source_name;
        public NodeType node_type = NodeType.Corridor;
        public bool is_accessible = true;
    }
}
