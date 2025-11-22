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
        [SerializeField] private Color overlayColor = new(0f, 0f, 0f, 0.75f);
        [SerializeField] private MapOption[] maps = { new() { SceneName = "Main", DisplayName = "Main" } };
        [SerializeField] private TMP_FontAsset defaultFont;

        private GameObject mapOverlay;
        private TMP_Text mapLabel;
        private Button startButton;
        private Button cancelButton;
        private Button previousButton;
        private Button nextButton;
        private int mapIndex;

        private GameObject loadingOverlay;
        private TMP_Text loadingLabel;
        private Slider loadingSlider;
        private Coroutine loadingRoutine;

        void Awake()
        {
            if (!sceneFlow)
            {
                sceneFlow = FindFirstObjectByType<SceneFlowController>();
            }

            if (!playButton)
            {
                playButton = GetComponentInChildren<Button>();
            }

            BuildMapOverlay();
            BuildLoadingOverlay();

            if (playButton)
            {
                bool hasPersistent = false;
                for (int i = 0; i < playButton.onClick.GetPersistentEventCount(); i++)
                {
                    if (playButton.onClick.GetPersistentMethodName(i) == nameof(OpenMapSelection))
                    {
                        hasPersistent = true;
                        break;
                    }
                }

                if (!hasPersistent)
                {
                    playButton.onClick.RemoveListener(OpenMapSelection);
                    playButton.onClick.AddListener(OpenMapSelection);
                }
            }
        }

        void Start()
        {
            RefreshMapLabel();
            ShowMapOverlay(false);
            ShowLoadingOverlay(false, 0f);
        }

        public void OpenMapSelection()
        {
            RefreshMapLabel();
            ShowMapOverlay(true);
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
            if (mapLabel == null)
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

        private void BuildMapOverlay()
        {
            mapOverlay = CreateOverlay("MapSelectionOverlay", overlayColor);
            mapOverlay.SetActive(true);

            RectTransform root = mapOverlay.GetComponent<RectTransform>();

            TMP_Text title = CreateText("Select a Map", root, new Vector2(0.5f, 0.8f), 36);
            mapLabel = CreateText("Map: Main", root, new Vector2(0.5f, 0.65f), 30);

            previousButton = CreateButton("Previous", root, new Vector2(0.35f, 0.5f));
            previousButton.onClick.AddListener(() => StepMap(-1));

            nextButton = CreateButton("Next", root, new Vector2(0.65f, 0.5f));
            nextButton.onClick.AddListener(() => StepMap(1));

            startButton = CreateButton("Start", root, new Vector2(0.5f, 0.35f));
            startButton.onClick.AddListener(BeginLoadingSelectedMap);

            cancelButton = CreateButton("Cancel", root, new Vector2(0.5f, 0.22f));
            cancelButton.onClick.AddListener(() => ShowMapOverlay(false));

            mapOverlay.SetActive(false);
        }

        private void BuildLoadingOverlay()
        {
            loadingOverlay = CreateOverlay("LoadingOverlay", overlayColor);
            loadingOverlay.SetActive(true);

            RectTransform root = loadingOverlay.GetComponent<RectTransform>();
            loadingLabel = CreateText("Loading...", root, new Vector2(0.5f, 0.55f), 32);

            GameObject sliderObject = new("LoadingSlider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Slider));
            sliderObject.transform.SetParent(root, false);

            RectTransform rect = sliderObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.2f, 0.45f);
            rect.anchorMax = new Vector2(0.8f, 0.45f);
            rect.sizeDelta = new Vector2(0f, 24f);

            Image background = sliderObject.GetComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0.25f);

            loadingSlider = sliderObject.GetComponent<Slider>();
            loadingSlider.minValue = 0f;
            loadingSlider.maxValue = 1f;
            loadingSlider.value = 0f;
            loadingSlider.transition = Selectable.Transition.None;

            GameObject fillArea = new("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObject.transform, false);
            RectTransform fillRect = fillArea.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0.05f, 0.25f);
            fillRect.anchorMax = new Vector2(0.95f, 0.75f);
            fillRect.sizeDelta = Vector2.zero;

            GameObject fill = new("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fillTransform = fill.GetComponent<RectTransform>();
            fillTransform.anchorMin = new Vector2(0f, 0f);
            fillTransform.anchorMax = new Vector2(1f, 1f);
            fillTransform.sizeDelta = Vector2.zero;

            Image fillImage = fill.GetComponent<Image>();
            fillImage.color = new Color(0.3f, 0.9f, 0.6f, 0.9f);

            loadingSlider.fillRect = fillTransform;
            loadingSlider.targetGraphic = fillImage;

            loadingOverlay.SetActive(false);
        }

        private GameObject CreateOverlay(string name, Color color)
        {
            GameObject overlay = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlay.transform.SetParent(transform, false);

            RectTransform rect = overlay.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = overlay.GetComponent<Image>();
            image.color = color;

            return overlay;
        }

        private TMP_Text CreateText(string content, RectTransform parent, Vector2 anchor, float fontSize)
        {
            GameObject obj = new("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);

            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(600f, 80f);

            TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = fontSize;
            text.font = defaultFont;

            return text;
        }

        private Button CreateButton(string label, RectTransform parent, Vector2 anchor)
        {
            GameObject obj = new(label + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);

            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(240f, 60f);

            Image image = obj.GetComponent<Image>();
            image.color = new Color(0.17f, 0.18f, 0.2f, 0.85f);

            Button button = obj.GetComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;

            TMP_Text text = CreateText(label, rect, new Vector2(0.5f, 0.5f), 26f);
            text.rectTransform.SetParent(obj.transform, false);
            text.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            text.rectTransform.anchoredPosition = Vector2.zero;
            text.rectTransform.sizeDelta = Vector2.zero;

            return button;
        }

        private void BeginLoadingSelectedMap()
        {
            string sceneName = null;
            if (maps != null && maps.Length > 0)
            {
                MapOption selected = maps[Mathf.Clamp(mapIndex, 0, maps.Length - 1)];
                sceneName = string.IsNullOrWhiteSpace(selected.SceneName) ? sceneFlow?.GameplaySceneName : selected.SceneName;
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                sceneName = sceneFlow ? sceneFlow.GameplaySceneName : SceneManager.GetActiveScene().name;
            }

            ShowMapOverlay(false);
            StartLoading(sceneName);
        }

        private void StartLoading(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

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
                yield break;
            }

            while (!operation.isDone)
            {
                float progress = Mathf.Clamp01(operation.progress / 0.9f);
                ShowLoadingOverlay(true, progress);
                yield return null;
            }
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
