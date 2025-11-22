using UnityEngine;
using UnityEngine.UI;

namespace FF
{
    public class LoadingScreen : MonoBehaviour
    {
        private static LoadingScreen _instance;

        private CanvasGroup _canvasGroup;
        private Image _progressFill;
        private Text _statusLabel;

        public static LoadingScreen Instance
        {
            get
            {
                if (_instance == null)
                {
                    CreateInstance();
                }

                return _instance;
            }
        }

        private static void CreateInstance()
        {
            GameObject root = new("LoadingScreen");

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            root.AddComponent<GraphicRaycaster>();

            _instance = root.AddComponent<LoadingScreen>();
            DontDestroyOnLoad(root);

            _instance.BuildUI();
        }

        private void BuildUI()
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = false;

            RectTransform rootRect = GetComponent<RectTransform>();
            if (rootRect)
            {
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
            }

            Image backdrop = CreateChildImage("Backdrop", new Color(0f, 0f, 0f, 0.75f));
            backdrop.rectTransform.anchorMin = Vector2.zero;
            backdrop.rectTransform.anchorMax = Vector2.one;
            backdrop.rectTransform.offsetMin = Vector2.zero;
            backdrop.rectTransform.offsetMax = Vector2.zero;

            _progressFill = CreateProgressBar();
            _statusLabel = CreateStatusLabel();
        }

        private Image CreateChildImage(string name, Color color)
        {
            GameObject obj = new(name);
            obj.transform.SetParent(transform, false);

            Image image = obj.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private Image CreateProgressBar()
        {
            Image track = CreateChildImage("ProgressTrack", new Color(1f, 1f, 1f, 0.15f));
            RectTransform trackRect = track.rectTransform;
            trackRect.anchorMin = new Vector2(0.1f, 0.12f);
            trackRect.anchorMax = new Vector2(0.9f, 0.18f);
            trackRect.offsetMin = Vector2.zero;
            trackRect.offsetMax = Vector2.zero;

            GameObject fillObj = new("ProgressFill");
            fillObj.transform.SetParent(track.transform, false);

            Image fill = fillObj.AddComponent<Image>();
            fill.color = new Color(1f, 1f, 1f, 0.9f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;

            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            return fill;
        }

        private Text CreateStatusLabel()
        {
            GameObject labelObject = new("Status");
            labelObject.transform.SetParent(transform, false);

            Text label = labelObject.AddComponent<Text>();
            label.text = "Loading...";
            label.fontSize = 28;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0.1f, 0.18f);
            rect.anchorMax = new Vector2(0.9f, 0.26f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return label;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            _canvasGroup.alpha = 1f;
            UpdateProgress(0f);
        }

        public void Hide()
        {
            _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        public void UpdateProgress(float progress)
        {
            if (_progressFill)
            {
                _progressFill.fillAmount = Mathf.Clamp01(progress);
            }

            if (_statusLabel)
            {
                int percent = Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f);
                _statusLabel.text = $"Loading... {percent}%";
            }
        }
    }
}
