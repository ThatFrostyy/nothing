using System;
using UnityEngine;
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

        [Header("UI References")]
        [SerializeField] private TMP_Text healthValueText;
        [SerializeField] private Image healthFillImage;
        [SerializeField] private TMP_Text killCountText;
        [SerializeField] private TMP_Text weaponNameText;
        [SerializeField] private TMP_Text waveText;
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text xpText;
        [SerializeField] private Image xpFillImage;

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

        [Header("Wave Banner")]
        [SerializeField] private TMP_Text waveBannerText;
        [SerializeField] private CanvasGroup waveBannerGroup;
        [SerializeField, Min(0f)] private float waveBannerDuration = 2.5f;
        [SerializeField, Min(0f)] private float waveBannerFadeTime = 0.5f;

        [Header("Audio")]
        [SerializeField] private AudioClip waveStartClip;
        [SerializeField] private AudioClip heartbeatClip;
        [SerializeField] private AudioClip xpFillLoopClip;

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

        private float waveBannerTimer;

        private Vector3 healthPulseBaseScale = Vector3.one;
        private Vector3 xpPulseBaseScale = Vector3.one;
        private Vector2 healthPulseBaseAnchoredPosition = Vector2.zero;
        private Vector2 xpPulseBaseAnchoredPosition = Vector2.zero;
        private Vector3 healthPulseScaleVelocity = Vector3.zero;
        private Vector3 xpPulseScaleVelocity = Vector3.zero;

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

            healthPulseTarget = ResolveHealthPulseTarget();

            if (!xpPulseTarget && xpFillImage)
            {
                xpPulseTarget = xpFillImage.rectTransform;
            }

            healthPulseBaseScale = healthPulseTarget ? healthPulseTarget.localScale : Vector3.one;
            xpPulseBaseScale = xpPulseTarget ? xpPulseTarget.localScale : Vector3.one;
            healthPulseBaseAnchoredPosition = healthPulseTarget ? healthPulseTarget.anchoredPosition : Vector2.zero;
            xpPulseBaseAnchoredPosition = xpPulseTarget ? xpPulseTarget.anchoredPosition : Vector2.zero;

            healthFillCurrent = healthFillImage ? healthFillImage.fillAmount : 0f;
            healthFillTarget = healthFillCurrent;

            xpFillCurrent = xpFillImage ? xpFillImage.fillAmount : 0f;
            xpFillTarget = xpFillCurrent;

            InitializeAudio();
            SetWaveBannerVisible(0f);
        }

        void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged += HandleHealthChanged;
            }

            if (wallet != null)
            {
                wallet.OnXPChanged += HandleXPChanged;
            }

            if (gameManager != null)
            {
                gameManager.OnKillCountChanged += HandleKillCountChanged;
                gameManager.OnWaveStarted += HandleWaveStarted;
            }

            if (weaponManager != null)
            {
                weaponManager.OnWeaponEquipped += HandleWeaponEquipped;
            }

            RefreshAll();
            SyncFillImmediately();
        }

        void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged -= HandleHealthChanged;
            }

            if (wallet != null)
            {
                wallet.OnXPChanged -= HandleXPChanged;
            }

            if (gameManager != null)
            {
                gameManager.OnKillCountChanged -= HandleKillCountChanged;
                gameManager.OnWaveStarted -= HandleWaveStarted;
            }

            if (weaponManager != null)
            {
                weaponManager.OnWeaponEquipped -= HandleWeaponEquipped;
            }

            SetHeartbeatActive(false);
            SetXPFillSoundActive(false);
            lowHealthPulseActive = false;
            xpIsFilling = false;
            lowHealthHeartbeatTimer = 0f;
            ResetHealthPulse();
            ResetXPPulse();
            waveBannerTimer = 0f;
            SetWaveBannerVisible(0f);
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;
            float unscaledDeltaTime = Time.unscaledDeltaTime;
            UpdateWaveDisplay();
            UpdateTimeDisplay();
            UpdateHealthFill(deltaTime);
            UpdateXPFill(deltaTime);
            UpdateHealthPulse(deltaTime);
            UpdateXPPulse(deltaTime);
            UpdateHeartbeatTimer(unscaledDeltaTime);
            UpdateWaveBanner(unscaledDeltaTime);
        }

        void RefreshAll()
        {
            HandleHealthChanged(playerHealth ? playerHealth.CurrentHP : 0, playerHealth ? playerHealth.MaxHP : 0);
            HandleXPChanged(wallet ? wallet.Level : 1, wallet ? wallet.XP : 0, wallet ? wallet.Next : 1);
            HandleKillCountChanged(gameManager ? gameManager.KillCount : 0);
            UpdateWeaponDisplay();
            UpdateWaveDisplay();
            UpdateTimeDisplay();
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

        void HandleKillCountChanged(int kills)
        {
            if (!killCountText)
            {
                return;
            }

            killCountText.text = $"Kills: {Mathf.Max(0, kills)}";
        }

        void HandleWeaponEquipped(Weapon weapon)
        {
            UpdateWeaponDisplay(weapon);
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
            PlayWaveStartSound();
        }

        void UpdateWeaponDisplay(Weapon weaponOverride = null)
        {
            if (!weaponNameText)
            {
                return;
            }

            Weapon weaponToShow = weaponOverride ? weaponOverride : weaponManager ? weaponManager.CurrentWeapon : null;
            string weaponLabel = weaponToShow && !string.IsNullOrEmpty(weaponToShow.weaponName)
                ? weaponToShow.weaponName
                : "--";

            weaponNameText.text = $"Weapon: {weaponLabel}";
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

        void PlayWaveStartSound()
        {
            if (!waveStartClip || !uiAudioSource)
            {
                return;
            }

            uiAudioSource.PlayOneShot(waveStartClip);
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
