using System.Collections.Generic;
using UnityEngine;
using NavAR.Core.Entities;
using NavAR.Core.Interfaces;

namespace NavAR.Infrastructure.Navigation
{
    public class NavMeshEntranceSelector : IEntranceSelector
    {
        private readonly IPathCalculator _pathCalculator;

        public NavMeshEntranceSelector(IPathCalculator pathCalculator)
        {
            _pathCalculator = pathCalculator;
        }

        public Destination SelectBestEntrance(QRAnchor startAnchor, List<Destination> entrances)
        {
            if (entrances == null || entrances.Count == 0)
            {
                return null;
            }

            if (startAnchor == null || _pathCalculator == null)
            {
                return entrances[0];
            }

            var startPos = new Vector3(startAnchor.x, startAnchor.y, startAnchor.z);
            Destination bestEntrance = null;
            var bestCost = float.PositiveInfinity;
            var foundPath = false;

            foreach (var entrance in entrances)
            {
                var targetPos = new Vector3(entrance.target_x, entrance.target_y, entrance.target_z);
                var path = _pathCalculator.CalculatePath(startPos, targetPos);

                if (path != null && path.Count > 1)
                {
                    var cost = CalculatePathLength(path);
                    if (!foundPath || cost < bestCost)
                    {
                        bestCost = cost;
                        bestEntrance = entrance;
                        foundPath = true;
                    }
                }
                else if (!foundPath)
                {
                    var fallbackCost = Vector3.Distance(startPos, targetPos);
                    if (fallbackCost < bestCost)
                    {
                        bestCost = fallbackCost;
                        bestEntrance = entrance;
                    }
                }
            }

            return bestEntrance ?? entrances[0];
        }

        private static float CalculatePathLength(List<Vector3> corners)
        {
            var length = 0f;
            for (var i = 1; i < corners.Count; i++)
            {
                length += Vector3.Distance(corners[i - 1], corners[i]);
            }
            return length;
        }
    }
}
