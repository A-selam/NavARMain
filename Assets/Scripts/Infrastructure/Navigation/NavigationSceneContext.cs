using UnityEngine;
using NavAR.Core.Interfaces;

namespace NavAR.Infrastructure
{
    public class NavigationSceneContext : MonoBehaviour
    {
        [Header("Scene Navigation Services")]
        [SerializeField] private AlignmentService alignmentService;
        [SerializeField] private NavMeshPathCalculator pathCalculator;
        [SerializeField] private ArPathRenderer pathRenderer;

        public AlignmentService AlignmentService => alignmentService;
        public IPathCalculator PathCalculator => pathCalculator;
        public IArRenderer PathRenderer => pathRenderer;

        public bool TryResolve(out AlignmentService resolvedAlignmentService, out IPathCalculator resolvedPathCalculator, out IArRenderer resolvedPathRenderer)
        {
            resolvedAlignmentService = alignmentService;
            resolvedPathCalculator = pathCalculator;
            resolvedPathRenderer = pathRenderer;

            return resolvedAlignmentService != null && resolvedPathCalculator != null && resolvedPathRenderer != null;
        }
    }
}