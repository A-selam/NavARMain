using System;
using System.Collections.Generic;
using UnityEngine; // We use UnityEngine here just to access Vector3 (X,Y,Z math)
using NavAR.Core.Entities;

namespace NavAR.Core.Interfaces
{
    // 1. Handles the device camera and reading the QR sticker
    public interface IQrScannerService
    {
        // Starts the camera and triggers the callback when a code (like "QR-MB-001") is found
        void StartScanning(Action<string> onQrCodeScanned);
        void StopScanning();
    }

    // 2. Handles aligning the AR world with the real building
    public interface ILocalizationService
    {
        void CalibratePosition(Entities.QRAnchor anchor);

        // The App State Manager will check this to know if we should show the "Position Lost" UI
        bool IsTrackingLost();
    }

    // 3. Handles figuring out how to get from the user to the destination
    public interface IPathCalculator
    {
        List<Vector3> CalculatePath(Vector3 startPosition, Vector3 endPosition);
    }

    // 4. Handles drawing the blue arrows on the floor
    public interface IArRenderer
    {
        void DrawPath(List<Vector3> pathCorners);
        void ClearPath();
    }

    // 5. Chooses the best entrance for a destination based on routing cost
    public interface IEntranceSelector
    {
        Destination SelectBestEntrance(QRAnchor startAnchor, List<Destination> entrances);
    }

    // 6. Optional service to switch floor scenes when user confirms a transition.
    // Implement this in your scene-loading layer (additive/manual/code-based).
    public interface IFloorSceneTransitionService
    {
        void RequestFloorTransition(int targetFloorId);
    }
}