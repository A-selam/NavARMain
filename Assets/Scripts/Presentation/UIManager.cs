using UnityEngine;
using UnityEngine.UIElements;
using NavAR.Core.State; // This is the state folder from Stage 3
using NavAR.Core.Interfaces; // For IQrScannerService
using NavAR.Presentation.Controllers;
using NavAR.Infrastructure; // For AlignmentService
using NavAR.Core.Entities; // For QRAnchor
using NavAR.Data; // For MockMapRepository
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NavAR.Infrastructure.Navigation;
using UnityEngine.SceneManagement;
using NavAR.Core.Navigation; // For graph routing
#if !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace NavAR.Presentation
{
    [RequireComponent(typeof(UIDocument))]
    public class UIManager : MonoBehaviour
    {
        private bool _initialized = false;
        private class DestinationGroup
        {
            public DestinationGroup(string groupId, string displayName, string category, int floorId)
            {
                GroupId = groupId;
                DisplayName = displayName;
                Category = category;
                FloorId = floorId;
                Entrances = new List<Destination>();
            }

            public string GroupId { get; }
            public string DisplayName { get; }
            public string Category { get; }
            public int FloorId { get; }
            public List<Destination> Entrances { get; }
        }
        [Header("Screen Assets")]
        [SerializeField] private VisualTreeAsset splashScreenAsset;
        [SerializeField] private VisualTreeAsset homeScreenAsset;
        [SerializeField] private VisualTreeAsset destinationScreenAsset;
        [SerializeField] private VisualTreeAsset settingsScreenAsset;
        [SerializeField] private VisualTreeAsset permissionScreenAsset;
        [SerializeField] private VisualTreeAsset qrScannerAsset;
        [SerializeField] private VisualTreeAsset arNavigationAsset;
        [SerializeField] private VisualTreeAsset positionLostAsset;
        [SerializeField] private VisualTreeAsset feedbackScreenAsset;
        [SerializeField] private VisualTreeAsset destinationItemAsset;
        [Header("Navigation Scene Context")]
        [SerializeField] private NavigationSceneContext navigationSceneContext;

        [Header("Diagnostics")]
        [SerializeField] private bool enableUiDiagnostics = true;

        [Header("Transition Detection")]
        [SerializeField] private float transitionArrivalRadiusMeters = 1.5f;

        private VisualElement _contentContainer;
        private VisualElement _root;
        private NavigationBarController _navigationBarController;
        private AppState _lastNonOverlayState = AppState.Home;
        private AppStateManager _stateManager;
        private IQrScannerService _qrScannerService;
        private IMapRepository _mapRepository; // Changed to interface type
        private AlignmentService _alignmentService;
        private IPathCalculator _pathCalculator;
        private HybridGraphPathCalculator _hybridCalculator;
        private IArRenderer _pathRenderer;
        private IFloorSceneTransitionService _floorTransitionService;
        private IEntranceSelector _entranceSelector;
        private Coroutine _transitionArrivalWatchRoutine;
        private Vector3 _pendingTransitionLandingPosition;
        private bool _hasPendingTransitionLanding;

        public void Initialize(AppStateManager stateManager, IQrScannerService qrScannerService, IMapRepository mapRepository, IFloorSceneTransitionService floorTransitionService = null)
        {
            // Allow safe re-binding without resetting UI state or resubscribing.
            var firstInit = !_initialized;

            if (firstInit)
            {
                _stateManager = stateManager;
                _qrScannerService = qrScannerService; // Save the reference
                _mapRepository = mapRepository; // Save the repository we were handed
                _floorTransition_service: ;
            }

            // Always update references passed by the bootstrapper
            _stateManager = stateManager ?? _stateManager;
            _qrScanner_service: ;
            _qrScannerService = qrScannerService ?? _qrScannerService;
            _mapRepository = mapRepository ?? _mapRepository;
            _floorTransitionService = floorTransitionService ?? _floorTransitionService;

            // Resolve navigation services from the current active scene(s)
            ResolveNavigationDependencies();

            // Wire UI document and controls only once
            if (firstInit)
            {
                _root = GetComponent<UIDocument>().rootVisualElement;
                _contentContainer = _root.Q<VisualElement>("ContentContainer");

                if (_contentContainer == null)
                {
                    Debug.LogError("UIManager: ContentContainer was not found.");
                    return;
                }

                ValidateScreenAssetAssignments();

                _navigationBarController = new NavigationBarController(_root, SetState);
                _navigationBarController.Wire();

                _stateManager.OnStateChanged += HandleStateChange;

                // Set initial screen
                _stateManager.SetState(AppState.Home);

                _initialized = true;
            }
        }

        private void ResolveNavigationDependencies()
        {
            if (navigationSceneContext == null)
            {
                navigationSceneContext = FindObjectOfType<NavigationSceneContext>();
            }

            if (navigationSceneContext == null)
            {
                var contextObject = GameObject.Find("[NAVIGATION_CONTEXT]");
                if (contextObject != null)
                {
                    navigationSceneContext = contextObject.GetComponent<NavigationSceneContext>();
                }
            }

            if (enableUiDiagnostics)
            {
                var contextName = navigationSceneContext != null ? navigationSceneContext.gameObject.name : "<none>";
                var contextScene = navigationSceneContext != null ? navigationSceneContext.gameObject.scene.name : "<none>";
                var contextActive = navigationSceneContext != null && navigationSceneContext.gameObject.activeInHierarchy;
                Debug.Log($"UIManager: NavigationSceneContext resolved -> {contextName} (scene={contextScene}, active={contextActive})");
            }

            if (navigationSceneContext != null && navigationSceneContext.TryResolve(out var sceneAlignmentService, out var scenePathCalculator, out var scenePathRenderer))
            {
                // Validate that resolved services are not null (destroyed components will be null after TryResolve)
                if (sceneAlignmentService != null && scenePathCalculator != null && scenePathRenderer != null)
                {
                    _alignmentService = sceneAlignmentService;
                    _pathCalculator = scenePathCalculator;
                    _pathRenderer = scenePathRenderer;

                    if (_entranceSelector == null && _pathCalculator != null)
                    {
                        _entranceSelector = new NavMeshEntranceSelector(_pathCalculator);
                    }

                    CreateHybridCalculator();

                    if (enableUiDiagnostics)
                    {
                        Debug.Log("UIManager: Navigation services resolved from NavigationSceneContext.");
                    }
                    return;
                }
                else
                {
                    if (enableUiDiagnostics)
                    {
                        Debug.LogWarning($"UIManager: NavigationSceneContext services are null/destroyed. Alignment={sceneAlignmentService != null}, PathCalc={scenePathCalculator != null}, Renderer={scenePathRenderer != null}. Falling back to FindObjectOfType.");
                    }
                }
            }

            if (navigationSceneContext != null)
            {
                var hasAlign = navigationSceneContext.AlignmentService != null;
                var hasCalc = navigationSceneContext.PathCalculator != null;
                var hasRenderer = navigationSceneContext.PathRenderer != null;
                Debug.LogWarning($"UIManager: NavigationSceneContext missing services. Alignment={hasAlign}, PathCalculator={hasCalc}, PathRenderer={hasRenderer}.");
            }

            if (_alignmentService == null)
            {
                _alignmentService = FindObjectOfType<AlignmentService>();
            }

            if (_pathCalculator == null)
            {
                _pathCalculator = FindObjectOfType<NavMeshPathCalculator>();
            }

            if (_pathRenderer == null)
            {
                _pathRenderer = FindObjectOfType<ArPathRenderer>();
            }

            if (_alignmentService == null || _pathCalculator == null || _pathRenderer == null)
            {
                Debug.LogWarning("UIManager: Navigation services were not fully resolved. The UI can still load, but navigation will fail until the scene provides AlignmentService, NavMeshPathCalculator, and ArPathRenderer.");
            }

            if (_entranceSelector == null && _pathCalculator != null)
            {
                _entranceSelector = new NavMeshEntranceSelector(_pathCalculator);
            }

            CreateHybridCalculator();

            if (enableUiDiagnostics)
            {
                Debug.Log($"UIManager: Services -> Alignment={(_alignmentService != null)}, PathCalc={(_pathCalculator != null)}, Renderer={(_pathRenderer != null)}, EntranceSelector={(_entranceSelector != null)}, HybridCalc={(_hybridCalculator != null)}");
            }
        }

        private void CreateHybridCalculator()
        {
            if (_hybridCalculator != null)
            {
                return; // Already created
            }

            if (_pathCalculator == null || _mapRepository == null)
            {
                if (enableUiDiagnostics)
                {
                    Debug.Log("UIManager: Cannot create hybrid calculator without pathCalculator and mapRepository.");
                }
                return;
            }

            try
            {
                var graphRouter = new DijkstraGraphRouter(_mapRepository, enableUiDiagnostics);
                _hybridCalculator = new HybridGraphPathCalculator(
                    _pathCalculator,
                    graphRouter,
                    (targetFloor, label, nodeId) => BeginFloorTransition(targetFloor, label, nodeId),
                    enableUiDiagnostics
                );

                if (enableUiDiagnostics)
                {
                    Debug.Log("UIManager: Hybrid calculator created successfully.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"UIManager: Failed to create hybrid calculator: {ex}");
            }
        }

        private bool EnsureNavigationServices()
        {
            if (_pathCalculator == null || _pathRenderer == null || _entranceSelector == null)
            {
                ResolveNavigationDependencies();
            }

            var ready = _pathCalculator != null && _pathRenderer != null && _entranceSelector != null;
            if (enableUiDiagnostics)
            {
                Debug.Log($"UIManager: EnsureNavigationServices -> {ready}. PathCalculator={_pathCalculator != null}, PathRenderer={_pathRenderer != null}, EntranceSelector={_entranceSelector != null}");
            }
            return ready;
        }

        private void OnDestroy()
        {
            StopTransitionArrivalWatch();

            if (_stateManager != null)
            {
                _stateManager.OnStateChanged -= HandleStateChange;
            }
        }

        private void HandleStateChange(AppState newState)
        {
            if (enableUiDiagnostics)
            {
                Debug.Log($"UIManager: Entering state '{newState}'.");
            }

            _contentContainer.Clear();
            _navigationBarController?.UpdateActive(newState);

            if (!IsOverlayState(newState))
            {
                _lastNonOverlayState = newState;
            }

            if (newState != AppState.QrScanning && _qrScannerService != null)
            {
                _qrScannerService.StopScanning();
            }

            if (newState != AppState.Navigating)
            {
                StopTransitionArrivalWatch();
            }

            switch (newState)
            {
                case AppState.Splash:
                    ShowScreen(splashScreenAsset);
                    break;

                case AppState.Home:
                    ShowScreen(homeScreenAsset);
                    ScreenBinders.WireHome(_contentContainer, SetState);
                    break;

                case AppState.Explore | AppState.DestinationSelection:
                    ShowScreen(destinationScreenAsset);
                    PopulateDestinationList();
                    ScreenBinders.WireExplore(_contentContainer, SetState);
                    break;

                case AppState.QrScanning:
                    if (HasCameraPermission())
                    {
                        ShowScreen(qrScannerAsset);
                        ScreenBinders.WireQrScanner(_contentContainer, SetState);

                        // START THE CAMERA AND WAIT FOR THE RESULT!
                        _qrScannerService?.StartScanning(OnQrCodeFound);
                    }
                    else
                    {
                        SetState(AppState.Permission);
                    }
                    break;

                case AppState.Permission:
                    ShowScreen(permissionScreenAsset);
                    ScreenBinders.WirePermission(_contentContainer, SetState, RequestCameraPermission);
                    break;

                case AppState.Navigating:
                    ShowScreen(arNavigationAsset);
                    ScreenBinders.WireArNavigation(
                        _contentContainer,
                        SetState,
                        () => _lastNonOverlayState,
                        OnToggleVoiceGuidance,
                        OnOpenFloorMap
                    );
                    // At this point, the UI is transparent, and the AR Camera feed 
                    // will be visible behind it!
                    break;

                case AppState.FloorTransition:
                    ShowFloorTransitionScreen();
                    break;

                case AppState.PositionLost:
                    ShowScreen(positionLostAsset);
                    ScreenBinders.WirePositionLost(_contentContainer, SetState);
                    break;

                case AppState.Settings:
                    ShowScreen(settingsScreenAsset);
                    ScreenBinders.WireSettings(_contentContainer, SetState, OnSignOutRequested, OnAboutRequested);
                    break;

                case AppState.Feedback:
                    ShowScreen(feedbackScreenAsset);
                    ScreenBinders.WireFeedback(_contentContainer, SetState, () => _lastNonOverlayState, OnSubmitFeedback);
                    break;

                default:
                    ShowScreen(homeScreenAsset);
                    ScreenBinders.WireHome(_contentContainer, SetState);
                    break;
            }
        }

        private void ShowScreen(VisualTreeAsset asset)
        {
            if (asset == null)
            {
                Debug.LogError("UIManager: Attempted to show a screen with a null VisualTreeAsset. Check inspector assignments.");
                return;
            }

            var instance = asset.Instantiate();
            if (instance == null)
            {
                Debug.LogError($"UIManager: Failed to instantiate VisualTreeAsset '{asset.name}'.");
                return;
            }

            instance.style.flexGrow = 1; // Make it fill the screen
            _contentContainer.Add(instance);

            if (enableUiDiagnostics)
            {
                Debug.Log($"UIManager: Rendered screen asset '{asset.name}'.");
            }
        }

        private void ShowFloorTransitionScreen()
        {
            var promptFloorId = _stateManager?.Context?.PendingFloorId ?? 0;
            var promptLabel = _stateManager?.Context?.PendingFloorLabel;
            var transitionNode = _stateManager?.Context?.PendingTransitionNodeId;

            var overlay = new VisualElement
            {
                name = "FloorTransitionOverlay"
            };

            overlay.style.flexGrow = 1;
            overlay.style.justifyContent = Justify.Center;
            overlay.style.alignItems = Align.Center;
            overlay.style.paddingLeft = 24;
            overlay.style.paddingRight = 24;
            overlay.style.paddingTop = 24;
            overlay.style.paddingBottom = 24;

            var panel = new VisualElement();
            panel.style.maxWidth = 520;
            panel.style.width = Length.Percent(100);
            panel.style.paddingLeft = 24;
            panel.style.paddingRight = 24;
            panel.style.paddingTop = 24;
            panel.style.paddingBottom = 24;
            panel.style.borderTopLeftRadius = 16;
            panel.style.borderTopRightRadius = 16;
            panel.style.borderBottomLeftRadius = 16;
            panel.style.borderBottomRightRadius = 16;
            panel.style.backgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.92f);

            var title = new Label("Floor Transition");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 28;
            title.style.color = Color.white;
            title.style.marginBottom = 12;

            var messageText = string.IsNullOrWhiteSpace(promptLabel)
                ? $"Click the button when you are at floor {promptFloorId}."
                : $"Click the button when you are at {promptLabel}.";

            if (!string.IsNullOrWhiteSpace(transitionNode))
            {
                messageText += $"\nTransition node: {transitionNode}";
            }

            var message = new Label(messageText);
            message.style.whiteSpace = WhiteSpace.Normal;
            message.style.color = Color.white;
            message.style.fontSize = 18;
            message.style.marginBottom = 16;

            var confirmButton = new Button(() => ConfirmFloorTransition())
            {
                text = $"I am at floor {promptFloorId}" 
            };
            confirmButton.style.marginTop = 12;
            confirmButton.style.paddingTop = 12;
            confirmButton.style.paddingBottom = 12;
            confirmButton.style.paddingLeft = 16;
            confirmButton.style.paddingRight = 16;

            panel.Add(title);
            panel.Add(message);
            panel.Add(confirmButton);
            overlay.Add(panel);
            _contentContainer.Add(overlay);

            if (enableUiDiagnostics)
            {
                Debug.Log($"UIManager: Showing floor transition prompt for floor {promptFloorId}.");
            }
        }

        private void PopulateDestinationList()
        {
            var listContainer = _contentContainer.Q<ScrollView>("DestinationListContainer");
            if (listContainer == null) return;

            listContainer.Clear();
            var destinations = _mapRepository.GetAllDestinations();
            var groups = BuildDestinationGroups(destinations);

            foreach (var group in groups)
            {
                // CRITICAL: We must store the data in a local variable inside the loop, 
                // otherwise every button will think it is the LAST item in the list!
                DestinationGroup localGroup = group;

                var itemInstance = destinationItemAsset.Instantiate();

                var nameLabel = itemInstance.Q<Label>("DestinationNameLabel");
                var descLabel = itemInstance.Q<Label>("DestinationDescLabel");

                if (nameLabel != null) nameLabel.text = localGroup.DisplayName;
                if (descLabel != null) descLabel.text = $"{localGroup.Category} - Floor {localGroup.FloorId}";

                // Force the item to catch mouse/touch interactions
                itemInstance.pickingMode = PickingMode.Position;

                // In destination selection, allow user to select ANY destination
                // Floor will be loaded after QR scan. Don't check floor load state here.
                itemInstance.RegisterCallback<PointerUpEvent>(evt =>
                {
                    Debug.Log($"[UI] Clicked on: {localGroup.DisplayName}");
                    OnDestinationGroupSelected(localGroup);
                });

                listContainer.Add(itemInstance);
            }
        }

        private List<DestinationGroup> BuildDestinationGroups(List<Destination> destinations)
        {
            var groups = new Dictionary<string, DestinationGroup>(StringComparer.OrdinalIgnoreCase);

            foreach (var destination in destinations)
            {
                var baseKey = NormalizeDestinationBase(destination.destination_id);
                if (string.IsNullOrWhiteSpace(baseKey))
                {
                    baseKey = NormalizeDestinationBase(destination.name);
                }

                if (string.IsNullOrWhiteSpace(baseKey))
                {
                    baseKey = destination.destination_id ?? destination.name ?? "Unknown";
                }

                if (!groups.TryGetValue(baseKey, out var group))
                {
                    var displayName = baseKey.Trim();
                    group = new DestinationGroup(baseKey, displayName, destination.category, destination.floor_id);
                    groups.Add(baseKey, group);
                }

                group.Entrances.Add(destination);
            }

            return groups.Values
                .OrderBy(g => g.DisplayName)
                .ToList();
        }

        private bool IsFloorLoaded(int floorId)
        {
            if (floorId < 0) return false;

            // Convention: floor scenes are named 'Floor_<id>' (e.g. Floor_1)
            var sceneName = $"Floor_{floorId}";
            var scene = SceneManager.GetSceneByName(sceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                return true;
            }

            // Fall back: check for any active NavigationSceneContext in a loaded scene
            var contexts = FindObjectsOfType<NavigationSceneContext>();
            foreach (var ctx in contexts)
            {
                if (ctx != null && ctx.gameObject.scene.IsValid() && ctx.gameObject.scene.isLoaded)
                {
                    // Heuristic: assume the scene name contains the floor id when named following convention
                    if (ctx.gameObject.scene.name.Contains($"{floorId}") || ctx.gameObject.scene.name.Contains($"Floor_{floorId}"))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string NormalizeDestinationBase(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var trimmed = input.Trim();
            return Regex.Replace(trimmed, "-door-\\d+$", string.Empty, RegexOptions.IgnoreCase).Trim();
        }

        private bool HasCameraPermission()
        {
            // If we are running in the Unity Editor, we just pretend we have permission 
            // so we don't get stuck during testing.
            #if UNITY_EDITOR
            return true;
            #else
            // This is the actual check for Android/iOS devices
            return Permission.HasUserAuthorizedPermission(Permission.Camera);
            #endif
        }

        private void ValidateScreenAssetAssignments()
        {
            ValidateAsset(splashScreenAsset, nameof(splashScreenAsset));
            ValidateAsset(homeScreenAsset, nameof(homeScreenAsset));
            ValidateAsset(destinationScreenAsset, nameof(destinationScreenAsset));
            ValidateAsset(settingsScreenAsset, nameof(settingsScreenAsset));
            ValidateAsset(permissionScreenAsset, nameof(permissionScreenAsset));
            ValidateAsset(qrScannerAsset, nameof(qrScannerAsset));
            ValidateAsset(arNavigationAsset, nameof(arNavigationAsset));
            ValidateAsset(positionLostAsset, nameof(positionLostAsset));
            ValidateAsset(feedbackScreenAsset, nameof(feedbackScreenAsset));
            ValidateAsset(destinationItemAsset, nameof(destinationItemAsset));
        }

        private void ValidateAsset(VisualTreeAsset asset, string fieldName)
        {
            if (asset == null)
            {
                Debug.LogWarning($"UIManager: '{fieldName}' is not assigned in the inspector.");
                return;
            }

            if (enableUiDiagnostics)
            {
                Debug.Log($"UIManager: '{fieldName}' assigned to '{asset.name}'.");
            }
        }

        private static bool IsOverlayState(AppState state)
        {
            return state == AppState.Permission
                || state == AppState.QrScanning
                || state == AppState.FloorTransition
                || state == AppState.PositionLost
                || state == AppState.Feedback;
        }

        private void SetState(AppState state)
        {
            if (enableUiDiagnostics)
            {
                Debug.Log($"UIManager: SetState requested -> '{_stateManager.CurrentState}' to '{state}'.");
            }
            _stateManager.SetState(state);
        }

        private void RequestCameraPermission()
        {
            Debug.Log("User clicked Allow Camera.");

            // Request the physical permission from the OS
            #if !UNITY_EDITOR
            Permission.RequestUserPermission(Permission.Camera);
            #endif
        }

        private void OnToggleVoiceGuidance()
        {
            Debug.Log("Voice guidance toggle action placeholder.");
        }

        private void OnOpenFloorMap()
        {
            Debug.Log("Open floor map action placeholder.");
        }

        public void BeginFloorTransition(int targetFloorId, string targetFloorLabel = null, string transitionNodeId = null)
        {
            if (_stateManager == null)
            {
                Debug.LogError("UIManager: Cannot begin floor transition because the state manager is not initialized.");
                return;
            }

            _stateManager.Context.PendingFloorId = targetFloorId;
            _stateManager.Context.PendingFloorLabel = targetFloorLabel;
            _stateManager.Context.PendingTransitionNodeId = transitionNodeId;
            _stateManager.ChangeState(AppState.FloorTransition);
        }

        private void ConfirmFloorTransition()
        {
            if (_stateManager == null)
            {
                return;
            }

            var destination = _stateManager.Context.CurrentDestination;
            var targetFloorId = _stateManager.Context.PendingFloorId;

            if (targetFloorId > 0)
            {
                _stateManager.Context.CurrentFloorId = targetFloorId;
            }

            _stateManager.Context.PendingFloorId = 0;
            _stateManager.Context.PendingFloorLabel = null;
            _stateManager.Context.PendingTransitionNodeId = null;

            _stateManager.ChangeState(AppState.Navigating);

            if (destination != null)
            {
                // Request floor transition (load target floor, unload others)
                // Then wait for it to complete before resuming navigation
                if (_floorTransitionService != null && targetFloorId > 0)
                {
                    Debug.Log($"[UIManager] Requesting floor transition to floor {targetFloorId}...");
                    _floorTransitionService.RequestFloorTransition(targetFloorId);
                }

                StartCoroutine(ResumeNavigationAfterFloorTransition(destination, targetFloorId));
            }
        }

        private IEnumerator ResumeNavigationAfterFloorTransition(Destination destination, int targetFloorId)
        {
            const int maxAttempts = 60; // Increased to allow floor loading time

            // Clear ALL navigation service references since old floor scene is being unloaded
            _pathRenderer = null;
            _pathCalculator = null;
            _alignmentService = null;
            _entranceSelector = null;
            navigationSceneContext = null; // Force re-discovery from new floor scene

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // Wait for floor transition service to complete (if it exists)
                if (_floorTransitionService != null && targetFloorId > 0)
                {
                    // Check if transition is still in progress using the interface method
                    var floorSceneTransitionService = _floorTransitionService as FloorSceneTransitionService;
                    if (floorSceneTransitionService != null && floorSceneTransitionService.IsTransitionInProgress)
                    {
                        if (enableUiDiagnostics)
                        {
                            Debug.Log($"[UIManager] Waiting for floor {targetFloorId} to load... (attempt {attempt + 1}/{maxAttempts})");
                        }
                        yield return null;
                        continue;
                    }
                }

                if (EnsureNavigationServices())
                {
                    // Check that the NavigationSceneContext for the target floor is actually ready
                    var navContext = FindObjectOfType<NavigationSceneContext>();
                    if (navContext == null || navContext.gameObject.scene.name != $"Floor_{targetFloorId}" && targetFloorId > 0)
                    {
                        if (enableUiDiagnostics)
                        {
                            Debug.Log($"[UIManager] Waiting for NavigationSceneContext in floor {targetFloorId} to be ready... (attempt {attempt + 1}/{maxAttempts})");
                        }
                        yield return null;
                        continue;
                    }

                            if (_hasPendingTransitionLanding)
                            {
                                SnapXrOriginToPendingTransitionLanding();
                            }

                    var currentCamera = Camera.main != null ? Camera.main.transform : null;
                    var startPos = currentCamera != null
                        ? currentCamera.position
                        : new Vector3(destination.target_x, destination.target_y, destination.target_z);
                    var targetPos = new Vector3(destination.target_x, destination.target_y, destination.target_z);

                    var continuationPath = CalculatePathForCurrentFloor(startPos, targetPos, _stateManager.Context.CurrentFloorId, destination.floor_id);
                    if (continuationPath != null && continuationPath.Count > 0)
                    {
                        StartCoroutine(WaitForAndDrawPath(continuationPath));
                    }
                    else
                    {
                        Debug.LogWarning("[UIManager] Could not calculate continuation path after floor transition confirmation.");
                    }

                    yield break;
                }

                yield return null;
            }

            Debug.LogError("[UIManager] Floor transition continuation failed: navigation services were not ready after transition.");
        }

        private void SnapXrOriginToPendingTransitionLanding()
        {
            if (!_hasPendingTransitionLanding)
            {
                return;
            }

            // Try common XR origin names first and log each step for diagnostics
            Transform xrOrigin = null;

            var foundByName = GameObject.Find("XROrigin");
            if (foundByName != null)
            {
                xrOrigin = foundByName.transform;
                if (enableUiDiagnostics)
                {
                    Debug.Log($"[UIManager] Found XR Origin by name: XROrigin (scene={foundByName.scene.name}, id={foundByName.GetInstanceID()})");
                }
            }

            if (xrOrigin == null)
            {
                var arSessionOrigin = FindObjectOfType<UnityEngine.XR.ARFoundation.ARSessionOrigin>();
                if (arSessionOrigin != null)
                {
                    xrOrigin = arSessionOrigin.transform;
                    if (enableUiDiagnostics)
                    {
                        Debug.Log($"[UIManager] Found ARSessionOrigin instance (scene={arSessionOrigin.gameObject.scene.name}, id={arSessionOrigin.gameObject.GetInstanceID()})");
                    }
                }
            }

            if (xrOrigin == null)
            {
                // Last resort: search for any object named similarly
                var possible = GameObject.FindObjectsOfType<Transform>().FirstOrDefault(t => t.name.IndexOf("xr", StringComparison.OrdinalIgnoreCase) >= 0);
                if (possible != null)
                {
                    xrOrigin = possible;
                    if (enableUiDiagnostics)
                    {
                        Debug.Log($"[UIManager] Found XR-like transform by heuristic: {possible.name} (scene={possible.gameObject.scene.name}, id={possible.gameObject.GetInstanceID()})");
                    }
                }
            }

            if (xrOrigin == null)
            {
                Debug.LogWarning("[UIManager] Could not locate XR Origin for transition landing snap. Checked 'XROrigin', 'ARSessionOrigin' and heuristics.");
                return;
            }

            xrOrigin.position = _pendingTransitionLandingPosition;
            _hasPendingTransitionLanding = false;

            if (enableUiDiagnostics)
            {
                Debug.Log($"UIManager: Snapped XR Origin to transition landing {_pendingTransitionLandingPosition}.");
            }
        }

        private List<Vector3> CalculatePathForCurrentFloor(Vector3 startPos, Vector3 targetPos, int floorId, int? destinationFloorId = null)
        {
            if (_hybridCalculator != null)
            {
                return _hybridCalculator.CalculatePathWithContext(startPos, targetPos, floorId, destinationFloorId);
            }

            if (_pathCalculator != null)
            {
                return _pathCalculator.CalculatePath(startPos, targetPos);
            }

            return null;
        }

        /// <summary>
        /// Safely draws a path, handling cases where the renderer may have been destroyed.
        /// </summary>
        private void SafeDrawPath(List<Vector3> pathCorners)
        {
            if (pathCorners == null || pathCorners.Count == 0)
            {
                return;
            }

            // First quick null check
            if (_pathRenderer == null)
            {
                if (enableUiDiagnostics)
                {
                    Debug.LogWarning("[UIManager] Path renderer is null, attempting to re-resolve before giving up.");
                }

                // Try to find a fresh renderer instance in the loaded scenes
                var fresh = FindObjectOfType<ArPathRenderer>();
                if (fresh != null)
                {
                    _pathRenderer = fresh as IArRenderer;
                    if (enableUiDiagnostics)
                    {
                        Debug.Log($"[UIManager] Re-resolved path renderer: instanceId={fresh.GetInstanceID()}, scene={fresh.gameObject.scene.name}");
                    }
                }
                else
                {
                    if (enableUiDiagnostics)
                    {
                        Debug.LogWarning("[UIManager] Could not re-resolve ArPathRenderer in current scenes.");
                    }
                    return;
                }
            }

            try
            {
                _pathRenderer.DrawPath(pathCorners);
            }
            catch (System.MissingMemberException mme)
            {
                Debug.LogError($"[UIManager] MissingMemberException drawing path: {mme.Message}. Clearing reference and aborting draw.");
                _pathRenderer = null;
            }
            catch (UnityEngine.MissingReferenceException mre)
            {
                Debug.LogError($"[UIManager] MissingReferenceException drawing path: {mre.Message}. Renderer destroyed. Clearing reference and attempting re-resolve.");
                _pathRenderer = null;

                // Attempt one more re-resolve and retry once
                var retry = FindObjectOfType<ArPathRenderer>();
                if (retry != null)
                {
                    _pathRenderer = retry as IArRenderer;
                    if (enableUiDiagnostics)
                    {
                        Debug.Log($"[UIManager] Re-resolved path renderer on retry: instanceId={retry.GetInstanceID()}, scene={retry.gameObject.scene.name}");
                    }
                    try
                    {
                        _pathRenderer.DrawPath(pathCorners);
                    }
                    catch (System.Exception ex2)
                    {
                        Debug.LogError($"[UIManager] Retry draw failed: {ex2.Message}. Giving up and clearing reference.");
                        _pathRenderer = null;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[UIManager] Error drawing path: {ex.Message}. Renderer may have been destroyed. Clearing reference.");
                _pathRenderer = null;
            }
        }

        /// <summary>
        /// Waits for an `ArPathRenderer` to be available (up to a timeout) and draws the path.
        /// Retries across frames instead of throwing immediately so transient scene unloads
        /// won't crash or cause state fallback.
        /// </summary>
        private IEnumerator WaitForAndDrawPath(List<Vector3> pathCorners, bool setNavigatingOnSuccess = false, int maxAttempts = 60)
        {
            if (pathCorners == null || pathCorners.Count == 0)
            {
                yield break;
            }

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // Ensure we have a renderer reference
                if (_pathRenderer == null)
                {
                    var fresh = FindObjectOfType<ArPathRenderer>();
                    if (fresh != null)
                    {
                        _pathRenderer = fresh as IArRenderer;
                        if (enableUiDiagnostics)
                        {
                            Debug.Log($"[UIManager] WaitForAndDrawPath: re-resolved renderer instanceId={fresh.GetInstanceID()}, scene={fresh.gameObject.scene.name}");
                        }
                    }
                }

                    if (_pathRenderer != null)
                {
                    try
                    {
                        _pathRenderer.DrawPath(pathCorners);
                            if (setNavigatingOnSuccess && _stateManager != null)
                            {
                                _stateManager.ChangeState(AppState.Navigating);
                                StartTransitionArrivalWatchIfNeeded();
                            }
                        yield break; // success
                    }
                    catch (UnityEngine.MissingReferenceException mre)
                    {
                        if (enableUiDiagnostics)
                        {
                            Debug.LogWarning($"[UIManager] WaitForAndDrawPath attempt {attempt + 1}: renderer destroyed while drawing: {mre.Message}");
                        }
                        _pathRenderer = null; // clear stale ref and retry
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[UIManager] WaitForAndDrawPath unexpected error: {ex.Message}");
                        _pathRenderer = null;
                        yield break; // don't loop on unknown errors
                    }
                }

                // Wait a frame and retry
                yield return null;
            }

            Debug.LogWarning("[UIManager] WaitForAndDrawPath: failed to draw path after retries.");
        }

        private void OnSubmitFeedback()
        {
            Debug.Log("Submit feedback clicked. Backend submission not implemented in this phase.");
        }

        private void OnSignOutRequested()
        {
            Debug.Log("Sign out action placeholder. Auth flow not implemented.");
        }

        private void OnAboutRequested()
        {
            Debug.Log("About app action placeholder.");
        }

        private void OnQrCodeFound(string qrPayload)
        {
            // 1. Look up the anchor from your existing Repository
            QRAnchor anchor = _mapRepository.GetQRAnchor(qrPayload); 

            if (anchor != null)
            {
                Debug.Log($"[UIManager] QR {qrPayload} found. Performing spatial alignment...");

                // 2. Teleport the XR Origin using our AlignmentService
                _alignmentService?.Realign(anchor);

                // 3. Save the anchor ID to our central brain memory (Stage 3)
                _stateManager.Context.LastScannedAnchor = anchor;
                _stateManager.Context.CurrentFloorId = anchor.floor_id;

                // 4. In the background, load the detected floor scene
                if (_floorTransitionService != null)
                {
                    Debug.Log($"[UIManager] Loading floor scene for floor {anchor.floor_id} in background...");
                    _floorTransitionService.RequestFloorTransition(anchor.floor_id);
                }

                // 5. Now that we have both start (QR) and end (selected destination), calculate path and start navigation
                var destination = _stateManager.Context.CurrentDestination;
                if (destination != null)
                {
                    StartCoroutine(StartNavigationAfterQrScan(anchor, destination));
                }
                else
                {
                    Debug.LogError("[UIManager] QR scanned but no destination was selected!");
                }
            }
            else
            {
                Debug.LogError($"[UIManager] QR {qrPayload} not found in database!");
            }
        }

        private IEnumerator StartNavigationAfterQrScan(QRAnchor startAnchor, Destination destination)
        {
            const int maxAttempts = 30;

            // Wait for floor scene to load
            if (_floorTransitionService != null)
            {
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    var floorSceneTransitionService = _floorTransitionService as FloorSceneTransitionService;
                    if (floorSceneTransitionService != null && floorSceneTransitionService.IsTransitionInProgress)
                    {
                        Debug.Log($"[UIManager] Waiting for floor scene to load... (attempt {attempt + 1}/{maxAttempts})");
                        yield return null;
                        continue;
                    }
                    break;
                }
            }

            // Wait for navigation services to be ready
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (EnsureNavigationServices())
                {
                    break;
                }
                yield return null;
            }

            // Now calculate and draw the path
            Vector3 startPos = new Vector3(startAnchor.x, startAnchor.y, startAnchor.z);
            Vector3 targetPos = new Vector3(destination.target_x, destination.target_y, destination.target_z);

            Debug.Log($"[UIManager] Calculating path from QR position for floor {_stateManager.Context.CurrentFloorId} to destination on floor {destination.floor_id}.");
            var path = CalculatePathForCurrentFloor(startPos, targetPos, _stateManager.Context.CurrentFloorId, destination.floor_id);
            Debug.Log($"[UIManager] Path calculation returned {path?.Count ?? 0} corners.");

            if (path != null && path.Count > 0)
            {
                Debug.Log($"[UIManager] Drawing path with {path.Count} corners.");
                // Draw and set navigation state only after successful draw to avoid transient draw failures causing UI fallback
                StartCoroutine(WaitForAndDrawPath(path, true));
            }
            else
            {
                Debug.LogError("[UIManager] Failed to calculate path after QR scan!");
                _stateManager.ChangeState(AppState.Explore); // Go back to destination selection
            }
        }

        public void OnDestinationSelected(Destination dest)
        {
            if (dest == null)
            {
                Debug.LogError("[UIManager] Destination is null!");
                return;
            }

            Debug.Log($"[UIManager] Destination selected: {dest.name}. Transitioning to QR scanning to establish start position.");

            // 1. Save the selected destination for later use
            _stateManager.Context.CurrentDestination = dest;

            // 2. Transition to QR scanning to get the starting position
            // After user scans QR, OnQrCodeFound will use this destination and start navigation
            _stateManager.ChangeState(AppState.QrScanning);
        }

        private void StartTransitionArrivalWatchIfNeeded()
        {
            if (_hybridCalculator == null)
            {
                return;
            }

            if (!_hybridCalculator.TryGetPendingTransition(
                out var targetFloorId,
                out var targetFloorLabel,
                out var transitionNodeId,
                out var transitionNodePosition,
                out var transitionLandingPosition))
            {
                return;
            }

            _pendingTransitionLandingPosition = transitionLandingPosition;
            _hasPendingTransitionLanding = true;

            StopTransitionArrivalWatch();
            _transitionArrivalWatchRoutine = StartCoroutine(
                WatchForArrivalAtTransitionNode(targetFloorId, targetFloorLabel, transitionNodeId, transitionNodePosition)
            );

            if (enableUiDiagnostics)
            {
                Debug.Log($"UIManager: Watching for arrival at transition node {transitionNodeId} to prompt floor {targetFloorId}.");
            }
        }

        private void StopTransitionArrivalWatch()
        {
            if (_transitionArrivalWatchRoutine != null)
            {
                StopCoroutine(_transitionArrivalWatchRoutine);
                _transitionArrivalWatchRoutine = null;
            }
        }

        private IEnumerator WatchForArrivalAtTransitionNode(int targetFloorId, string targetFloorLabel, string transitionNodeId, Vector3 transitionNodePosition)
        {
            while (_stateManager != null && _stateManager.CurrentState == AppState.Navigating)
            {
                var cameraTransform = Camera.main != null ? Camera.main.transform : null;
                if (cameraTransform == null)
                {
                    yield return null;
                    continue;
                }

                var currentPos = cameraTransform.position;
                var horizontalDistance = Vector2.Distance(
                    new Vector2(currentPos.x, currentPos.z),
                    new Vector2(transitionNodePosition.x, transitionNodePosition.z)
                );

                if (horizontalDistance <= transitionArrivalRadiusMeters)
                {
                    if (enableUiDiagnostics)
                    {
                        Debug.Log($"UIManager: Reached transition node {transitionNodeId} (distance={horizontalDistance:F2}m). Prompting floor transition.");
                    }

                    _transitionArrivalWatchRoutine = null;
                    BeginFloorTransition(targetFloorId, targetFloorLabel, transitionNodeId);
                    yield break;
                }

                yield return null;
            }

            _transitionArrivalWatchRoutine = null;
        }

        private void OnDestinationGroupSelected(DestinationGroup group)
        {
            if (group == null || group.Entrances.Count == 0)
            {
                Debug.LogError("[UIManager] Destination group is empty.");
                return;
            }

            // Destination selection should NOT require navigation services or an active NavigationSceneContext.
            // Users pick destination first, then scan QR to establish start and load the relevant floor.
            Destination bestEntrance = null;

            if (_entranceSelector != null)
            {
                try
                {
                    bestEntrance = _entranceSelector.SelectBestEntrance(_stateManager?.Context?.LastScannedAnchor, group.Entrances);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[UIManager] Entrance selector failed: {ex.Message}. Falling back to default entrance.");
                    bestEntrance = null;
                }
            }

            // Fallback: choose the first entrance if selector is unavailable or returned null
            if (bestEntrance == null)
            {
                bestEntrance = group.Entrances.FirstOrDefault();
            }

            if (bestEntrance == null)
            {
                Debug.LogError("[UIManager] Could not resolve a valid entrance for the destination group.");
                return;
            }

            OnDestinationSelected(bestEntrance);
        }
    }
}