using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using NavAR.Core.Interfaces;

namespace NavAR.Infrastructure.Navigation
{
    public class FloorSceneTransitionService : MonoBehaviour, IFloorSceneTransitionService
    {
        [Header("Scene Naming")]
        [SerializeField] private string floorScenePrefix = "Floor_";
        [SerializeField] private string floorSceneSuffix = string.Empty;

        [Header("Transition Behavior")]
        [SerializeField] private bool unloadOtherFloorScenes = true;
        [SerializeField] private bool setLoadedFloorActive = true;
        [SerializeField] private bool logVerbose = true;

        private int _pendingFloorId = -1;
        private bool _transitionInProgress;

        public bool IsTransitionInProgress => _transitionInProgress;

        public void RequestFloorTransition(int targetFloorId)
        {
            if (targetFloorId < 0)
            {
                Debug.LogWarning("FloorSceneTransitionService: Ignoring invalid floor id.");
                return;
            }

            if (_transitionInProgress && _pendingFloorId == targetFloorId)
            {
                if (logVerbose)
                {
                    Debug.Log($"FloorSceneTransitionService: Transition to floor {targetFloorId} is already in progress.");
                }
                return;
            }

            _pendingFloorId = targetFloorId;
            StartCoroutine(TransitionRoutine(targetFloorId));
        }

        public bool IsFloorSceneReady(int targetFloorId)
        {
            var scene = SceneManager.GetSceneByName(BuildSceneName(targetFloorId));
            return scene.IsValid() && scene.isLoaded && !_transitionInProgress && _pendingFloorId < 0;
        }

        private System.Collections.IEnumerator TransitionRoutine(int targetFloorId)
        {
            _transitionInProgress = true;

            var targetSceneName = BuildSceneName(targetFloorId);
            if (logVerbose)
            {
                Debug.Log($"FloorSceneTransitionService: Loading floor scene '{targetSceneName}'.");
            }

            var targetScene = SceneManager.GetSceneByName(targetSceneName);
            if (!targetScene.isLoaded)
            {
                var loadOperation = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Additive);
                if (loadOperation == null)
                {
                    Debug.LogError($"FloorSceneTransitionService: Failed to start loading scene '{targetSceneName}'. Make sure it is added to Build Settings.");
                    _transitionInProgress = false;
                    yield break;
                }

                yield return loadOperation;
                targetScene = SceneManager.GetSceneByName(targetSceneName);
            }

            if (!targetScene.IsValid() || !targetScene.isLoaded)
            {
                Debug.LogError($"FloorSceneTransitionService: Scene '{targetSceneName}' was not loaded successfully.");
                _transitionInProgress = false;
                yield break;
            }

            if (setLoadedFloorActive)
            {
                SceneManager.SetActiveScene(targetScene);
            }

            if (unloadOtherFloorScenes)
            {
                yield return UnloadOtherFloorScenes(targetSceneName);
            }

            if (logVerbose)
            {
                Debug.Log($"FloorSceneTransitionService: Floor {targetFloorId} is ready.");
            }

            _pendingFloorId = -1;
            _transitionInProgress = false;
        }

        private System.Collections.IEnumerator UnloadOtherFloorScenes(string keepSceneName)
        {
            // Collect all floor scenes to unload FIRST to avoid index shifting during iteration
            var scenesToUnload = new System.Collections.Generic.List<Scene>();
            var sceneCount = SceneManager.sceneCount;
            
            for (var index = 0; index < sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                if (!scene.name.StartsWith(floorScenePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (scene.name.Equals(keepSceneName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                scenesToUnload.Add(scene);
            }

            // Now unload collected scenes
            foreach (var scene in scenesToUnload)
            {
                if (logVerbose)
                {
                    Debug.Log($"FloorSceneTransitionService: Unloading floor scene '{scene.name}'.");
                }

                var unloadOperation = SceneManager.UnloadSceneAsync(scene);
                if (unloadOperation != null)
                {
                    yield return unloadOperation;
                }
            }
        }

        private string BuildSceneName(int floorId)
        {
            return $"{floorScenePrefix}{floorId}{floorSceneSuffix}";
        }
    }
}
