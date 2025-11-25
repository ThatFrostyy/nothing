using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace FF
{
    public class GameHUD : MonoBehaviour
    {
        [Header("Data Sources")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private Health playerHealth;
        [SerializeField] private XPWallet wallet;
        [SerializeField] private WeaponManager weaponManager;
        [SerializeField] private UpgradeManager upgradeManager;

        [Header("UI References")]
        [SerializeField] private TMP_Text healthValueText;
        [SerializeField] private Image healthFillImage;
        [SerializeField] private TMP_Text killCountText;
        [SerializeField] private TMP_Text weaponNameText;
        [SerializeField] private TMP_Text waveText;
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text xpText;
        [SerializeField] private Image xpFillImage;
        [SerializeField] private TMP_Text upgradePromptText;
        [SerializeField] private CanvasGroup upgradePromptGroup;
        [SerializeField] private string upgradeKeyLabel = "Tab";

        [SerializeField] private string timeFormat = "mm\\:ss";

        [Header("UI Animation")]
        [SerializeField] private RectTransform healthPulseTarget;
        [SerializeField] private RectTransform xpPulseTarget;
        [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.25f;
        [SerializeField, Min(0f)] private float healthFillSpeed = 2f;
        [SerializeField, Min(0f)] private float xpFillSpeed = 2.5f;
        [SerializeField, Min(0f)] private float healthPulseSpeed = 6f;
        [SerializeField, Range(0f, 1f)] private float healthPulseAmplitude = 0.09f;
        [SerializeField, Min(0f)] private float xpPulseSpeed = 10f;
        [SerializeField, Range(0f, 1f)] private float xpPulseAmplitude = 0.06f;
        [SerializeField, Min(0f)] private float lowHealthHeartbeatDuration = 4f;
        [SerializeField, Min(0f)] private float upgradePromptFadeDuration = 0.2f;
        [SerializeField] private float upgradePromptVisibleTime = 1.5f;

        [Header("Wave Banner")]
        [SerializeField] private TMP_Text waveBannerText;
        [SerializeField] private CanvasGroup waveBannerGroup;
        [SerializeField, Min(0f)] private float waveBannerDuration = 2.5f;
        [SerializeField, Min(0f)] private float waveBannerFadeTime = 0.5f;

        [Header("Wave Effects")]
        [SerializeField] private Image waveFlashImage;
        [SerializeField] private Color waveFlashColor = new(1f, 0f, 0f, 0.45f);
        [SerializeField, Min(0f)] private float waveFlashDuration = 0.6f;
        [SerializeField] private AnimationCurve waveFlashAlphaCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [SerializeField] private AudioClip waveMilestoneClip;
        [SerializeField, Min(1)] private int waveMilestoneInterval = 5;
        [SerializeField, Min(1)] private int waveMilestoneStartingWave = 5;

        [Header("Audio")]
        [SerializeField] private AudioClip waveStartClip;
        [SerializeField] private AudioClip heartbeatClip;
        [SerializeField] private AudioClip xpFillLoopClip;

        [Header("Weapon Hotbar")]
        [SerializeField] private Image[] weaponSlotIcons = new Image[3];
        [SerializeField] private Image[] weaponSlotHighlights = new Image[3];
        [SerializeField] private Sprite emptyWeaponIcon;
        [SerializeField] private Color emptyWeaponColor = new(1f, 1f, 1f, 0.25f);
        [SerializeField] private Color activeSlotHighlight = Color.white;
        [SerializeField] private Color inactiveSlotHighlight = new(1f, 1f, 1f, 0.35f);
        [SerializeField, Min(0f)] private float weaponNameFadeInDuration = 0.2f;
        [SerializeField, Min(0f)] private float weaponNameHoldDuration = 1.2f;
        [SerializeField, Min(0f)] private float weaponNameFadeOutDuration = 0.35f;
        [SerializeField, Min(0f)] private float slotScaleAnimationTime = 0.12f;
        [SerializeField, Min(1f)] private float slotSelectedScale = 1.12f;

        private AudioSource uiAudioSource;
        private AudioSource heartbeatSource;
        private AudioSource xpFillSource;

        private float healthFillTarget;
        private float healthFillCurrent;
        private float healthPulseTimer;
        private bool lowHealthPulseActive;
        private float lowHealthHeartbeatTimer;

        private float xpFillTarget;
        private float xpFillCurrent;
        private float xpPulseTimer;
        private bool xpIsFilling;
        private int pendingUpgrades;

        private float waveBannerTimer;
        private float waveFlashElapsed = float.PositiveInfinity;
        private float waveFlashBaseAlpha = 1f;

        private Coroutine upgradePromptFadeRoutine;
        private float upgradePromptTimer = 0f;
        private bool upgradeMenuVisible;

        private Vector3 healthPulseBaseScale = Vector3.one;
        private Vector3 xpPulseBaseScale = Vector3.one;
        private Vector2 healthPulseBaseAnchoredPosition = Vector2.zero;
        private Vector2 xpPulseBaseAnchoredPosition = Vector2.zero;
        private Vector3 healthPulseScaleVelocity = Vector3.zero;
        private Vector3 xpPulseScaleVelocity = Vector3.zero;
        private Color weaponNameBaseColor = Color.white;
        private Coroutine weaponNameRoutine;
        private Vector3[] slotBaseScales = Array.Empty<Vector3>();
        private Coroutine[] slotScaleRoutines = Array.Empty<Coroutine>();

        void Awake()
        {
            if (!gameManager)
            {
                gameManager = GameManager.I;
            }

            if (!playerHealth)
            {
                var playerObject = GameObject.FindWithTag("Player");
                if (playerObject)
                {
                    playerHealth = playerObject.GetComponent<Health>();
                    if (!weaponManager)
                    {
                        weaponManager = playerObject.GetComponentInChildren<WeaponManager>();
                    }
                }
            }

            if (!wallet && playerHealth)
            {
                wallet = playerHealth.GetComponent<XPWallet>();
            }

            if (!weaponManager && playerHealth)
            {
                weaponManager = playerHealth.GetComponent<WeaponManager>();
            }

            if (!upgradeManager)
            {
                upgradeManager = FindFirstObjectByType<UpgradeManager>();
            }

            if (!upgradePromptGroup && upgradePromptText)
            {
                upgradePromptGroup = upgradePromptText.GetComponent<CanvasGroup>();
                if (!upgradePromptGroup)
                {
                    upgradePromptGroup = upgradePromptText.gameObject.AddComponent<CanvasGroup>();
                }
            }

            healthPulseTarget = ResolveHealthPulseTarget();

            if (!xpPulseTarget && xpFillImage)
            {
                xpPulseTarget = xpFillImage.rectTransform;
            }

            healthPulseBaseScale = healthPulseTarget ? healthPulseTarget.localScale : Vector3.one;
            xpPulseBaseScale = xpPulseTarget ? xpPulseTarget.localScale : Vector3.one;
            healthPulseBaseAnchoredPosition = healthPulseTarget ? healthPulseTarget.anchoredPosition : Vector2.zero;
            xpPulseBaseAnchoredPosition = xpPulseTarget ? xpPulseTarget.anchoredPosition : Vector2.zero;

            if (weaponNameText)
            {
                weaponNameBaseColor = weaponNameText.color;
            }

            CacheSlotBaseScales();

            healthFillCurrent = healthFillImage ? healthFillImage.fillAmount : 0f;
            healthFillTarget = healthFillCurrent;

            xpFillCurrent = xpFillImage ? xpFillImage.fillAmount : 0f;
            xpFillTarget = xpFillCurrent;

            InitializeAudio();
            SetWaveBannerVisible(0f);
            InitializeWaveFlash();
            RefreshUpgradePrompt();
            BindSceneManagers();
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;

            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged += HandleHealthChanged;
            }

            if (wallet != null)
            {
                wallet.OnXPChanged += HandleXPChanged;
            }

            if (weaponManager != null)
            {
                weaponManager.OnWeaponEquipped += HandleWeaponEquipped;
                weaponManager.OnInventoryChanged += HandleWeaponInventoryChanged;
            }

            BindSceneManagers();

            UpgradeUI.OnVisibilityChanged += HandleUpgradeVisibilityChanged;
            upgradeMenuVisible = UpgradeUI.IsShowing;

            RefreshAll();
            SyncFillImmediately();
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged -= HandleHealthChanged;
            }

            if (wallet != null)
            {
                wallet.OnXPChanged -= HandleXPChanged;
            }

            if (weaponManager != null)
            {
                weaponManager.OnWeaponEquipped -= HandleWeaponEquipped;
                weaponManager.OnInventoryChanged -= HandleWeaponInventoryChanged;
            }

            UnbindSceneManagers();

            UpgradeUI.OnVisibilityChanged -= HandleUpgradeVisibilityChanged;
            upgradeMenuVisible = false;

            SetHeartbeatActive(false);
            SetXPFillSoundActive(false);
            lowHealthPulseActive = false;
            xpIsFilling = false;
            if (upgradePromptFadeRoutine != null)
            {
                StopCoroutine(upgradePromptFadeRoutine);
                upgradePromptFadeRoutine = null;
            }
            lowHealthHeartbeatTimer = 0f;
            ResetHealthPulse();
            ResetXPPulse();
            waveBannerTimer = 0f;
            SetWaveBannerVisible(0f);
            waveFlashElapsed = float.PositiveInfinity;
            SetWaveFlashAlpha(0f);
            RefreshUpgradePrompt();
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            BindSceneManagers();
            RefreshAll();
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;
            float unscaledDeltaTime = Time.unscaledDeltaTime;

            if (upgradePromptTimer > 0f)
            {
                upgradePromptTimer -= Time.deltaTime;

                if (upgradePromptTimer <= 0f)
                {
                    StartUpgradePromptFade(0f);
                }
            }

            UpdateWaveDisplay();
            UpdateTimeDisplay();
            UpdateHealthFill(deltaTime);
            UpdateXPFill(deltaTime);
            UpdateHealthPulse(deltaTime);
            UpdateXPPulse(deltaTime);
            UpdateHeartbeatTimer(unscaledDeltaTime);
            UpdateWaveBanner(unscaledDeltaTime);
            UpdateWaveFlash(unscaledDeltaTime);
        }

        void BindSceneManagers()
        {
            RefreshGameManagerReference();
            RefreshUpgradeManagerReference();
        }

        void UnbindSceneManagers()
        {
            if (gameManager != null)
            {
                gameManager.OnKillCountChanged -= HandleKillCountChanged;
                gameManager.OnWaveStarted -= HandleWaveStarted;
            }

            if (upgradeManager != null)
            {
                upgradeManager.OnPendingUpgradesChanged -= HandlePendingUpgradesChanged;
            }
        }

        void RefreshGameManagerReference()
        {
            GameManager resolved = gameManager ? gameManager : GameManager.I;
            if (resolved == gameManager)
            {
                return;
            }

            if (gameManager != null)
            {
                gameManager.OnKillCountChanged -= HandleKillCountChanged;
                gameManager.OnWaveStarted -= HandleWaveStarted;
            }

            gameManager = resolved;
            if (gameManager != null && isActiveAndEnabled)
            {
                gameManager.OnKillCountChanged += HandleKillCountChanged;
                gameManager.OnWaveStarted += HandleWaveStarted;
                HandleKillCountChanged(gameManager.KillCount);
            }
        }

        void RefreshUpgradeManagerReference()
        {
            UpgradeManager resolved = upgradeManager ? upgradeManager : UpgradeManager.I;
            if (resolved == upgradeManager)
            {
                return;
            }

            if (upgradeManager != null)
            {
                upgradeManager.OnPendingUpgradesChanged -= HandlePendingUpgradesChanged;
            }

            upgradeManager = resolved;
            if (upgradeManager != null && isActiveAndEnabled)
            {
                upgradeManager.OnPendingUpgradesChanged += HandlePendingUpgradesChanged;
                HandlePendingUpgradesChanged(upgradeManager.GetPendingUpgradeCount());
            }
        }

        void RefreshAll()
        {
            HandleHealthChanged(playerHealth ? playerHealth.CurrentHP : 0, playerHealth ? playerHealth.MaxHP : 0);
            HandleXPChanged(wallet ? wallet.Level : 1, wallet ? wallet.XP : 0, wallet ? wallet.Next : 1);
            HandleKillCountChanged(gameManager ? gameManager.KillCount : 0);
            UpdateWeaponDisplay();
            UpdateWaveDisplay();
            UpdateTimeDisplay();
            RefreshUpgradePrompt();
        }

        void HandleHealthChanged(int current, int max)
        {
            if (healthValueText)
            {
                if (max <= 0)
                {
                    healthValueText.text = "HP: --";
                }
                else
                {
                    int clamped = Mathf.Clamp(current, 0, max);
                    healthValueText.text = $"HP: {clamped}/{max}";
                }
            }

            if (!healthFillImage)
            {
                return;
            }

            if (max <= 0)
            {
                healthFillTarget = 0f;
            }
            else
            {
                int clamped = Mathf.Clamp(current, 0, max);
                healthFillTarget = Mathf.Clamp01((float)clamped / max);
            }

            bool wasPulsing = lowHealthPulseActive;
            bool shouldPulse = max > 0 && healthFillTarget <= lowHealthThreshold;
            lowHealthPulseActive = shouldPulse;

            if (!shouldPulse)
            {
                ResetHealthPulse();
                lowHealthHeartbeatTimer = 0f;
                SetHeartbeatActive(false);
            }
            else if (!wasPulsing)
            {
                lowHealthHeartbeatTimer = lowHealthHeartbeatDuration;
                SetHeartbeatActive(true);
            }
        }

        void HandleXPChanged(int level, int current, int next)
        {
            if (xpText)
            {
                xpText.text = $"LVL: {Mathf.Max(1, level)} – XP: {Mathf.Max(0, current)}/{Mathf.Max(1, next)}";
            }

            if (!xpFillImage)
            {
                return;
            }

            float target = next <= 0 ? 0f : Mathf.Clamp01((float)Mathf.Max(0, current) / Mathf.Max(1, next));
            if (target < xpFillTarget)
            {
                xpFillCurrent = target;
            }

            xpFillTarget = target;
        }

        void HandlePendingUpgradesChanged(int pending)
        {
            pendingUpgrades = Mathf.Max(0, pending);

            if (pendingUpgrades > 0)
            {
                upgradePromptTimer = upgradePromptVisibleTime;

                if (!upgradeMenuVisible)
                {
                    StartUpgradePromptFade(1f);
                }
            }
            else
            {
                upgradePromptTimer = 0f;
                StartUpgradePromptFade(0f);
            }

            RefreshUpgradePrompt();
        }

        void HandleKillCountChanged(int kills)
        {
            if (!killCountText)
            {
                return;
            }

            killCountText.text = $"{Mathf.Max(0, kills)}";
        }

        void HandleWeaponEquipped(Weapon weapon)
        {
            UpdateWeaponDisplay(weapon);
        }

        void HandleWeaponInventoryChanged()
        {
            UpdateWeaponHotbar();
        }

        void HandleWaveStarted(int wave)
        {
            int displayWave = Mathf.Max(1, wave);
            if (waveBannerText)
            {
                waveBannerText.text = $"Wave {displayWave}";
            }

            waveBannerTimer = waveBannerDuration;
            SetWaveBannerVisible(1f);
            TriggerWaveFlash();
            PlayWaveStartSound(wave);
        }

        void HandleUpgradeVisibilityChanged(bool isVisible)
        {
            upgradeMenuVisible = isVisible;

            if (isVisible)
            {
                upgradePromptTimer = 0f;
                StartUpgradePromptFade(0f, true);
            }
            else if (pendingUpgrades > 0)
            {
                upgradePromptTimer = upgradePromptVisibleTime;
                StartUpgradePromptFade(1f);
            }

            RefreshUpgradePrompt();
        }

        void RefreshUpgradePrompt()
        {
            if (!upgradePromptText) return;

            if (pendingUpgrades > 0)
            {
                upgradePromptText.text = $"Press {upgradeKeyLabel} to upgrade! ({pendingUpgrades} left)";
            }
            else
            {
                upgradePromptText.text = string.Empty;
            }
        }

        void StartUpgradePromptFade(float targetAlpha, bool instant = false)
        {
            if (upgradePromptFadeRoutine != null)
            {
                StopCoroutine(upgradePromptFadeRoutine);
            }

            if (targetAlpha > 0f && upgradePromptText && !upgradePromptText.gameObject.activeSelf)
            {
                upgradePromptText.gameObject.SetActive(true);
            }

            if (instant || targetAlpha <= 0f)
            {
                ApplyUpgradePromptVisibility(targetAlpha);
                upgradePromptFadeRoutine = null;
                return;
            }

            upgradePromptFadeRoutine = StartCoroutine(FadeUpgradePrompt(targetAlpha));
        }

        System.Collections.IEnumerator FadeUpgradePrompt(float targetAlpha)
        {
            if (!upgradePromptGroup)
            {
                yield break;
            }

            float duration = Mathf.Max(0.01f, upgradePromptFadeDuration);
            float startAlpha = upgradePromptGroup.alpha;
            float elapsed = 0f;

            upgradePromptGroup.gameObject.SetActive(true);
            upgradePromptGroup.interactable = true;
            upgradePromptGroup.blocksRaycasts = true;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                upgradePromptGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            upgradePromptGroup.alpha = targetAlpha;
            ApplyUpgradePromptVisibility(targetAlpha);

            upgradePromptFadeRoutine = null;
        }

        void ApplyUpgradePromptVisibility(float targetAlpha)
        {
            if (!upgradePromptGroup)
            {
                return;
            }

            bool isVisible = targetAlpha > 0f;
            upgradePromptGroup.alpha = Mathf.Max(0f, targetAlpha);
            upgradePromptGroup.interactable = isVisible;
            upgradePromptGroup.blocksRaycasts = isVisible;
            upgradePromptGroup.gameObject.SetActive(isVisible);

            if (!isVisible && upgradePromptText)
            {
                upgradePromptText.gameObject.SetActive(false);
            }
        }

        void UpdateWeaponDisplay(Weapon weaponOverride = null)
        {
            Weapon weaponToShow = weaponOverride ? weaponOverride : weaponManager ? weaponManager.CurrentWeapon : null;
            string weaponLabel = weaponToShow && !string.IsNullOrEmpty(weaponToShow.weaponName)
                ? weaponToShow.weaponName
                : "--";

            if (weaponNameText)
            {
                StartWeaponNameFade(weaponLabel);
            }

            UpdateWeaponHotbar();
        }

        void UpdateWeaponHotbar()
        {
            int slotVisualCount = Mathf.Max(
                weaponSlotIcons != null ? weaponSlotIcons.Length : 0,
                weaponSlotHighlights != null ? weaponSlotHighlights.Length : 0
            );

            if (slotVisualCount == 0)
            {
                return;
            }

            for (int i = 0; i < slotVisualCount; i++)
            {
                Weapon slotWeapon = weaponManager && i < weaponManager.SlotCount
                    ? weaponManager.GetWeaponInSlot(i)
                    : null;

                if (weaponSlotIcons != null && i < weaponSlotIcons.Length && weaponSlotIcons[i])
                {
                    Sprite slotSprite = slotWeapon && slotWeapon.weaponIcon ? slotWeapon.weaponIcon : emptyWeaponIcon;
                    weaponSlotIcons[i].sprite = slotSprite;
                    weaponSlotIcons[i].color = slotWeapon ? Color.white : emptyWeaponColor;
                }

                if (weaponSlotHighlights != null && i < weaponSlotHighlights.Length && weaponSlotHighlights[i])
                {
                    bool isSelected = weaponManager && weaponManager.CurrentSlotIndex == i;
                    weaponSlotHighlights[i].color = isSelected ? activeSlotHighlight : inactiveSlotHighlight;
                    AnimateSlotScale(i, isSelected);
                }
            }
        }

        void StartWeaponNameFade(string label)
        {
            if (!weaponNameText)
            {
                return;
            }

            if (weaponNameRoutine != null)
            {
                StopCoroutine(weaponNameRoutine);
            }

            weaponNameRoutine = StartCoroutine(WeaponNameFadeRoutine(label));
        }

        System.Collections.IEnumerator WeaponNameFadeRoutine(string label)
        {
            weaponNameText.text = label;

            Color startColor = weaponNameBaseColor;
            startColor.a = 0f;
            weaponNameText.color = startColor;

            float elapsed = 0f;
            while (elapsed < weaponNameFadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = weaponNameFadeInDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / weaponNameFadeInDuration);
                weaponNameText.color = Color.Lerp(startColor, weaponNameBaseColor, t);
                yield return null;
            }

            weaponNameText.color = weaponNameBaseColor;

            if (weaponNameHoldDuration > 0f)
            {
                yield return new WaitForSeconds(weaponNameHoldDuration);
            }

            elapsed = 0f;
            Color targetColor = weaponNameBaseColor;
            targetColor.a = 0f;
            while (elapsed < weaponNameFadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = weaponNameFadeOutDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / weaponNameFadeOutDuration);
                weaponNameText.color = Color.Lerp(weaponNameBaseColor, targetColor, t);
                yield return null;
            }

            weaponNameText.color = targetColor;
            weaponNameRoutine = null;
        }

        void CacheSlotBaseScales()
        {
            int slotVisualCount = Mathf.Max(
                weaponSlotIcons != null ? weaponSlotIcons.Length : 0,
                weaponSlotHighlights != null ? weaponSlotHighlights.Length : 0
            );

            slotBaseScales = new Vector3[slotVisualCount];
            slotScaleRoutines = new Coroutine[slotVisualCount];

            for (int i = 0; i < slotVisualCount; i++)
            {
                Transform slotTransform = ResolveSlotTransform(i);
                slotBaseScales[i] = slotTransform ? slotTransform.localScale : Vector3.one;
            }
        }

        Transform ResolveSlotTransform(int index)
        {
            if (index < 0)
            {
                return null;
            }

            if (weaponSlotHighlights != null && index < weaponSlotHighlights.Length && weaponSlotHighlights[index])
            {
                return weaponSlotHighlights[index].rectTransform;
            }

            if (weaponSlotIcons != null && index < weaponSlotIcons.Length && weaponSlotIcons[index])
            {
                return weaponSlotIcons[index].rectTransform;
            }

            return null;
        }

        void AnimateSlotScale(int index, bool isSelected)
        {
            if (index < 0 || index >= slotBaseScales.Length)
            {
                return;
            }

            Transform targetTransform = ResolveSlotTransform(index);
            if (!targetTransform)
            {
                return;
            }

            if (slotScaleRoutines[index] != null)
            {
                StopCoroutine(slotScaleRoutines[index]);
            }

            Vector3 baseScale = slotBaseScales[index];
            Vector3 targetScale = isSelected ? baseScale * slotSelectedScale : baseScale;
            slotScaleRoutines[index] = StartCoroutine(SlotScaleRoutine(targetTransform, targetScale, index));
        }

        System.Collections.IEnumerator SlotScaleRoutine(Transform target, Vector3 targetScale, int index)
        {
            Vector3 startScale = target.localScale;
            float elapsed = 0f;

            while (elapsed < slotScaleAnimationTime)
            {
                elapsed += Time.deltaTime;
                float t = slotScaleAnimationTime <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / slotScaleAnimationTime));
                target.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }

            target.localScale = targetScale;
            slotScaleRoutines[index] = null;
        }

        void UpdateWaveDisplay()
        {
            if (!waveText)
            {
                return;
            }

            int wave = gameManager ? gameManager.Wave : 0;
            waveText.text = $"Wave: {Mathf.Max(0, wave)}";
        }

        void UpdateTimeDisplay()
        {
            if (!timeText)
            {
                return;
            }

            TimeSpan span = TimeSpan.FromSeconds(Time.timeSinceLevelLoad);
            timeText.text = $"{span.ToString(timeFormat)}";
        }

        void UpdateHealthFill(float deltaTime)
        {
            if (!healthFillImage)
            {
                return;
            }

            healthFillCurrent = Mathf.MoveTowards(healthFillCurrent, healthFillTarget, healthFillSpeed * deltaTime);
            healthFillImage.fillAmount = healthFillCurrent;
        }

        void UpdateXPFill(float deltaTime)
        {
            if (!xpFillImage)
            {
                return;
            }

            bool wasFilling = xpIsFilling;

            xpFillCurrent = Mathf.MoveTowards(xpFillCurrent, xpFillTarget, xpFillSpeed * deltaTime);
            xpFillImage.fillAmount = xpFillCurrent;

            xpIsFilling = xpFillCurrent < xpFillTarget - 0.001f;

            if (!wasFilling && xpIsFilling)
            {
                xpPulseTimer = 0f;
            }
            else if (wasFilling && !xpIsFilling)
            {
                ResetXPPulse();
            }

            SetXPFillSoundActive(xpIsFilling);
        }

        void UpdateHealthPulse(float deltaTime)
        {
            if (!healthPulseTarget)
            {
                return;
            }

            if (lowHealthPulseActive)
            {
                healthPulseTimer += deltaTime * healthPulseSpeed;
                float sine = (Mathf.Sin(healthPulseTimer) + 1f) * 0.5f;
                float pulseScale = Mathf.Lerp(1f - healthPulseAmplitude, 1f + healthPulseAmplitude, sine);
                Vector3 targetScale = new Vector3(
                    healthPulseBaseScale.x * pulseScale,
                    healthPulseBaseScale.y * pulseScale,
                    healthPulseBaseScale.z
                );
                healthPulseTarget.localScale = Vector3.SmoothDamp(
                    healthPulseTarget.localScale,
                    targetScale,
                    ref healthPulseScaleVelocity,
                    0.08f,
                    Mathf.Infinity,
                    deltaTime
                );
            }
            else
            {
                healthPulseTarget.localScale = Vector3.SmoothDamp(
                    healthPulseTarget.localScale,
                    healthPulseBaseScale,
                    ref healthPulseScaleVelocity,
                    0.08f,
                    Mathf.Infinity,
                    deltaTime
                );
            }

            healthPulseTarget.anchoredPosition = healthPulseBaseAnchoredPosition;
        }

        void UpdateXPPulse(float deltaTime)
        {
            if (!xpPulseTarget)
            {
                return;
            }

            if (xpIsFilling)
            {
                xpPulseTimer += deltaTime * xpPulseSpeed;
                float pulseScale = 1f + Mathf.Sin(xpPulseTimer) * xpPulseAmplitude;
                Vector3 targetScale = new Vector3(
                    xpPulseBaseScale.x * pulseScale,
                    xpPulseBaseScale.y * pulseScale,
                    xpPulseBaseScale.z
                );
                xpPulseTarget.localScale = Vector3.SmoothDamp(
                    xpPulseTarget.localScale,
                    targetScale,
                    ref xpPulseScaleVelocity,
                    0.06f,
                    Mathf.Infinity,
                    deltaTime
                );
            }
            else
            {
                xpPulseTarget.localScale = Vector3.SmoothDamp(
                    xpPulseTarget.localScale,
                    xpPulseBaseScale,
                    ref xpPulseScaleVelocity,
                    0.06f,
                    Mathf.Infinity,
                    deltaTime
                );
            }

            xpPulseTarget.anchoredPosition = xpPulseBaseAnchoredPosition;
        }

        void UpdateHeartbeatTimer(float deltaTime)
        {
            if (lowHealthHeartbeatTimer <= 0f)
            {
                return;
            }

            lowHealthHeartbeatTimer = Mathf.Max(0f, lowHealthHeartbeatTimer - deltaTime);
            if (lowHealthHeartbeatTimer <= 0f)
            {
                SetHeartbeatActive(false);
            }
        }

        void UpdateWaveBanner(float deltaTime)
        {
            if (!waveBannerText && !waveBannerGroup)
            {
                return;
            }

            if (waveBannerTimer > 0f)
            {
                waveBannerTimer = Mathf.Max(0f, waveBannerTimer - deltaTime);

                float alpha = 1f;
                if (waveBannerTimer <= waveBannerFadeTime && waveBannerFadeTime > 0f)
                {
                    alpha = Mathf.Clamp01(waveBannerTimer / waveBannerFadeTime);
                }

                SetWaveBannerVisible(alpha);
            }
            else
            {
                SetWaveBannerVisible(0f);
            }
        }

        void InitializeWaveFlash()
        {
            waveFlashBaseAlpha = Mathf.Clamp01(waveFlashColor.a);
            waveFlashElapsed = float.PositiveInfinity;
            SetWaveFlashAlpha(0f);
        }

        void TriggerWaveFlash()
        {
            if (!waveFlashImage || waveFlashDuration <= 0f)
            {
                return;
            }

            waveFlashElapsed = 0f;
            SetWaveFlashAlpha(EvaluateWaveFlashAlpha(0f));
        }

        void UpdateWaveFlash(float deltaTime)
        {
            if (!waveFlashImage || waveFlashDuration <= 0f || float.IsPositiveInfinity(waveFlashElapsed))
            {
                return;
            }

            waveFlashElapsed += deltaTime;

            if (waveFlashElapsed >= waveFlashDuration)
            {
                waveFlashElapsed = float.PositiveInfinity;
                SetWaveFlashAlpha(0f);
                return;
            }

            float normalized = Mathf.Clamp01(waveFlashElapsed / Mathf.Max(0.0001f, waveFlashDuration));
            SetWaveFlashAlpha(EvaluateWaveFlashAlpha(normalized));
        }

        float EvaluateWaveFlashAlpha(float normalizedTime)
        {
            normalizedTime = Mathf.Clamp01(normalizedTime);

            if (waveFlashAlphaCurve != null && waveFlashAlphaCurve.length > 0)
            {
                return Mathf.Clamp01(waveFlashAlphaCurve.Evaluate(normalizedTime));
            }

            return 1f - normalizedTime;
        }

        void SetWaveFlashAlpha(float normalizedAlpha)
        {
            if (!waveFlashImage)
            {
                return;
            }

            float alpha = Mathf.Clamp01(normalizedAlpha) * waveFlashBaseAlpha;
            Color color = waveFlashColor;
            color.a = alpha;
            waveFlashImage.color = color;
        }

        void InitializeAudio()
        {
            if (!uiAudioSource)
            {
                uiAudioSource = GetComponent<AudioSource>();
            }

            if (!uiAudioSource)
            {
                uiAudioSource = gameObject.AddComponent<AudioSource>();
            }

            uiAudioSource.playOnAwake = false;
            uiAudioSource.loop = false;
            uiAudioSource.spatialBlend = 0f;
            uiAudioSource.ignoreListenerPause = true;

            if (!heartbeatSource)
            {
                heartbeatSource = gameObject.AddComponent<AudioSource>();
            }

            heartbeatSource.playOnAwake = false;
            heartbeatSource.loop = true;
            heartbeatSource.spatialBlend = 0f;
            heartbeatSource.ignoreListenerPause = true;

            if (!xpFillSource)
            {
                xpFillSource = gameObject.AddComponent<AudioSource>();
            }

            xpFillSource.playOnAwake = false;
            xpFillSource.loop = true;
            xpFillSource.spatialBlend = 0f;
            xpFillSource.ignoreListenerPause = true;
        }

        void SetHeartbeatActive(bool active)
        {
            if (!heartbeatSource || !heartbeatClip)
            {
                return;
            }


            if (active)
            {
                if (!heartbeatSource.isPlaying)
                {
                    heartbeatSource.clip = heartbeatClip;
                    heartbeatSource.Play();
                }
            }
            else if (heartbeatSource.isPlaying)
            {
                heartbeatSource.Stop();
            }
        }

        void SetXPFillSoundActive(bool active)
        {
            if (!xpFillSource || !xpFillLoopClip)
            {
                return;
            }


            if (active)
            {
                if (!xpFillSource.isPlaying)
                {
                    xpFillSource.clip = xpFillLoopClip;
                    xpFillSource.Play();
                }
            }
            else if (xpFillSource.isPlaying)
            {
                xpFillSource.Stop();
            }
        }

        void PlayWaveStartSound(int wave)
        {
            if (uiAudioSource && waveStartClip)
            {
                uiAudioSource.PlayOneShot(waveStartClip);
            }

            if (!uiAudioSource || !waveMilestoneClip)
            {
                return;
            }

            int startWave = Mathf.Max(1, waveMilestoneStartingWave);
            int interval = Mathf.Max(1, waveMilestoneInterval);

            if (wave < startWave)
            {
                return;
            }

            if ((wave - startWave) % interval == 0)
            {
                uiAudioSource.PlayOneShot(waveMilestoneClip);
            }
        }

        void ResetHealthPulse()
        {
            healthPulseTimer = 0f;
            healthPulseScaleVelocity = Vector3.zero;
            if (healthPulseTarget)
            {
                healthPulseTarget.localScale = healthPulseBaseScale;
                healthPulseTarget.anchoredPosition = healthPulseBaseAnchoredPosition;
            }
        }

        void ResetXPPulse()
        {
            xpPulseTimer = 0f;
            xpPulseScaleVelocity = Vector3.zero;
            if (xpPulseTarget)
            {
                xpPulseTarget.localScale = xpPulseBaseScale;
                xpPulseTarget.anchoredPosition = xpPulseBaseAnchoredPosition;
            }
        }

        void SyncFillImmediately()
        {
            if (healthFillImage)
            {
                healthFillCurrent = healthFillTarget;
                healthFillImage.fillAmount = healthFillCurrent;
            }

            if (xpFillImage)
            {
                xpFillCurrent = xpFillTarget;
                xpFillImage.fillAmount = xpFillCurrent;
            }
        }

        void SetWaveBannerVisible(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);

            if (waveBannerGroup)
            {
                waveBannerGroup.alpha = alpha;
            }
            else if (waveBannerText)
            {
                Color color = waveBannerText.color;
                color.a = alpha;
                waveBannerText.color = color;
            }
        }

        void OnValidate()
        {
            lowHealthThreshold = Mathf.Clamp01(lowHealthThreshold);
            healthPulseAmplitude = Mathf.Clamp01(healthPulseAmplitude);
            xpPulseAmplitude = Mathf.Clamp01(xpPulseAmplitude);
            waveFlashDuration = Mathf.Max(0f, waveFlashDuration);
            waveMilestoneInterval = Mathf.Max(1, waveMilestoneInterval);
            waveMilestoneStartingWave = Mathf.Max(1, waveMilestoneStartingWave);
            waveFlashBaseAlpha = Mathf.Clamp01(waveFlashColor.a);
            waveFlashElapsed = float.PositiveInfinity;
            if (waveFlashImage)
            {
                SetWaveFlashAlpha(0f);
            }

            healthPulseTarget = ResolveHealthPulseTarget();

            if (!xpPulseTarget && xpFillImage)
            {
                xpPulseTarget = xpFillImage.rectTransform;
            }
        }

        RectTransform ResolveHealthPulseTarget()
        {
            if (healthPulseTarget)
            {
                return healthPulseTarget;
            }

            if (!healthFillImage)
            {
                return null;
            }

            RectTransform fillRect = healthFillImage.rectTransform;
            if (fillRect != null)
            {
                RectTransform parentRect = fillRect.parent as RectTransform;
                if (parentRect != null)
                {
                    return parentRect;
                }

                return fillRect;
            }

            return null;
        }
    }
}
