using UnityEngine;
using System.Collections.Generic;
using NavAR.Core.Interfaces;

namespace NavAR.Infrastructure
{
    // This script draws the line in the AR world
    [RequireComponent(typeof(LineRenderer))]
    public class ArPathRenderer : MonoBehaviour, IArRenderer
    {
        private LineRenderer lineRenderer;

        void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer != null)
            {
                lineRenderer.startWidth = 0.1f;
                lineRenderer.endWidth = 0.1f;
            }
        }

        /// <summary>
        /// Ensures the LineRenderer reference is valid, recreating if necessary.
        /// </summary>
        private LineRenderer EnsureLineRenderer()
        {
            // If we don't have a reference or the gameObject was destroyed, try to get a fresh one
            if (lineRenderer == null)
            {
                // Check if this MonoBehaviour's GameObject still exists
                if (gameObject == null)
                {
                    Debug.LogWarning("[ArPathRenderer] This ArPathRenderer's GameObject was destroyed.");
                    return null;
                }

                lineRenderer = GetComponent<LineRenderer>();
            }

            if (lineRenderer == null)
            {
                // Check if gameObject is still valid before adding component
                if (gameObject == null)
                {
                    Debug.LogWarning("[ArPathRenderer] Cannot add LineRenderer: GameObject was destroyed.");
                    return null;
                }

                Debug.LogWarning("[ArPathRenderer] LineRenderer component is missing, attempting to add it.");
                lineRenderer = gameObject.AddComponent<LineRenderer>();
                if (lineRenderer != null)
                {
                    lineRenderer.startWidth = 0.1f;
                    lineRenderer.endWidth = 0.1f;
                }
            }

            return lineRenderer;
        }

        public void DrawPath(List<Vector3> pathCorners)
        {
            if (pathCorners == null || pathCorners.Count < 2) return;
            
            var renderer = EnsureLineRenderer();
            if (renderer != null)
            {
                renderer.positionCount = pathCorners.Count;
                renderer.SetPositions(pathCorners.ToArray());
            }
        }
        
        public void ClearPath()
        {
            var renderer = EnsureLineRenderer();
            if (renderer != null)
            {
                renderer.positionCount = 0;
            }
        }
    }
}