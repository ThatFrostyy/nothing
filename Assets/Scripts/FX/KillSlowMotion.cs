using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FF
{
    public class KillSlowMotion : MonoBehaviour
    {
        private static KillSlowMotion _instance;
        public static KillSlowMotion Instance => _instance;
        public static event System.Action OnBulletTimeTriggered;

        // Required by PauseMenuController
        public bool IsActive => _slowmoRoutine != null;

        [Header("Slowmo")]
        [SerializeField, Range(0.05f, 1f)] private float multiKillTimeScale = 0.45f;
        [SerializeField, Min(0.1f)] private float multiKillDuration = 1.25f;
        [SerializeField, Range(0.05f, 1f)] private float bossKillTimeScale = 0.35f;
        [SerializeField, Min(0.1f)] private float bossKillDuration = 1.75f;
        [SerializeField, Min(0.1f)] private float killWindow = 2.2f;
        [SerializeField, Min(2)] private int killsNeededForSlowmo = 4;
        [SerializeField, Min(0f)] private float slowmoCooldown = 1f;

        [Header("UI")]
        [SerializeField] private string multiKillMessage = "BULLET TIME";
        [SerializeField] private string bossKillMessage = "BOSS DOWN";
        [SerializeField] private float bannerHoldTime = 0.6f;
        [SerializeField] private float bannerFadeTime = 0.3f;
        [SerializeField] private Color bannerColor = new(1f, 0.83f, 0.54f, 1f);

        [Header("FX")]
        [SerializeField] private GameObject slowmoStartFX;
        [SerializeField] private Transform slowmoFXAnchor;

        private readonly List<float> _recentKillTimes = new();
        private Coroutine _slowmoRoutine;
        private Coroutine _bannerRoutine;
        private bool _restoreDeferred;
        private float _cooldownUntil;

        private CanvasGroup _bannerGroup;
        private TextMeshProUGUI _bannerText;
        private Transform _cachedPlayer;

        private void Awake()
        {
            if (_instance && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            Time.timeScale = 1f;
            BuildBannerUI();
        }

        private void OnEnable()
        {
            Enemy.OnAnyEnemyKilled += HandleEnemyKilled;
            PlayerController.OnPlayerReady += HandlePlayerReady;
        }

        private void OnDisable()
        {
            Enemy.OnAnyEnemyKilled -= HandleEnemyKilled;
            PlayerController.OnPlayerReady -= HandlePlayerReady;
            RestoreTimeScale();
        }

        private void HandlePlayerReady(PlayerController controller)
        {
            if (controller != null)
            {
                _cachedPlayer = controller.transform;
                if (!slowmoFXAnchor)
                {
                    slowmoFXAnchor = _cachedPlayer;
                }
            }
        }

        private void HandleEnemyKilled(Enemy enemy)
        {
            float now = Time.unscaledTime;

            _recentKillTimes.Add(now);
            _recentKillTimes.RemoveAll(t => now - t > killWindow);

            if (enemy != null && enemy.IsBoss)
            {
                TriggerSlowmo(bossKillTimeScale, bossKillDuration, bossKillMessage);
                return;
            }

            if (_recentKillTimes.Count >= killsNeededForSlowmo)
            {
                TriggerSlowmo(multiKillTimeScale, multiKillDuration, multiKillMessage);
            }
        }

        private void TriggerSlowmo(float scale, float duration, string message)
        {
            if (_slowmoRoutine == null && Time.unscaledTime < _cooldownUntil)
            {
                return;
            }

            scale = Mathf.Clamp(scale, 0.05f, 1f);
            duration = Mathf.Max(0.05f, duration);

            if (_slowmoRoutine != null)
                StopCoroutine(_slowmoRoutine);

            PlaySlowmoStartFx();
            _slowmoRoutine = StartCoroutine(SlowmoRoutine(scale, duration));
            PlayBanner(message);
            OnBulletTimeTriggered?.Invoke();
        }

        private System.Collections.IEnumerator SlowmoRoutine(float scale, float duration)
        {
            Time.timeScale = scale;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            RestoreTimeScale();
            _cooldownUntil = Time.unscaledTime + slowmoCooldown;
            _slowmoRoutine = null;
        }

        private void RestoreTimeScale()
        {
            if (PauseMenuController.IsMenuOpen)
            {
                _restoreDeferred = true;
                return;
            }

            Time.timeScale = 1f;
            _restoreDeferred = false;
        }

        public static void EnsureRestoredAfterPause()
        {
            if (_instance == null) return;
            if (_instance._restoreDeferred) _instance.RestoreTimeScale();
        }

        private void Update()
        {
            // If the menu closed (fade finished) but we deferred, restore now.
            if (_restoreDeferred && !PauseMenuController.IsMenuOpen)
            {
                RestoreTimeScale();
            }
        }

        private void BuildBannerUI()
        {
            if (_bannerGroup) return;

            var canvasGo = new GameObject("SlowmoCanvas", typeof(RectTransform));
            DontDestroyOnLoad(canvasGo);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            var bannerGo = new GameObject("SlowmoBanner", typeof(RectTransform));
            bannerGo.transform.SetParent(canvasGo.transform, false);
            var rect = bannerGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.6f);
            rect.anchorMax = new Vector2(0.5f, 0.6f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(640f, 180f);

            _bannerText = bannerGo.AddComponent<TextMeshProUGUI>();
            _bannerText.alignment = TextAlignmentOptions.Center;
            _bannerText.fontSize = 72f;
            _bannerText.textWrappingMode = TextWrappingModes.NoWrap;
            _bannerText.color = bannerColor;
            _bannerText.text = string.Empty;
            _bannerText.raycastTarget = false;

            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Vanilla Caramel SDF");
            if (!font) font = Resources.Load<TMP_FontAsset>("Vanilla Caramel SDF 2");
            if (font) _bannerText.font = font;

            _bannerGroup = bannerGo.AddComponent<CanvasGroup>();
            _bannerGroup.alpha = 0f;
        }

        private void PlayBanner(string message)
        {
            if (!_bannerGroup || !_bannerText) return;
            _bannerText.text = message;
            if (_bannerRoutine != null) StopCoroutine(_bannerRoutine);
            _bannerRoutine = StartCoroutine(BannerRoutine());
        }

        private System.Collections.IEnumerator BannerRoutine()
        {
            float elapsed = 0f;
            while (elapsed < bannerFadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                _bannerGroup.alpha = Mathf.Clamp01(elapsed / bannerFadeTime);
                yield return null;
            }
            _bannerGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(bannerHoldTime);
            elapsed = 0f;
            while (elapsed < bannerFadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                _bannerGroup.alpha = 1f - Mathf.Clamp01(elapsed / bannerFadeTime);
                yield return null;
            }
            _bannerGroup.alpha = 0f;
            _bannerRoutine = null;
        }

        private void PlaySlowmoStartFx()
        {
            if (!slowmoStartFX)
            {
                return;
            }

            Vector3 position = slowmoFXAnchor ? slowmoFXAnchor.position : (_cachedPlayer ? _cachedPlayer.position : Vector3.zero);
            GameObject fx = PoolManager.Get(slowmoStartFX, position, Quaternion.identity);
            if (fx && !fx.TryGetComponent<PooledParticleSystem>(out var pooled))
            {
                pooled = fx.AddComponent<PooledParticleSystem>();
                pooled.OnTakenFromPool();
            }
        }
    }
}
