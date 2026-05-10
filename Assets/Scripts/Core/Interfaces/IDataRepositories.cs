using System.Collections.Generic;
using NavAR.Core.Entities;

namespace NavAR.Core.Interfaces
{
    // This handles fetching offline/online map data
    public interface IMapRepository
    {
        List<Destination> GetAllDestinations();
        List<Destination> GetDestinationsByCategory(string category);

        List<DestinationEntrance> GetDestinationEntrances(string destinationId);
        List<GraphNode> GetGraphNodes(int floorId);
        List<GraphEdge> GetGraphEdges(int floorId);
        List<GraphEdge> GetAllGraphEdges();

        // When we scan a QR code, we use this to get the exact X,Y,Z coordinates from the database
        QRAnchor GetQRAnchor(string qrPayload);
    }

    // This handles saving analytics and feedback (Epic 7)
    public interface ITelemetryRepository
    {
        void SaveTelemetry(TelemetryRecord record);
        void SubmitFeedback(UserFeedback feedback);
    }
}