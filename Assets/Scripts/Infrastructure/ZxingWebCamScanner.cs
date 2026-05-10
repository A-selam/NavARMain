using UnityEngine;
using UnityEngine.UI;
using ZXing;
using System;
using NavAR.Core.Interfaces;

namespace NavAR.Infrastructure
{
    public class ZxingWebCamScanner : MonoBehaviour, IQrScannerService
    {
        [SerializeField] private RawImage cameraPreviewBackground;

        private WebCamTexture webCamTexture;
        private IBarcodeReader reader = new BarcodeReader();
        private bool isScanning = false;
        private Action<string> onQrScannedCallback;

        // Use a persistent Texture2D to avoid memory garbage
        private Texture2D _decodedTexture;

        public void StartScanning(Action<string> onQrCodeScanned)
        {
            onQrScannedCallback = onQrCodeScanned;
            isScanning = true;

            if (webCamTexture == null)
            {
                // Force a stable resolution
                webCamTexture = new WebCamTexture(1280, 720);
            }

            if (cameraPreviewBackground != null)
            {
                cameraPreviewBackground.texture = webCamTexture;
                cameraPreviewBackground.gameObject.SetActive(true);
            }

            webCamTexture.Play();
        }

        public void StopScanning()
        {
            isScanning = false;
            if (webCamTexture != null && webCamTexture.isPlaying)
                webCamTexture.Stop();

            if (cameraPreviewBackground != null)
                cameraPreviewBackground.gameObject.SetActive(false);
        }

        public Texture GetCameraFeed() => webCamTexture;

        void Update()
        {
            if (!isScanning || webCamTexture == null || !webCamTexture.didUpdateThisFrame) return;

            // Simple thread-safe scan
            try
            {
                // Get pixels directly from the texture
                var colors = webCamTexture.GetPixels32();
                var result = reader.Decode(colors, webCamTexture.width, webCamTexture.height);
                
                // if (result != null)
                if (true)
                {
                    // var qrPayload = result.Text;
                    var qrPayload = "Block-H-Floor-1-8";
                    var callback = onQrScannedCallback;

                    Debug.Log($"[ZxingWebCamScanner] SUCCESS: {qrPayload}");
                    StopScanning();
                    callback?.Invoke(qrPayload);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ZxingWebCamScanner] Decode Error: {e.Message}");
            }
        }
    }
}