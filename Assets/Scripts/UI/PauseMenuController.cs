using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FF
{
    public class PauseMenuController : MonoBehaviour
    {
        public static PauseMenuController Instance { get; private set; }
        public static bool IsMenuOpen => Instance != null && Instance._isVisible;

        [Header("UI")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image backdrop;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private string pauseTitle = "Paused";
        [SerializeField] private string deathTitle = "Defeated";
        [SerializeField, Min(0f)] private float fadeDuration = 0.25f;
        [SerializeField, Range(0f, 1f)] private float backdropAlpha = 0.7f;

        [Header("Flow")]
        [SerializeField] private SceneFlowController sceneFlow;

        [Header("Death Feedback")]
        [SerializeField] private AudioClip deathClip;
        [SerializeField] private GameObject deathEffectPrefab;

        private bool _isVisible;
        private bool _isDeathMenu;
        private float _previousTimeScale = 1f;
        private Coroutine _fadeRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (!sceneFlow)
            {
                sceneFlow = FindAnyObjectByType<SceneFlowController>();
            }

            if (!deathClip)
            {
                deathClip = Resources.Load<AudioClip>("Sounds/death1");
            }

            if (!deathEffectPrefab)
            {
                deathEffectPrefab = Resources.Load<GameObject>("Prefabs/FX/Death");
            }

            if (resumeButton && restartButton && mainMenuButton)
            {
                resumeButton.onClick.AddListener(HideMenu);
                restartButton.onClick.AddListener(RestartScene);
                mainMenuButton.onClick.AddListener(ReturnToMainMenu);
            }

            ApplyVisibility(0f, true);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        public static void TogglePause()
        {
            if (UpgradeUI.IsShowing)
            {
                return;
            }

            if (Instance == null)
            {
                CreateRuntimeInstance();
            }

            if (Instance._isDeathMenu)
            {
                return;
            }

            if (Instance._isVisible && !Instance._isDeathMenu)
            {
                Instance.HideMenu();
            }
            else
            {
                Instance.ShowMenu(false);
            }
        }

        public static void ShowDeathMenu(Vector3 deathPosition)
        {
            if (Instance == null)
            {
                CreateRuntimeInstance();
            }

            Instance.PlayDeathFeedback(deathPosition);
            Instance.ShowMenu(true);
        }

        private void ShowMenu(bool isDeath)
        {
            _isVisible = true;
            _isDeathMenu = isDeath;
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            if (titleText)
            {
                titleText.text = isDeath ? deathTitle : pauseTitle;
            }

            if (resumeButton)
            {
                resumeButton.gameObject.SetActive(!isDeath);
            }

            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
            }

            _fadeRoutine = StartCoroutine(FadeCanvas(1f));
        }

        private void HideMenu()
        {
            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
            }

            _isVisible = false;
            _isDeathMenu = false;
            _fadeRoutine = StartCoroutine(FadeCanvas(0f, RestoreTimeScale));
        }

        private void RestoreTimeScale()
        {
            float targetScale = Mathf.Approximately(_previousTimeScale, 0f) ? 1f : _previousTimeScale;
            Time.timeScale = targetScale;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!sceneFlow)
            {
                sceneFlow = FindAnyObjectByType<SceneFlowController>();
            }

            HideInstant();
        }

        private IEnumerator FadeCanvas(float targetAlpha, System.Action onComplete = null)
        {
            float duration = Mathf.Max(0.01f, fadeDuration);
            float start = canvasGroup ? canvasGroup.alpha : 0f;
            float elapsed = 0f;

            if (canvasGroup)
            {
                canvasGroup.gameObject.SetActive(true);
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                ApplyVisibility(Mathf.Lerp(start, targetAlpha, t), false);
                yield return null;
            }

            ApplyVisibility(targetAlpha, true);
            _fadeRoutine = null;
            onComplete?.Invoke();
        }

        private void ApplyVisibility(float alpha, bool finalize)
        {
            if (canvasGroup)
            {
                canvasGroup.alpha = alpha;
                if (finalize && Mathf.Approximately(alpha, 0f))
                {
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;
                    canvasGroup.gameObject.SetActive(false);
                }
            }

            if (backdrop)
            {
                Color c = backdrop.color;
                c.a = Mathf.Lerp(0f, backdropAlpha, alpha);
                backdrop.color = c;
            }
        }

        private TextMeshProUGUI BuildLabel(RectTransform parent, string name, float size, Vector2 anchorMin, Vector2 anchorMax, Vector2 offset)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(380f, 48f);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = size;
            label.text = pauseTitle;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        private void HideInstant()
        {
            _isVisible = false;
            _isDeathMenu = false;
            ApplyVisibility(0f, true);
            RestoreTimeScale();
        }

        private static void CreateRuntimeInstance()
        {
            var host = new GameObject("PauseMenuController");
            host.AddComponent<PauseMenuController>();
        }

        private void RestartScene()
        {
            if (sceneFlow == null)
            {
                sceneFlow = FindAnyObjectByType<SceneFlowController>();
            }

            StartCoroutine(LoadWithFade(() => sceneFlow?.ReloadCurrentScene()));
        }

        private void ReturnToMainMenu()
        {
            if (sceneFlow == null)
            {
                sceneFlow = FindAnyObjectByType<SceneFlowController>();
            }

            StartCoroutine(LoadWithFade(() => sceneFlow?.LoadMainMenuScene()));
        }

        private IEnumerator LoadWithFade(System.Action loadAction)
        {
            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
            }

            _isVisible = true;
            _fadeRoutine = StartCoroutine(FadeCanvas(1f));
            yield return new WaitForSecondsRealtime(fadeDuration);

            loadAction?.Invoke();
        }

        private void PlayDeathFeedback(Vector3 position)
        {
            if (deathEffectPrefab)
            {
                PoolManager.Get(deathEffectPrefab, position, Quaternion.identity);
            }

            if (deathClip)
            {
                AudioPlaybackPool.PlayOneShot(deathClip, position);
            }
        }
    }
}
