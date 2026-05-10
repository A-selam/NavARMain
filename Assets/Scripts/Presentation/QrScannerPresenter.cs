using UnityEngine;
using UnityEngine.UIElements;
using System;
using NavAR.Core.Interfaces;
using NavAR.Core.State; 

namespace NavAR.Presentation
{
    public class QrScannerPresenter : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        
        private IQrScannerService qrScannerService;
        private AppStateManager appStateManager;
        private VisualElement rootContainer;

        public void Initialize(IQrScannerService scanner, AppStateManager stateManager)
        {
            this.qrScannerService = scanner;
            this.appStateManager = stateManager;

            rootContainer = uiDocument.rootVisualElement.Q<VisualElement>("qr-scan-screen-root"); 
            appStateManager.OnStateChanged += HandleStateChanged;
            rootContainer.style.display = DisplayStyle.None;
        }

        private void HandleStateChanged(AppState newState)
        {
            if (newState == AppState.QrScanning)
            {
                // 1. Show our UI Toolkit screen (ensure its background color in UI Builder is transparent!)
                rootContainer.style.display = DisplayStyle.Flex;
                
                // 2. Tell the core interface to start scanning
                qrScannerService.StartScanning(OnQrCodeFound);
            }
            else
            {
                rootContainer.style.display = DisplayStyle.None;
                qrScannerService.StopScanning();
            }
        }

        private void OnQrCodeFound(string qrPayload)
        {
            Debug.Log($"[QrScannerPresenter] QR Found: {qrPayload}");
            appStateManager.ChangeState(AppState.DestinationSelection);
        }

        private void OnDestroy()
        {
            if (appStateManager != null)
                appStateManager.OnStateChanged -= HandleStateChanged;
        }
    }
}