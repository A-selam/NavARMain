using System;
using NavAR.Core.State;
using UnityEngine.UIElements;

namespace NavAR.Presentation.Controllers
{
    public sealed class NavigationBarController
    {
        private readonly VisualElement _root;
        private readonly Action<AppState> _setState;

        private Button _navHome;
        private Button _navExplore;
        private Button _navSaved;
        private Button _navSettings;
        private VisualElement _bottomNavBar;

        public NavigationBarController(VisualElement root, Action<AppState> setState)
        {
            _root = root;
            _setState = setState;
        }

        public void Wire()
        {
            _navHome = _root.Q<Button>("NavHome");
            _navExplore = _root.Q<Button>("NavExplore");
            _navSaved = _root.Q<Button>("NavSaved");
            _navSettings = _root.Q<Button>("NavSettings");
            _bottomNavBar = _root.Q<VisualElement>("BottomNavBar");

            if (_navHome != null)
            {
                _navHome.clicked += () => _setState(AppState.Home);
            }

            if (_navExplore != null)
            {
                _navExplore.clicked += () => _setState(AppState.Explore);
            }

            if (_navSaved != null)
            {
                _navSaved.clicked += () =>
                {
                    // Saved screen is not implemented yet.
                    _setState(AppState.Explore);
                };
            }

            if (_navSettings != null)
            {
                _navSettings.clicked += () => _setState(AppState.Settings);
            }
        }

        public void UpdateActive(AppState state)
        {
            var isMainScreen = state == AppState.Home
                || state == AppState.Explore
                || state == AppState.DestinationSelection
                || state == AppState.Settings;

            if (_bottomNavBar != null)
            {
                _bottomNavBar.style.display = isMainScreen ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (!isMainScreen)
            {
                SetNavItemActive(_navHome, false);
                SetNavItemActive(_navExplore, false);
                SetNavItemActive(_navSaved, false);
                SetNavItemActive(_navSettings, false);
                return;
            }

            SetNavItemActive(_navHome, state == AppState.Home);
            SetNavItemActive(_navExplore, state == AppState.Explore || state == AppState.DestinationSelection);
            SetNavItemActive(_navSaved, false);
            SetNavItemActive(_navSettings, state == AppState.Settings);
        }

        private static void SetNavItemActive(Button navButton, bool isActive)
        {
            if (navButton == null)
            {
                return;
            }

            const string activeClass = "nav-item-active";
            if (isActive)
            {
                navButton.AddToClassList(activeClass);
            }
            else
            {
                navButton.RemoveFromClassList(activeClass);
            }
        }
    }
}
