using System.Collections.Generic;
using UnityEngine;
using NavAR.Core.Entities;

namespace NavAR.EditorTools
{
    public class NodeEdgeLink : MonoBehaviour
    {
        public List<NodeMarker> targets = new List<NodeMarker>();
        public EdgeType edge_type = EdgeType.Corridor;
        public bool is_accessible = true;
    }
}
