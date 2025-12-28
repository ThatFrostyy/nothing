using System;
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
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button closeSettingsButton;
        [SerializeField] private string pauseTitle = "Paused";
        [SerializeField] private string deathTitle = "Defeated";
        [SerializeField, Min(0f)] private float fadeDuration = 0.25f;
        [SerializeField, Range(0f, 1f)] private float backdropAlpha = 0.7f;
        [SerializeField] private TextMeshProUGUI roundKillsText;
        [SerializeField] private TextMeshProUGUI lastWaveText;
        [SerializeField] private TextMeshProUGUI mostUsedWeaponText;
        [SerializeField] private string unavailableStatText = "--";

        [Header("Settings")]
        [SerializeField] private CanvasGroup settingsGroup;
        [SerializeField] private GameObject settingsRoot;
        [SerializeField] private MusicSettingsUI musicSettingsUI;
        [SerializeField] private KeybindSettingsUI keybindSettingsUI;
        [SerializeField] private VideoSettingsUI videoSettingsUI;

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

            if (!sceneFlow) sceneFlow = FindAnyObjectByType<SceneFlowController>();
            if (!deathClip) deathClip = Resources.Load<AudioClip>("Sounds/death1");
            if (!deathEffectPrefab) deathEffectPrefab = Resources.Load<GameObject>("Prefabs/FX/Death");

            BindButtons();

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

        private void BindButtons()
        {
            if (resumeButton && restartButton && mainMenuButton)
            {
                resumeButton.onClick.AddListener(HideMenu);
                restartButton.onClick.AddListener(RestartScene);
                mainMenuButton.onClick.AddListener(ReturnToMainMenu);
            }

            if (settingsButton)
            {
                settingsButton.onClick.AddListener(ShowSettings);
            }

            if (closeSettingsButton)
            {
                closeSettingsButton.onClick.AddListener(HideSettings);
            }
        }

        public static void TogglePause()
        {
            if (UpgradeUI.IsShowing) return;
            if (Instance == null) CreateRuntimeInstance();
            if (Instance._isDeathMenu) return;

            if (Instance._isVisible && !Instance._isDeathMenu)
                Instance.HideMenu();
            else
                Instance.ShowMenu(false);
        }

        public static void ShowDeathMenu(Vector3 deathPosition)
        {
            if (Instance == null) CreateRuntimeInstance();
            if (GameManager.I != null)
            {
                GameManager.I.SetSpawningEnabled(false, true);
            }
            Instance.PlayDeathFeedback(deathPosition);
            Instance.ShowMenu(true);
        }

        private void ShowMenu(bool isDeath)
        {
            MusicManager.Instance.SetPaused(true);

            _isVisible = true;
            _isDeathMenu = isDeath;
            _previousTimeScale = Time.timeScale;

            if (!isDeath)
            {
                Time.timeScale = 0f;
            }

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Confined;

            var customCursor = FindAnyObjectByType<CursorFollowUI>();
            if (customCursor)
            {
                customCursor.Show();
            }

            HideSettings();
            RefreshSettingsUI();

            if (isDeath)
            {
                RefreshDeathStats();
            }

            if (titleText) titleText.text = isDeath ? deathTitle : pauseTitle;
            if (resumeButton) resumeButton.gameObject.SetActive(!isDeath);

            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeCanvas(1f));
        }

        private void HideMenu()
        {
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);

            _isVisible = false;
            _isDeathMenu = false;
            MusicManager.Instance.SetPaused(false);
            HideSettings();
            _fadeRoutine = StartCoroutine(FadeCanvas(0f, RestoreTimeScale));
        }

        private void RestoreTimeScale()
        {
            // FIX: Check if Slow Motion is still active. 
            // If KillSlowMotion isn't active (meaning it finished while we were paused),
            // we should NOT revert to _previousTimeScale (which might be 0.45).
            // We should reset to 1.0f.

            float targetScale = Mathf.Approximately(_previousTimeScale, 0f) ? 1f : _previousTimeScale;

            if (KillSlowMotion.Instance != null && !KillSlowMotion.Instance.IsActive)
            {
                targetScale = 1f;
            }

            Time.timeScale = targetScale;

            KillSlowMotion.EnsureRestoredAfterPause();
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

        private void HideInstant()
        {
            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
            }

            _isVisible = false;
            _isDeathMenu = false;
            MusicManager.Instance.SetPaused(false);
            HideSettings();
            ApplyVisibility(0f, true);
            RestoreTimeScale();
        }

        private void ShowSettings()
        {
            if (settingsRoot)
            {
                settingsRoot.SetActive(true);
            }

            if (settingsGroup)
            {
                settingsGroup.alpha = 1f;
                settingsGroup.blocksRaycasts = true;
                settingsGroup.interactable = true;
            }

            RefreshSettingsUI();
        }

        private void HideSettings()
        {
            if (settingsGroup)
            {
                settingsGroup.alpha = 0f;
                settingsGroup.blocksRaycasts = false;
                settingsGroup.interactable = false;
            }

            if (settingsRoot)
            {
                settingsRoot.SetActive(false);
            }
        }

        private void RefreshSettingsUI()
        {
            if (musicSettingsUI)
            {
                musicSettingsUI.RefreshDisplay();
            }

            if (keybindSettingsUI)
            {
                keybindSettingsUI.RefreshDisplay();
            }

            if (videoSettingsUI)
            {
                videoSettingsUI.RefreshDisplay();
            }
        }

        private static void CreateRuntimeInstance()
        {
            var host = new GameObject("PauseMenuController");
            host.AddComponent<PauseMenuController>();
        }

        private void RestartScene()
        {
            CharacterProgressionRuntime.Instance?.TryFinalizeRun();
            if (sceneFlow == null) sceneFlow = FindAnyObjectByType<SceneFlowController>();
            StartCoroutine(LoadWithFade(() => sceneFlow?.ReloadCurrentScene()));
        }

        private void ReturnToMainMenu()
        {
            CharacterProgressionRuntime.Instance?.TryFinalizeRun();
            if (sceneFlow == null) sceneFlow = FindAnyObjectByType<SceneFlowController>();
            StartCoroutine(LoadWithFade(() => sceneFlow?.LoadMainMenuScene()));
        }

        private IEnumerator LoadWithFade(System.Action loadAction)
        {
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _isVisible = true;
            _fadeRoutine = StartCoroutine(FadeCanvas(1f));
            yield return new WaitForSecondsRealtime(fadeDuration);
            loadAction?.Invoke();
        }

        private void RefreshDeathStats()
        {
            int kills = GameManager.I != null ? GameManager.I.KillCount : 0;
            int bosses = GameManager.I != null ? GameManager.I.BossKillCount : 0;
            int crates = GameManager.I != null ? GameManager.I.CratesDestroyedCount : 0;
            float runTime = GameManager.I != null ? GameManager.I.RunTimeSeconds : 0f;
            RunStatsProgress.RecordRunTime(runTime);
            float longestTime = RunStatsProgress.LongestTimeSurvivedSeconds;
            string longestTimeLabel = longestTime > 0f ? FormatTime(longestTime) : unavailableStatText;
            if (roundKillsText)
            {
                roundKillsText.text = $"Kills: {kills}\nBosses: {bosses}";
            }

            int wave = GameManager.I != null ? GameManager.I.Wave : 0;
            if (lastWaveText)
            {
                lastWaveText.text = $"Last Wave: {Mathf.Max(0, wave)}\nCrates: {crates}";
            }

            string weaponLabel = unavailableStatText;
            if (UpgradeManager.I != null)
            {
                Weapon topWeapon = UpgradeManager.I.GetMostUsedWeapon(out int weaponKills);
                if (topWeapon != null)
                {
                    weaponLabel = weaponKills > 0
                        ? $"{topWeapon.weaponName} ({weaponKills} kills)"
                        : topWeapon.weaponName;
                }
            }

            if (mostUsedWeaponText)
            {
                mostUsedWeaponText.text = $"Most Used: {weaponLabel}\nLongest Time: {longestTimeLabel}";
            }
        }

        private static string FormatTime(float seconds)
        {
            if (seconds <= 0f)
            {
                return "0:00";
            }

            TimeSpan span = TimeSpan.FromSeconds(seconds);
            if (span.TotalHours >= 1)
            {
                return $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}";
            }

            return $"{span.Minutes}:{span.Seconds:00}";
        }

        private void PlayDeathFeedback(Vector3 position)
        {
            if (deathEffectPrefab) PoolManager.Get(deathEffectPrefab, position, Quaternion.identity);
            if (deathClip) AudioPlaybackPool.PlayOneShot(deathClip, position);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_isVisible && !_isDeathMenu && _fadeRoutine == null)
            {
                return;
            }

            HideInstant();
        }
    }
}
