using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FF
{
    public class LoadingScreen : MonoBehaviour
    {
        private static LoadingScreen _instance;

        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TMP_Text messageText;
        [SerializeField, Min(0f)] private float fadeDuration = 0.25f;

        private Coroutine _fadeRoutine;

        public static void Show(string message)
        {
            EnsureInstance();
            _instance.ShowInternal(message);
        }

        public static void UpdateMessage(string message)
        {
            if (_instance == null)
            {
                return;
            }

            _instance.UpdateMessageInternal(message);
        }

        public static void Hide()
        {
            if (_instance == null)
            {
                return;
            }

            _instance.HideInternal();
        }

        private static void EnsureInstance()
        {
            if (_instance != null)
            {
                return;
            }

            var go = new GameObject("LoadingScreen");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<LoadingScreen>();
            _instance.BuildRuntimeUI();
        }

        private void BuildRuntimeUI()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            canvas.pixelPerfect = false;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            var background = new GameObject("Background");
            background.transform.SetParent(transform, false);
            backgroundImage = background.AddComponent<Image>();
            var backgroundRect = backgroundImage.rectTransform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            backgroundImage.color = new Color(0f, 0f, 0f, 0.85f);

            var textObject = new GameObject("Message");
            textObject.transform.SetParent(background.transform, false);
            messageText = textObject.AddComponent<TextMeshProUGUI>();
            var textRect = messageText.rectTransform;
            textRect.anchorMin = new Vector2(0.05f, 0.05f);
            textRect.anchorMax = new Vector2(0.95f, 0.95f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            messageText.alignment = TextAlignmentOptions.Center;
            messageText.fontSize = 32f;
            messageText.enableWordWrapping = true;
            messageText.color = Color.white;
            messageText.text = string.Empty;
        }

        private void ShowInternal(string message)
        {
            UpdateMessageInternal(message);
            gameObject.SetActive(true);
            canvasGroup.blocksRaycasts = true;
            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
            }

            _fadeRoutine = StartCoroutine(FadeCanvas(1f));
        }

        private void UpdateMessageInternal(string message)
        {
            if (messageText)
            {
                messageText.text = string.IsNullOrWhiteSpace(message) ? "Loading..." : message;
            }
        }

        private void HideInternal()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
            }

            _fadeRoutine = StartCoroutine(FadeCanvas(0f));
        }

        private IEnumerator FadeCanvas(float targetAlpha)
        {
            float duration = Mathf.Max(0.01f, fadeDuration);
            float start = canvasGroup ? canvasGroup.alpha : 0f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (canvasGroup)
                {
                    canvasGroup.alpha = Mathf.Lerp(start, targetAlpha, t);
                }

                yield return null;
            }

            if (canvasGroup)
            {
                canvasGroup.alpha = targetAlpha;
                canvasGroup.blocksRaycasts = !Mathf.Approximately(targetAlpha, 0f);
                canvasGroup.interactable = canvasGroup.blocksRaycasts;
            }

            if (Mathf.Approximately(targetAlpha, 0f))
            {
                gameObject.SetActive(false);
            }

            _fadeRoutine = null;
        }
    }
}
