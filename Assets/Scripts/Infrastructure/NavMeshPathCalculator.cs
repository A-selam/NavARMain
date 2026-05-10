using UnityEngine;
using UnityEngine.AI; // Required for NavMesh
using System.Collections.Generic;
using NavAR.Core.Interfaces;

namespace NavAR.Infrastructure
{
    public class NavMeshPathCalculator : MonoBehaviour, IPathCalculator
    {
        // This calculates the path using Unity's baked NavMesh
        public List<Vector3> CalculatePath(Vector3 start, Vector3 end)
        {
            NavMeshPath path = new NavMeshPath();
            
            // Calculate the path on the NavMesh
            if (NavMesh.CalculatePath(start, end, NavMesh.AllAreas, path))
            {
                // Convert to List<Vector3> to keep it Core-friendly
                return new List<Vector3>(path.corners);
            }
            
            Debug.LogError("[NavMeshPathCalculator] Path not found!");
            return null;
        }
    }
}