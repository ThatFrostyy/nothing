using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FF
{
    public class KillSlowMotion : MonoBehaviour
    {
        private static KillSlowMotion _instance;

        [Header("Slowmo")]
        [SerializeField, Range(0.05f, 1f)] private float multiKillTimeScale = 0.45f;
        [SerializeField, Min(0.1f)] private float multiKillDuration = 1.25f;
        [SerializeField, Range(0.05f, 1f)] private float bossKillTimeScale = 0.35f;
        [SerializeField, Min(0.1f)] private float bossKillDuration = 1.75f;
        [SerializeField, Min(0.1f)] private float killWindow = 2.2f;
        [SerializeField, Min(2)] private int killsNeededForSlowmo = 4;

        [Header("UI")] 
        [SerializeField] private string multiKillMessage = "BULLET TIME";
        [SerializeField] private string bossKillMessage = "BOSS DOWN";
        [SerializeField] private float bannerHoldTime = 0.6f;
        [SerializeField] private float bannerFadeTime = 0.3f;
        [SerializeField] private Color bannerColor = new(1f, 0.83f, 0.54f, 1f);

        private readonly List<float> _recentKillTimes = new();
        private Coroutine _slowmoRoutine;
        private Coroutine _bannerRoutine;
        private float _baseFixedDeltaTime;
        private CanvasGroup _bannerGroup;
        private TextMeshProUGUI _bannerText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            if (_instance)
            {
                return;
            }

            var go = new GameObject("KillSlowMotion");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<KillSlowMotion>();
        }

        private void Awake()
        {
            if (_instance && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _baseFixedDeltaTime = Time.fixedDeltaTime;
            BuildBannerUI();
        }

        private void OnEnable()
        {
            Enemy.OnAnyEnemyKilled += HandleEnemyKilled;
        }

        private void OnDisable()
        {
            Enemy.OnAnyEnemyKilled -= HandleEnemyKilled;
            RestoreTimeScale();
        }

        private void HandleEnemyKilled(Enemy enemy)
        {
            float now = Time.unscaledTime;
            _recentKillTimes.Add(now);
            _recentKillTimes.RemoveAll(t => now - t > killWindow);

            bool isBoss = enemy != null && enemy.IsBoss;
            if (isBoss)
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
            scale = Mathf.Clamp(scale, 0.05f, 1f);
            duration = Mathf.Max(0.05f, duration);

            if (_slowmoRoutine != null)
            {
                StopCoroutine(_slowmoRoutine);
            }

            _slowmoRoutine = StartCoroutine(SlowmoRoutine(scale, duration));
            PlayBanner(message);
        }

        private System.Collections.IEnumerator SlowmoRoutine(float scale, float duration)
        {
            Time.timeScale = scale;
            Time.fixedDeltaTime = _baseFixedDeltaTime * scale;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            RestoreTimeScale();
            _slowmoRoutine = null;
        }

        private void RestoreTimeScale()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _baseFixedDeltaTime;
        }

        private void BuildBannerUI()
        {
            if (_bannerGroup)
            {
                return;
            }

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

            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Vanilla Caramel SDF");
            if (!font)
            {
                font = Resources.Load<TMP_FontAsset>("Vanilla Caramel SDF 2");
            }
            if (font)
            {
                _bannerText.font = font;
            }

            _bannerGroup = bannerGo.AddComponent<CanvasGroup>();
            _bannerGroup.alpha = 0f;
        }

        private void PlayBanner(string message)
        {
            if (!_bannerGroup || !_bannerText)
            {
                return;
            }

            _bannerText.text = message;
            if (_bannerRoutine != null)
            {
                StopCoroutine(_bannerRoutine);
            }

            _bannerRoutine = StartCoroutine(BannerRoutine());
        }

        private System.Collections.IEnumerator BannerRoutine()
        {
            float elapsed = 0f;
            while (elapsed < bannerFadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / bannerFadeTime);
                _bannerGroup.alpha = t;
                yield return null;
            }

            _bannerGroup.alpha = 1f;
            float holdTimer = 0f;
            while (holdTimer < bannerHoldTime)
            {
                holdTimer += Time.unscaledDeltaTime;
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < bannerFadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = 1f - Mathf.Clamp01(elapsed / bannerFadeTime);
                _bannerGroup.alpha = t;
                yield return null;
            }

            _bannerGroup.alpha = 0f;
            _bannerRoutine = null;
        }
    }
}
