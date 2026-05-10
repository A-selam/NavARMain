using NavAR.Core.Entities;

namespace NavAR.Core.State
{
    public class NavigationContext
    {
        public int CurrentFloorId { get; set; }
        public QRAnchor LastScannedAnchor { get; set; }
        public Destination CurrentDestination { get; set; }
        public int PendingFloorId { get; set; }
        public string PendingFloorLabel { get; set; }
        public string PendingTransitionNodeId { get; set; }

        public void ClearSession()
        {
            LastScannedAnchor = null;
            CurrentDestination = null;
            PendingFloorId = 0;
            PendingFloorLabel = null;
            PendingTransitionNodeId = null;
        }
    }
}