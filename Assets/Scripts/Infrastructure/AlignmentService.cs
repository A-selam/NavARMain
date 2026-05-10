using UnityEngine;
using UnityEngine.XR.ARFoundation;
using NavAR.Core.Entities;

namespace NavAR.Infrastructure
{
    public class AlignmentService : MonoBehaviour
    {
        [SerializeField] private ARSession session;
        [SerializeField] private Transform xrOrigin; // Assign your XR Origin here

        // Allow runtime binding when the serialized references are not available in the current scene
        public void SetSession(ARSession s) => session = s;
        public void SetXROrigin(Transform t) => xrOrigin = t;

        public void Realign(QRAnchor anchor)
        {
            // 1. Reset AR Session to clear any previous drift/offsets (if available)
            if (session != null)
            {
                session.Reset();
            }
            else
            {
                Debug.LogWarning("[AlignmentService] ARSession is null. Skipping session reset.");
            }

            // 2. Position the XR Origin at the location of the QR Anchor
            if (xrOrigin != null)
            {
                xrOrigin.position = new Vector3(anchor.x, anchor.y, anchor.z);
                // 3. Rotate the XR Origin to match the anchor's alignment
                xrOrigin.rotation = Quaternion.Euler(0, anchor.rotation_y, 0);
                Debug.Log($"[AlignmentService] Aligned to {anchor.location_name} at ({anchor.x}, {anchor.y}, {anchor.z})");
            }
            else
            {
                Debug.LogError("[AlignmentService] XR Origin is null. Cannot reposition or rotate the origin.");
            }
        }

        private void Awake()
        {
            // Try to auto-assign if someone forgot to wire the references in the Inspector
            if (session == null)
            {
                session = FindObjectOfType<ARSession>();
            }

            if (xrOrigin == null)
            {
                var originComp = FindObjectOfType<ARSessionOrigin>();
                if (originComp != null)
                {
                    xrOrigin = originComp.transform;
                }
                else
                {
                    var go = GameObject.Find("XROrigin");
                    if (go != null) xrOrigin = go.transform;
                }
            }
        }
    }
}