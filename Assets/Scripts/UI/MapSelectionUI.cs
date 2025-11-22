using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FF
{
    public class MapSelectionUI : MonoBehaviour
    {
        [System.Serializable]
        public struct MapOption
        {
            public string SceneName;
            public string DisplayName;
        }

        [SerializeField] private SceneFlowController sceneFlow;
        [SerializeField] private Button playButton;
        [SerializeField] private Button startButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private GameObject mapOverlay;
        [SerializeField] private TMP_Text mapLabel;
        [SerializeField] private GameObject loadingOverlay;
        [SerializeField] private TMP_Text loadingLabel;
        [SerializeField] private Slider loadingSlider;
        [SerializeField] private MapOption[] maps = { new() { SceneName = "Main", DisplayName = "Main" } };

        private Coroutine loadingRoutine;
        private int mapIndex;
        private bool buttonsWired;

        void Awake()
        {
            if (!sceneFlow)
            {
                sceneFlow = FindFirstObjectByType<SceneFlowController>();
            }
        }

        void OnEnable()
        {
            WireButtons();
            RefreshMapLabel();
            ShowMapOverlay(false);
            ShowLoadingOverlay(false, 0f);
        }

        void OnDisable()
        {
            UnwireButtons();

            if (loadingRoutine != null)
            {
                StopCoroutine(loadingRoutine);
                loadingRoutine = null;
            }
        }

        public void OpenMapSelection()
        {
            RefreshMapLabel();
            ShowMapOverlay(true);
        }

        public void CloseMapSelection()
        {
            ShowMapOverlay(false);
        }

        public void SelectNextMap()
        {
            StepMap(1);
        }

        public void SelectPreviousMap()
        {
            StepMap(-1);
        }

        private void WireButtons()
        {
            if (buttonsWired)
            {
                return;
            }

            if (playButton)
            {
                playButton.onClick.AddListener(OpenMapSelection);
            }

            if (startButton)
            {
                startButton.onClick.AddListener(BeginLoadingSelectedMap);
            }

            if (cancelButton)
            {
                cancelButton.onClick.AddListener(CloseMapSelection);
            }

            if (previousButton)
            {
                previousButton.onClick.AddListener(SelectPreviousMap);
            }

            if (nextButton)
            {
                nextButton.onClick.AddListener(SelectNextMap);
            }

            buttonsWired = true;
        }

        private void UnwireButtons()
        {
            if (playButton) playButton.onClick.RemoveListener(OpenMapSelection);
            if (startButton) startButton.onClick.RemoveListener(BeginLoadingSelectedMap);
            if (cancelButton) cancelButton.onClick.RemoveListener(CloseMapSelection);
            if (previousButton) previousButton.onClick.RemoveListener(SelectPreviousMap);
            if (nextButton) nextButton.onClick.RemoveListener(SelectNextMap);

            buttonsWired = false;
        }

        private void StepMap(int delta)
        {
            if (maps == null || maps.Length == 0)
            {
                return;
            }

            mapIndex = Mathf.FloorToInt(Mathf.Repeat(mapIndex + delta, maps.Length));
            RefreshMapLabel();
        }

        private void RefreshMapLabel()
        {
            if (!mapLabel)
            {
                return;
            }

            if (maps == null || maps.Length == 0)
            {
                mapLabel.text = "No Maps Configured";
                return;
            }

            MapOption current = maps[Mathf.Clamp(mapIndex, 0, maps.Length - 1)];
            string displayName = string.IsNullOrWhiteSpace(current.DisplayName) ? current.SceneName : current.DisplayName;
            mapLabel.text = $"Map: {displayName}";
        }

        private void BeginLoadingSelectedMap()
        {
            string sceneName = GetSelectedSceneName();
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            ShowMapOverlay(false);
            StartLoading(sceneName);
        }

        private string GetSelectedSceneName()
        {
            if (maps != null && maps.Length > 0)
            {
                MapOption selected = maps[Mathf.Clamp(mapIndex, 0, maps.Length - 1)];
                if (!string.IsNullOrWhiteSpace(selected.SceneName))
                {
                    return selected.SceneName;
                }
            }

            if (sceneFlow && !string.IsNullOrWhiteSpace(sceneFlow.GameplaySceneName))
            {
                return sceneFlow.GameplaySceneName;
            }

            return SceneManager.GetActiveScene().name;
        }

        private void StartLoading(string sceneName)
        {
            if (loadingRoutine != null)
            {
                StopCoroutine(loadingRoutine);
            }

            loadingRoutine = StartCoroutine(LoadSceneRoutine(sceneName));
        }

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            ShowLoadingOverlay(true, 0f);

            AsyncOperation operation = sceneFlow ? sceneFlow.LoadSceneAsync(sceneName) : SceneManager.LoadSceneAsync(sceneName);
            if (operation == null)
            {
                ShowLoadingOverlay(false, 0f);
                yield break;
            }

            while (!operation.isDone)
            {
                float progress = Mathf.Clamp01(operation.progress / 0.9f);
                ShowLoadingOverlay(true, progress);
                yield return null;
            }

            ShowLoadingOverlay(false, 1f);
        }

        private void ShowMapOverlay(bool visible)
        {
            if (mapOverlay)
            {
                mapOverlay.SetActive(visible);
            }
        }

        private void ShowLoadingOverlay(bool visible, float progress)
        {
            if (loadingOverlay)
            {
                loadingOverlay.SetActive(visible);
            }

            if (loadingSlider)
            {
                loadingSlider.value = progress;
            }

            if (loadingLabel)
            {
                int percent = Mathf.RoundToInt(progress * 100f);
                loadingLabel.text = visible ? $"Loading... {percent}%" : string.Empty;
            }
        }
    }
}
