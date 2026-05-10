using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using ZXing;
using NavAR.Core.Interfaces;

namespace NavAR.Infrastructure
{
    // Make sure this script is attached directly to the "Main Camera" object![RequireComponent(typeof(ARCameraManager))]
    public class ZxingQrScanner : MonoBehaviour, IQrScannerService
    {
        private ARCameraManager cameraManager; // No longer serialized
        
        private IBarcodeReader barcodeReader;
        private bool isScanning = false;
        private Action<string> onQrScannedCallback;
        
        private float scanInterval = 0.5f; 
        private float scanTimer = 0f;

        void Awake()
        {
            // AUTOMATICALLY grab the ARCameraManager on this camera! No drag-and-drop needed.
            cameraManager = GetComponent<ARCameraManager>();
        }

        void Start()
        {
            barcodeReader = new BarcodeReader
            {
                AutoRotate = true,
                Options = new ZXing.Common.DecodingOptions { TryHarder = false } 
            };
        }

        // --- Interface Methods ---
        public void StartScanning(Action<string> onQrCodeScanned)
        {
            onQrScannedCallback = onQrCodeScanned;
            isScanning = true;
            Debug.Log("[ZxingQrScanner] Camera scanning activated.");
        }

        public void StopScanning()
        {
            isScanning = false;
            onQrScannedCallback = null;
            Debug.Log("[ZxingQrScanner] Camera scanning deactivated.");
        }

        // --- Unity Lifecycle ---
        void Update()
        {
            if (!isScanning || cameraManager == null) return;

            scanTimer += Time.deltaTime;
            if (scanTimer < scanInterval) return;
            scanTimer = 0f;

            if (cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
            {
                DecodeImage(image);
            }
        }

        private void DecodeImage(XRCpuImage image)
        {
            try
            {
                var conversionParams = new XRCpuImage.ConversionParams
                {
                    inputRect = new RectInt(0, 0, image.width, image.height),
                    outputDimensions = new Vector2Int(image.width / 2, image.height / 2), 
                    outputFormat = TextureFormat.R8, 
                    transformation = XRCpuImage.Transformation.None
                };

                int size = image.GetConvertedDataSize(conversionParams);
                var buffer = new Unity.Collections.NativeArray<byte>(size, Unity.Collections.Allocator.Temp);
                image.Convert(conversionParams, buffer);

                var result = barcodeReader.Decode(buffer.ToArray(), conversionParams.outputDimensions.x, conversionParams.outputDimensions.y, RGBLuminanceSource.BitmapFormat.Gray8);

                if (result != null && !string.IsNullOrEmpty(result.Text))
                {
                    StopScanning(); 
                    Debug.Log($"[ZxingQrScanner] SUCCESS! Scanned QR Code: {result.Text}");
                    onQrScannedCallback?.Invoke(result.Text);
                }

                buffer.Dispose();
            }
            finally
            {
                image.Dispose(); 
            }
        }
    }
}