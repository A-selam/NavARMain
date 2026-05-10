using System;
using NavAR.Core.State;
using UnityEngine;
using UnityEngine.UIElements;

namespace NavAR.Presentation.Controllers
{
    public static class ScreenBinders
    {
        public static void WireHome(VisualElement content, Action<AppState> setState)
        {
            var startBtn = content.Q<Button>("BtnStartNavigation");
            var helpButton = content.Q<Button>("BtnViewHelp");
            var tutorialButton = content.Q<Button>("BtnLaunchTutorial");

            if (startBtn != null)
            {
                startBtn.clicked += () => setState(AppState.Explore); // Changed from QrScanning to Explore/DestinationSelection
            }

            if (helpButton != null)
            {
                helpButton.clicked += () => setState(AppState.Feedback);
            }

            if (tutorialButton != null)
            {
                tutorialButton.clicked += () =>
                {
                    Debug.Log("Launch Tutorial clicked. Tutorial workflow not implemented yet.");
                };
            }
        }

        public static void WireExplore(VisualElement content, Action<AppState> setState)
        {
            var clearButton = content.Q<Button>("BtnClearRecent");
            var quickPrint = content.Q<Button>("BtnQuickPrint");
            var quickExit = content.Q<Button>("BtnQuickExit");
            var backButton = content.Q<Button>("BackButton");

            if (clearButton != null)
            {
                clearButton.clicked += () =>
                {
                    var listContainer = content.Q<ScrollView>("DestinationListContainer");
                    listContainer?.Clear();
                };
            }

            if (quickPrint != null)
            {
                quickPrint.clicked += () => setState(AppState.Navigating);
            }

            if (quickExit != null)
            {
                quickExit.clicked += () => setState(AppState.Navigating);
            }

            if (backButton != null)
            {
                backButton.clicked += () => setState(AppState.Home);
            }
        }

        public static void WireQrScanner(VisualElement content, Action<AppState> setState)
        {
            var closeButton = content.Q<Button>("BtnCloseScanner");
            if (closeButton != null)
            {
                closeButton.clicked += () => setState(AppState.Home);
            }
        }

        public static void WirePermission(
            VisualElement content,
            Action<AppState> setState,
            Action requestPermissionAction
        )
        {
            var allowButton = content.Q<Button>("AllowButton");
            var cancelButton = content.Q<Button>("CancelButton");
            var backButton = content.Q<Button>("BtnPermissionBack");

            if (allowButton != null)
            {
                allowButton.clicked += () =>
                {
                    requestPermissionAction?.Invoke();
                    setState(AppState.QrScanning);
                };
            }

            if (cancelButton != null)
            {
                cancelButton.clicked += () => setState(AppState.Home);
            }

            if (backButton != null)
            {
                backButton.clicked += () => setState(AppState.Home);
            }
        }

        public static void WireArNavigation(
            VisualElement content,
            Action<AppState> setState,
            Func<AppState> getLastNonOverlayState,
            Action onToggleVoice,
            Action onOpenMap
        )
        {
            var backButton = content.Q<Button>("BtnArBack");
            var rescanButton = content.Q<Button>("BtnRescan");
            var endButton = content.Q<Button>("BtnEnd");
            var helpButton = content.Q<Button>("BtnHelp");
            var audioButton = content.Q<Button>("BtnAudio");
            var mapButton = content.Q<Button>("BtnMap");

            if (backButton != null)
            {
                backButton.clicked += () => setState(getLastNonOverlayState());
            }

            if (rescanButton != null)
            {
                rescanButton.clicked += () => setState(AppState.QrScanning);
            }

            if (endButton != null)
            {
                endButton.clicked += () => setState(AppState.Feedback);
            }

            if (helpButton != null)
            {
                helpButton.clicked += () => setState(AppState.Feedback);
            }

            if (audioButton != null)
            {
                audioButton.clicked += () => onToggleVoice?.Invoke();
            }

            if (mapButton != null)
            {
                mapButton.clicked += () => onOpenMap?.Invoke();
            }
        }

        public static void WirePositionLost(VisualElement content, Action<AppState> setState)
        {
            var scanButton = content.Q<Button>("BtnScanRecovery");
            var resumeButton = content.Q<Button>("BtnResumeNavigation");
            var cancelButton = content.Q<Button>("BtnCancelRecovery");

            if (scanButton != null)
            {
                scanButton.clicked += () => setState(AppState.QrScanning);
            }

            if (resumeButton != null)
            {
                resumeButton.clicked += () => setState(AppState.Navigating);
            }

            if (cancelButton != null)
            {
                cancelButton.clicked += () => setState(AppState.Home);
            }
        }

        public static void WireFeedback(
            VisualElement content,
            Action<AppState> setState,
            Func<AppState> getLastNonOverlayState,
            Action onSubmitFeedback
        )
        {
            var backButton = content.Q<Button>("BtnBackFeedback");
            var submitButton = content.Q<Button>("BtnSubmitFeedback");

            if (backButton != null)
            {
                backButton.clicked += () => setState(getLastNonOverlayState());
            }

            if (submitButton != null)
            {
                submitButton.clicked += () =>
                {
                    onSubmitFeedback?.Invoke();
                    setState(AppState.Home);
                };
            }
        }

        public static void WireSettings(
            VisualElement content,
            Action<AppState> setState,
            Action onSignOut,
            Action onAbout
        )
        {
            var slider = content.Q<SliderInt>("TextSizeSlider");
            var valueLabel = content.Q<Label>("TextSizeValueLabel");
            var signOut = content.Q<Button>("BtnSignOut");
            var helpCenter = content.Q<Button>("BtnHelpCenter");
            var about = content.Q<Button>("BtnAboutApp");

            if (slider != null && valueLabel != null)
            {
                valueLabel.text = $"{slider.value}%";
                slider.RegisterValueChangedCallback(evt =>
                {
                    valueLabel.text = $"{evt.newValue}%";
                });
            }

            if (signOut != null)
            {
                signOut.clicked += () => onSignOut?.Invoke();
            }

            if (helpCenter != null)
            {
                helpCenter.clicked += () => setState(AppState.Feedback);
            }

            if (about != null)
            {
                about.clicked += () => onAbout?.Invoke();
            }
        }
    }
}
