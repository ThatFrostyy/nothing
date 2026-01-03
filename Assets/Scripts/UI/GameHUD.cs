using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FF
{
    public class GameHUD : MonoBehaviour, ISceneReferenceHandler
    {
        public static GameHUD Instance { get; private set; }

        [Header("Data Sources")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private Health playerHealth;
        [SerializeField] private XPWallet wallet;
        [SerializeField] private WeaponManager weaponManager;
        [SerializeField] private UpgradeManager upgradeManager;
        [SerializeField] private CanvasGroup rootCanvasGroup;

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
        [SerializeField] private string interactKeyLabel = "E";
        [SerializeField] private InputActionReference upgradeAction;
        [SerializeField] private string upgradeBindingId = "d4d249ae-55f1-4a58-aef2-3913d718d7d7";
        [SerializeField] private InputActionReference interactAction;
        [SerializeField] private string upgradeActionName = "Upgrade";
        [SerializeField] private string interactActionName = "Interact";
        [SerializeField] private string interactBindingId = "1c04ea5f-b012-41d1-a6f7-02e963b52893";

        [SerializeField] private string timeFormat = "mm\\:ss";

        [Header("Upgrade Summary")]
        [SerializeField] private InputActionReference upgradeSummaryAction;
        [SerializeField] private string upgradeSummaryActionName = "UpgradeSummary";
        [SerializeField] private string upgradeSummaryBindingId = "";
        [SerializeField] private CanvasGroup upgradeSummaryGroup;
        [SerializeField] private TMP_Text upgradeSummaryTitleText;
        [SerializeField] private TMP_Text upgradeSummaryPlayerText;
        [SerializeField] private TMP_Text upgradeSummaryWeaponText;
        [SerializeField] private string upgradeSummaryTitle = "Upgrades";

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
        [SerializeField] private Color hitFlashColor = new(1f, 0f, 0f, 0.15f);
        [SerializeField, Min(0f)] private float waveFlashDuration = 0.6f;
        [SerializeField] private AnimationCurve waveFlashAlphaCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [SerializeField] private AudioClip waveMilestoneClip;
        [SerializeField, Min(1)] private int waveMilestoneInterval = 5;
        [SerializeField, Min(1)] private int waveMilestoneStartingWave = 5;

        [Header("Get Ready")]
        [SerializeField] private TMP_Text getReadyText;
        [SerializeField] private CanvasGroup getReadyGroup;
        [SerializeField, Min(0f)] private float getReadyFadeDuration = 0.75f;
        [SerializeField, Min(0f)] private float getReadyHoldDuration = 0.35f;

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
        private Color activeWaveFlashColor;

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
        private bool getReadyDismissed;
        private Coroutine getReadyRoutine;
        private bool hasNearbyPickups;
        private WeaponManager boundWeaponManager;
        private InputAction upgradeSummaryInput;
        private bool ownsUpgradeSummaryInput;
        private bool upgradeSummaryVisible;

        void Awake()
        {
            // Ensure a single persistent HUD instance so it is not destroyed
            // or reset when switching to the main menu. Keep references intact.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneReferenceRegistry.Register(this);

            if (!gameManager) gameManager = GameManager.I;

            // FIX: Aggressively prefer the Singleton for UpgradeManager
            if (UpgradeManager.I != null)
            {
                upgradeManager = UpgradeManager.I;
            }
            else if (!upgradeManager)
            {
                upgradeManager = FindFirstObjectByType<UpgradeManager>();
            }

            if (!playerHealth)
            {
                var playerObject = GameObject.FindWithTag("Player");
                if (playerObject)
                {
                    playerHealth = playerObject.GetComponent<Health>();
                    if (!weaponManager) weaponManager = playerObject.GetComponentInChildren<WeaponManager>();
                }
            }

            if (!wallet && playerHealth) wallet = playerHealth.GetComponent<XPWallet>();
            if (!weaponManager && playerHealth) weaponManager = playerHealth.GetComponent<WeaponManager>();

            if (!upgradePromptGroup && upgradePromptText)
            {
                upgradePromptGroup = upgradePromptText.GetComponent<CanvasGroup>();
                if (!upgradePromptGroup)
                {
                    upgradePromptGroup = upgradePromptText.gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (!rootCanvasGroup)
            {
                rootCanvasGroup = GetComponent<CanvasGroup>();
                if (!rootCanvasGroup)
                {
                    rootCanvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (!getReadyGroup && getReadyText)
            {
                getReadyGroup = getReadyText.GetComponent<CanvasGroup>();
                if (!getReadyGroup)
                {
                    getReadyGroup = getReadyText.gameObject.AddComponent<CanvasGroup>();
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

            if (weaponNameText) weaponNameBaseColor = weaponNameText.color;

            CacheSlotBaseScales();

            healthFillCurrent = healthFillImage ? healthFillImage.fillAmount : 0f;
            healthFillTarget = healthFillCurrent;
            xpFillCurrent = xpFillImage ? xpFillImage.fillAmount : 0f;
            xpFillTarget = xpFillCurrent;

            InitializeAudio();
            SetWaveBannerVisible(0f);
            InitializeGetReady();
            InitializeWaveFlash();
            RefreshUpgradePrompt();
            ApplyUpgradeSummaryVisibility(false);

            ApplySceneVisibility(SceneManager.GetActiveScene());
        }

        void OnDestroy()
        {
            SceneReferenceRegistry.Unregister(this);
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void ClearSceneReferences()
        {
            // Intentionally do NOT clear references here.
            // The HUD persists across scene loads and should retain its
            // references so it can restore itself when returning to gameplay.
            // SceneReferenceRegistry will still call this method during scene
            // transitions, but we avoid nulling out references to prevent the
            // HUD from losing its bindings and becoming unusable after menu
            // navigation.
            // (If a full reset is ever required, call RebindSceneReferences())
            return;
        }

        public void RebindSceneReferences()
        {
            RefreshGameManagerReference();
            RefreshUpgradeManagerReference();
        }

        private void Start()
        {
            BindSceneManagers();
            RefreshAll();
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            PlayerController.OnPlayerReady += HandlePlayerReady;

            EnsureActionReferences();
            InputBindingManager.Initialize(ResolveInputAsset());
            InputBindingManager.OnBindingsChanged += HandleBindingsChanged;
            RefreshKeybindLabels();
            BindUpgradeSummaryInput();

            if (gameManager != null)
            {
                gameManager.OnKillCountChanged += HandleKillCountChanged;
                gameManager.OnWaveStarted += HandleWaveStarted;
            }
            if (playerHealth != null) playerHealth.OnHealthChanged += HandleHealthChanged;
            if (playerHealth != null) playerHealth.OnDamaged += HandlePlayerDamaged;
            if (wallet != null) wallet.OnXPChanged += HandleXPChanged;

            // FIX: Ensure we are bound to the correct manager on enable//
            RefreshUpgradeManagerReference();
            BindWeaponManager(weaponManager);

            RefreshAll();
            SyncFillImmediately();
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            PlayerController.OnPlayerReady -= HandlePlayerReady;

            InputBindingManager.OnBindingsChanged -= HandleBindingsChanged;
            UnbindUpgradeSummaryInput();

            if (gameManager != null)
            {
                gameManager.OnKillCountChanged -= HandleKillCountChanged;
                gameManager.OnWaveStarted -= HandleWaveStarted;
            }
            if (playerHealth != null) playerHealth.OnHealthChanged -= HandleHealthChanged;
            if (playerHealth != null) playerHealth.OnDamaged -= HandlePlayerDamaged;
            if (wallet != null) wallet.OnXPChanged -= HandleXPChanged;

            if (upgradeManager != null) upgradeManager.OnPendingUpgradesChanged -= HandlePendingUpgradesChanged;

            UnbindWeaponManager();
        }

        private void HandlePlayerReady(PlayerController player)
        {
            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged -= HandleHealthChanged;
                playerHealth.OnDamaged -= HandlePlayerDamaged;
            }

            var playerObject = player.gameObject;

            playerHealth = playerObject.GetComponent<Health>();
            wallet = playerObject.GetComponent<XPWallet>();
            weaponManager = playerObject.GetComponentInChildren<WeaponManager>();

            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged += HandleHealthChanged;
                playerHealth.OnDamaged += HandlePlayerDamaged;
            }

            if (wallet != null)
            {
                wallet.OnXPChanged += HandleXPChanged;
                UpgradeManager.I?.RegisterWallet(wallet);
            }

            BindWeaponManager(weaponManager);

            RefreshAll();
            SyncFillImmediately();
            RefreshUpgradeManagerReference();
        }


        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplySceneVisibility(scene);

            if (!IsGameplayScene(scene))
            {
                return;
            }

            TryBindAll();
            RefreshAll();
            SyncFillImmediately();
        }

        private void ApplySceneVisibility(Scene scene)
        {
            bool isGameplay = IsGameplayScene(scene);

            if (rootCanvasGroup)
            {
                rootCanvasGroup.alpha = isGameplay ? 1f : 0f;
                rootCanvasGroup.interactable = isGameplay;
                rootCanvasGroup.blocksRaycasts = isGameplay;
            }

            if (!isGameplay)
            {
                upgradePromptTimer = 0f;
                ApplyUpgradeSummaryVisibility(false);
            }
        }

        private bool IsGameplayScene(Scene scene)
        {
            string gameplayName = SceneFlowController.Instance ? SceneFlowController.Instance.GameplaySceneName : string.Empty;
            string menuName = SceneFlowController.Instance ? SceneFlowController.Instance.MainMenuSceneName : string.Empty;

            if (!string.IsNullOrEmpty(gameplayName) && scene.name.Equals(gameplayName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(menuName) && scene.name.Equals(menuName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return scene.name.Equals("Main", StringComparison.OrdinalIgnoreCase);
        }

        private void TryBindAll()
        {
            gameManager = GameManager.I;
            playerHealth = FindFirstObjectByType<Health>();
            wallet = playerHealth ? playerHealth.GetComponent<XPWallet>() : null;
            weaponManager = FindFirstObjectByType<WeaponManager>();

            // FIX: Prefer singleton
            upgradeManager = UpgradeManager.I;
            if (!upgradeManager) upgradeManager = FindFirstObjectByType<UpgradeManager>();


            RefreshUpgradeManagerReference();
            BindWeaponManager(weaponManager);
        }

        private void BindWeaponManager(WeaponManager manager)
        {
            if (manager == null)
            {
                UnbindWeaponManager();
                return;
            }

            if (manager == boundWeaponManager)
            {
                HandlePickupAvailabilityChanged(manager.HasNearbyPickups);
                return;
            }

            UnbindWeaponManager();

            boundWeaponManager = manager;
            boundWeaponManager.OnWeaponEquipped += HandleWeaponEquipped;
            boundWeaponManager.OnInventoryChanged += HandleWeaponInventoryChanged;
            boundWeaponManager.OnPickupAvailabilityChanged += HandlePickupAvailabilityChanged;

            HandlePickupAvailabilityChanged(boundWeaponManager.HasNearbyPickups);
        }

        private void UnbindWeaponManager()
        {
            if (boundWeaponManager == null)
            {
                return;
            }

            boundWeaponManager.OnWeaponEquipped -= HandleWeaponEquipped;
            boundWeaponManager.OnInventoryChanged -= HandleWeaponInventoryChanged;
            boundWeaponManager.OnPickupAvailabilityChanged -= HandlePickupAvailabilityChanged;
            boundWeaponManager = null;

            HandlePickupAvailabilityChanged(false);
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;
            float unscaledDeltaTime = Time.unscaledDeltaTime;

            if (upgradePromptTimer > 0f)
            {
                upgradePromptTimer -= unscaledDeltaTime;
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
            UpdateUpgradeSummaryToggle();
        }

        void BindSceneManagers()
        {
            RefreshGameManagerReference();
            RefreshUpgradeManagerReference();
        }

        void RefreshGameManagerReference()
        {
            GameManager resolved = gameManager ? gameManager : GameManager.I;

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
            }
        }

        void RefreshUpgradeManagerReference()
        {

            // FIX: Robustly switch to Singleton if the current reference is stale or wron
   
            if (upgradeManager != null)
            {
                upgradeManager.OnPendingUpgradesChanged -= HandlePendingUpgradesChanged;
            }

            if (upgradeManager != null && isActiveAndEnabled)
            {
                upgradeManager.OnPendingUpgradesChanged += HandlePendingUpgradesChanged;
                // Force immediate sync
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

            // FIX: Explicitly refresh prompt from manager state
            if (upgradeManager)
                HandlePendingUpgradesChanged(upgradeManager.GetPendingUpgradeCount());
            else
                RefreshUpgradePrompt();
        }

        void HandleBindingsChanged()
        {
            RefreshKeybindLabels();
            RefreshUpgradePrompt();
            RefreshUpgradeSummaryText();
        }

        void RefreshKeybindLabels()
        {
            if (upgradeAction)
            {
                upgradeKeyLabel = InputBindingManager.GetBindingDisplay(upgradeAction, upgradeBindingId, 0, upgradeKeyLabel);
            }

            if (interactAction)
            {
                interactKeyLabel = InputBindingManager.GetBindingDisplay(interactAction, interactBindingId, 0, interactKeyLabel);
            }
        }

        InputActionAsset ResolveInputAsset()
        {
            return upgradeAction?.action?.actionMap?.asset ?? interactAction?.action?.actionMap?.asset;
        }

        void EnsureActionReferences()
        {
            var asset = ResolveInputAsset();
            if (asset == null)
            {
                var playerInput = FindFirstObjectByType<PlayerInput>();
                asset = playerInput ? playerInput.actions : null;
            }

            upgradeAction = EnsureActionReference(upgradeAction, asset, upgradeBindingId, upgradeActionName);
            interactAction = EnsureActionReference(interactAction, asset, interactBindingId, interactActionName);
            upgradeSummaryAction = EnsureActionReference(upgradeSummaryAction, asset, upgradeSummaryBindingId, upgradeSummaryActionName);
        }

        static InputActionReference EnsureActionReference(InputActionReference reference, InputActionAsset asset, string bindingId, string actionName)
        {
            if (reference != null && reference.action != null)
            {
                return reference;
            }

            var action = FindActionForBinding(asset, bindingId, actionName);
            if (action != null)
            {
                return InputActionReference.Create(action);
            }

            return reference;
        }

        static InputAction FindActionForBinding(InputActionAsset asset, string bindingId, string actionName)
        {
            if (asset != null && !string.IsNullOrEmpty(bindingId))
            {
                foreach (var map in asset.actionMaps)
                {
                    foreach (var action in map.actions)
                    {
                        for (int i = 0; i < action.bindings.Count; i++)
                        {
                            if (action.bindings[i].id.ToString() == bindingId)
                            {
                                return action;
                            }
                        }
                    }
                }
            }

            if (asset != null && !string.IsNullOrEmpty(actionName))
            {
                return asset.FindAction(actionName, false);
            }

            return null;
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

        void HandlePlayerDamaged(int _)
        {
            TriggerHitFlash();
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
                StartUpgradePromptFade(hasNearbyPickups ? 1f : 0f);
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

            if (wave == 1)
            {
                FadeOutGetReady();
            }
        }

        void HandlePickupAvailabilityChanged(bool hasPickups)
        {
            hasNearbyPickups = hasPickups;

            if (!upgradeMenuVisible && pendingUpgrades <= 0)
            {
                StartUpgradePromptFade(hasPickups ? 1f : 0f);
            }

            RefreshUpgradePrompt();
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
            else if (hasNearbyPickups)
            {
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
            else if (hasNearbyPickups)
            {
                upgradePromptText.text = $"Press {interactKeyLabel} to pickup";
            }
            else
            {
                upgradePromptText.text = string.Empty;
            }
        }

        void UpdateUpgradeSummaryToggle()
        {
            if (upgradeSummaryInput == null)
            {
                return;
            }

            // Changed behavior: show summary only while the key is held (hold-to-view).
            // Read the button value (0/1) and set visibility while pressed.
            bool isHeld = upgradeSummaryInput.ReadValue<float>() > 0.5f;

            if (isHeld && !upgradeSummaryVisible)
            {
                upgradeSummaryVisible = true;
                ApplyUpgradeSummaryVisibility(true);
                RefreshUpgradeSummaryText();
            }
            else if (!isHeld && upgradeSummaryVisible)
            {
                upgradeSummaryVisible = false;
                ApplyUpgradeSummaryVisibility(false);
            }
        }

        void ApplyUpgradeSummaryVisibility(bool isVisible)
        {
            if (upgradeSummaryGroup)
            {
                upgradeSummaryGroup.alpha = isVisible ? 1f : 0f;
                upgradeSummaryGroup.interactable = isVisible;
                upgradeSummaryGroup.blocksRaycasts = isVisible;
                upgradeSummaryGroup.gameObject.SetActive(isVisible);
            }

            if (upgradeSummaryTitleText)
            {
                upgradeSummaryTitleText.gameObject.SetActive(isVisible);
            }

            if (upgradeSummaryPlayerText)
            {
                upgradeSummaryPlayerText.gameObject.SetActive(isVisible);
            }

            if (upgradeSummaryWeaponText)
            {
                upgradeSummaryWeaponText.gameObject.SetActive(isVisible);
            }

            if (!isVisible)
            {
                upgradeSummaryVisible = false;
            }
        }

        void RefreshUpgradeSummaryText()
        {
            if (upgradeSummaryTitleText)
            {
                upgradeSummaryTitleText.text = upgradeSummaryTitle;
            }

            if (!upgradeSummaryPlayerText && !upgradeSummaryWeaponText)
            {
                return;
            }

            if (upgradeManager == null)
            {
                if (upgradeSummaryPlayerText)
                {
                    upgradeSummaryPlayerText.text = "No upgrade data.";
                }

                if (upgradeSummaryWeaponText)
                {
                    upgradeSummaryWeaponText.text = "No upgrade data.";
                }

                return;
            }

            if (upgradeSummaryPlayerText)
            {
                upgradeSummaryPlayerText.text = BuildPlayerUpgradeSummary();
            }

            if (upgradeSummaryWeaponText)
            {
                upgradeSummaryWeaponText.text = BuildWeaponUpgradeSummary();
            }
        }

        void StartUpgradePromptFade(float targetAlpha, bool instant = false)
        {
            if (upgradePromptFadeRoutine != null) StopCoroutine(upgradePromptFadeRoutine);

            if (targetAlpha > 0f && upgradePromptGroup && !upgradePromptGroup.gameObject.activeSelf)
            {
                upgradePromptGroup.gameObject.SetActive(true);
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
            if (!upgradePromptGroup) yield break;

            float duration = Mathf.Max(0.01f, upgradePromptFadeDuration);
            float startAlpha = upgradePromptGroup.alpha;
            float elapsed = 0f;

            upgradePromptGroup.gameObject.SetActive(true);
            upgradePromptGroup.interactable = true;
            upgradePromptGroup.blocksRaycasts = true;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
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
            if (!upgradePromptGroup) return;
            bool isVisible = targetAlpha > 0f;
            upgradePromptGroup.alpha = Mathf.Max(0f, targetAlpha);
            upgradePromptGroup.interactable = isVisible;
            upgradePromptGroup.blocksRaycasts = isVisible;
            upgradePromptGroup.gameObject.SetActive(isVisible);

            if (!isVisible && upgradePromptText) upgradePromptText.gameObject.SetActive(false);
        }

        private string BuildPlayerUpgradeSummary()
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("Player Upgrades:");

            IReadOnlyDictionary<Upgrade, int> playerUpgrades = upgradeManager.GetUpgradeCounts();
            bool hasPlayerUpgrades = false;

            foreach (var entry in playerUpgrades.OrderBy(entry => ResolveUpgradeTitle(entry.Key)))
            {
                if (entry.Key == null || entry.Value <= 0)
                {
                    continue;
                }

                hasPlayerUpgrades = true;
                builder.AppendLine($"- {ResolveUpgradeTitle(entry.Key)}{FormatUpgradeCount(entry.Value)}");
            }

            if (!hasPlayerUpgrades)
            {
                builder.AppendLine("None");
            }

            return builder.ToString().TrimEnd();
        }

        private string BuildWeaponUpgradeSummary()
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("Weapon Upgrades:");

            IReadOnlyDictionary<Weapon, WeaponUpgradeState> weaponStates = upgradeManager.GetWeaponUpgradeStates();
            bool hasWeaponUpgrades = false;

            foreach (var entry in weaponStates.OrderBy(entry => ResolveWeaponName(entry.Key)))
            {
                if (entry.Key == null || entry.Value == null)
                {
                    continue;
                }

                IReadOnlyDictionary<WeaponUpgradeType, int> counts = entry.Value.GetUpgradeCounts();
                if (counts.Count == 0)
                {
                    continue;
                }

                bool hasWeaponLines = false;
                foreach (var countEntry in counts.OrderBy(countEntry => countEntry.Key))
                {
                    if (countEntry.Value <= 0)
                    {
                        continue;
                    }

                    if (!hasWeaponLines)
                    {
                        builder.AppendLine(ResolveWeaponName(entry.Key));
                        hasWeaponLines = true;
                    }

                    builder.AppendLine($"  {GetWeaponUpgradeLabel(countEntry.Key)}{FormatUpgradeCount(countEntry.Value)}");
                }

                if (hasWeaponLines)
                {
                    hasWeaponUpgrades = true;
                }
            }

            if (!hasWeaponUpgrades)
            {
                builder.AppendLine("None");
            }

            return builder.ToString().TrimEnd();
        }

        private void BindUpgradeSummaryInput()
        {
            if (upgradeSummaryInput != null)
            {
                return;
            }

            upgradeSummaryInput = upgradeSummaryAction != null ? upgradeSummaryAction.action : null;
            ownsUpgradeSummaryInput = upgradeSummaryInput == null;

            if (upgradeSummaryInput == null)
            {
                upgradeSummaryInput = new InputAction("UpgradeSummaryHold", InputActionType.Button, "<Keyboard>/tab");
            }

            if (!upgradeSummaryInput.enabled)
            {
                upgradeSummaryInput.Enable();
            }
        }

        private void UnbindUpgradeSummaryInput()
        {
            if (upgradeSummaryInput == null)
            {
                return;
            }

            if (ownsUpgradeSummaryInput)
            {
                upgradeSummaryInput.Disable();
                upgradeSummaryInput.Dispose();
            }

            upgradeSummaryInput = null;
            ownsUpgradeSummaryInput = false;
        }

        private static string ResolveUpgradeTitle(Upgrade upgrade)
        {
            if (upgrade == null)
            {
                return "Upgrade";
            }

            return string.IsNullOrWhiteSpace(upgrade.Title) ? upgrade.name : upgrade.Title;
        }

        private static string ResolveWeaponName(Weapon weapon)
        {
            if (weapon == null)
            {
                return "Weapon";
            }

            return string.IsNullOrWhiteSpace(weapon.weaponName) ? weapon.name : weapon.weaponName;
        }

        private static string FormatUpgradeCount(int count)
        {
            return count > 1 ? $" x{count}" : string.Empty;
        }

        private static string GetWeaponUpgradeLabel(WeaponUpgradeType type)
        {
            return type switch
            {
                WeaponUpgradeType.Damage => "- Damage Boost",
                WeaponUpgradeType.FireRate => "- Fire Rate Boost",
                WeaponUpgradeType.ProjectileSpeed => "- Bullet Speed Boost",
                WeaponUpgradeType.Pierce => "- Piercing Rounds",
                WeaponUpgradeType.ExtraProjectiles => "- Multi-Shot",
                WeaponUpgradeType.FireCooldownReduction => "- Cooldown Reduction",
                WeaponUpgradeType.CritChance => "- Critical Chance Boost",
                WeaponUpgradeType.CritDamage => "- Critical Damage Boost",
                WeaponUpgradeType.Accuracy => "- Accuracy Boost",
                WeaponUpgradeType.FlamethrowerCooldown => "- Flamethrower Venting",
                WeaponUpgradeType.FlamethrowerRange => "- Longer Flame",
                _ => "Upgrade"
            };
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
            activeWaveFlashColor = waveFlashColor;
            waveFlashBaseAlpha = Mathf.Clamp01(activeWaveFlashColor.a);
            waveFlashElapsed = float.PositiveInfinity;
            SetWaveFlashAlpha(0f);
        }

        void TriggerWaveFlash()
        {
            TriggerFlash(waveFlashColor);
        }

        void TriggerHitFlash()
        {
            TriggerFlash(hitFlashColor);
        }

        void TriggerFlash(Color flashColor)
        {
            if (!waveFlashImage || waveFlashDuration <= 0f)
            {
                return;
            }

            activeWaveFlashColor = flashColor;
            waveFlashBaseAlpha = Mathf.Clamp01(activeWaveFlashColor.a);
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
            Color color = activeWaveFlashColor;
            color.a = alpha;
            waveFlashImage.color = color;
        }

        void InitializeGetReady()
        {
            if (!getReadyGroup && !getReadyText)
            {
                return;
            }

            if (getReadyGroup)
            {
                getReadyGroup.alpha = 1f;
                getReadyGroup.gameObject.SetActive(true);
                getReadyGroup.blocksRaycasts = false;
                getReadyGroup.interactable = false;
            }

            getReadyDismissed = false;
        }

        void FadeOutGetReady()
        {
            if (getReadyDismissed)
            {
                return;
            }

            getReadyDismissed = true;

            if (!getReadyGroup)
            {
                return;
            }

            if (getReadyRoutine != null)
            {
                StopCoroutine(getReadyRoutine);
            }

            getReadyRoutine = StartCoroutine(GetReadyFadeRoutine());
        }

        IEnumerator GetReadyFadeRoutine()
        {
            if (getReadyGroup)
            {
                getReadyGroup.gameObject.SetActive(true);
                getReadyGroup.alpha = 1f;
            }

            if (getReadyHoldDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(getReadyHoldDuration);
            }

            float duration = Mathf.Max(0.01f, getReadyFadeDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (getReadyGroup)
                {
                    getReadyGroup.alpha = Mathf.Lerp(1f, 0f, t);
                }

                yield return null;
            }

            if (getReadyGroup)
            {
                getReadyGroup.alpha = 0f;
                getReadyGroup.gameObject.SetActive(false);
            }

            getReadyRoutine = null;
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
                uiAudioSource.PlayOneShot(waveStartClip, GameAudioSettings.SfxVolume);
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
                uiAudioSource.PlayOneShot(waveMilestoneClip, GameAudioSettings.SfxVolume);
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
            activeWaveFlashColor = waveFlashColor;
            waveFlashBaseAlpha = Mathf.Clamp01(activeWaveFlashColor.a);
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

            getReadyFadeDuration = Mathf.Max(0f, getReadyFadeDuration);
            getReadyHoldDuration = Mathf.Max(0f, getReadyHoldDuration);
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
