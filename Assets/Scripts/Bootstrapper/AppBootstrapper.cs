// using UnityEngine;
// using NavAR.Core.State;
// using NavAR.Infrastructure;
// using NavAR.Presentation;

// namespace NavAR.Bootstrapper
// {
//     // The Bootstrapper is the ONLY script that knows about all the layers.
//     // It creates the pure C# classes and wires them into the Unity scripts.
//     public class AppBootstrapper : MonoBehaviour
//     {
//         [Header("Presentation Layer")]
//         [SerializeField] private UIManager uiManager;
//         [Header("Infrastructure Layer")]
//         [SerializeField] private ZxingWebCamScanner webCamScanner;

//         // Core Layer Brain
//         private AppStateManager appStateManager;

//         void Start()
//         {
//             Debug.Log("[Bootstrapper] Booting up NavAR System...");

//             // 1. Create the pure C# Brain (Core Layer)
//             appStateManager = new AppStateManager();

//             // 2. Wire the Infrastructure (Camera) and Core (Brain) into the Presentation (UI)
//             uiManager.Initialize(appStateManager, webCamScanner);
            
//             Debug.Log("[Bootstrapper] Initialization Complete.");
//         }
//     }
// }

using UnityEngine;
using NavAR.Core.State;
using NavAR.Infrastructure;
using NavAR.Presentation;
using NavAR.Data; // Add this!
using NavAR.Core.Interfaces; // Add this!
using UnityEngine.SceneManagement;
using NavAR.Data.SQLite;
using NavAR.Infrastructure.Navigation;
using System;

namespace NavAR.Bootstrapper
{
    public class AppBootstrapper : MonoBehaviour
    {
        [Header("Presentation Layer")]
        [SerializeField] private UIManager uiManager;

        [Header("Infrastructure Layer")]
        [SerializeField] private ZxingWebCamScanner webCamScanner;

        [Header("Data Layer")]
        [SerializeField] private bool useSQLiteRepository = true;

        private AppStateManager appStateManager;
        private IMapRepository mapRepository;
        private IFloorSceneTransitionService floorTransitionService;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void Start()
        {
            Debug.Log("[Bootstrapper] Booting up NavAR System...");

            // 1. Create the Brain
            appStateManager = new AppStateManager();
            
            // 2. Create the Data Layer (Safe to do here because Start() runs after Unity is fully awake)
            mapRepository = BuildRepository();

            // 3. Try to bind the current scene immediately, then again whenever a new scene loads
            TryBindPresentationLayer();

            Debug.Log("[Bootstrapper] Initialization Complete.");
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryBindPresentationLayer();
        }

        private IMapRepository BuildRepository()
        {
            if (!useSQLiteRepository)
            {
                Debug.Log("[Bootstrapper] Using MockMapRepository (SQLite disabled).");
                return new MockMapRepository();
            }

            try
            {
                Debug.Log("[Bootstrapper] Using SQLiteMapRepository.");
                return new SQLiteMapRepository();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Bootstrapper] SQLite initialization failed. Falling back to MockMapRepository. Error: {ex.Message}");
                return new MockMapRepository();
            }
        }

        private void TryBindPresentationLayer()
        {
            if (appStateManager == null || mapRepository == null)
            {
                return;
            }

            // Create FloorSceneTransitionService if not already created
            if (floorTransitionService == null)
            {
                var service = FindObjectOfType<FloorSceneTransitionService>();
                if (service == null)
                {
                    var serviceGO = new GameObject("FloorSceneTransitionService");
                    service = serviceGO.AddComponent<FloorSceneTransitionService>();
                    DontDestroyOnLoad(serviceGO);
                }
                floorTransitionService = service;
            }

            if (uiManager == null)
            {
                uiManager = FindObjectOfType<UIManager>();
            }

            if (webCamScanner == null)
            {
                webCamScanner = FindObjectOfType<ZxingWebCamScanner>();
            }

            if (uiManager == null || webCamScanner == null)
            {
                Debug.Log("[Bootstrapper] Waiting for UIManager and/or scanner to become available in the active scene.");
                return;
            }

            uiManager.Initialize(appStateManager, webCamScanner, mapRepository, floorTransitionService);

            // Attempt to auto-wire AlignmentService runtime references (ARSession + XR Origin)
            var arSession = FindObjectOfType<UnityEngine.XR.ARFoundation.ARSession>();
            UnityEngine.Transform xrOriginTransform = null;
            var originComp = FindObjectOfType<UnityEngine.XR.ARFoundation.ARSessionOrigin>();
            if (originComp != null) xrOriginTransform = originComp.transform;
            else
            {
                var go = GameObject.Find("XR Origin");
                if (go != null) xrOriginTransform = go.transform;
            }

            var alignServices = FindObjectsOfType<AlignmentService>();
            foreach (var a in alignServices)
            {
                if (arSession != null) a.SetSession(arSession);
                if (xrOriginTransform != null) a.SetXROrigin(xrOriginTransform);
            }
        }
    }
}