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
            }

            if (weaponManager != null)
            {
                weaponManager.OnWeaponEquipped += HandleWeaponEquipped;
            }

            RefreshAll();
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
            }

            if (weaponManager != null)
            {
                weaponManager.OnWeaponEquipped -= HandleWeaponEquipped;
            }
        }

        void Update()
        {
            UpdateWaveDisplay();
            UpdateTimeDisplay();
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
            if (!healthValueText && !healthFillImage)
            {
                return;
            }

            if (max <= 0)
            {
                if (healthValueText)
                {
                    healthValueText.text = "HP: --";
                }

                if (healthFillImage)
                {
                    healthFillImage.fillAmount = 0f;
                }
            }
            else
            {
                int clamped = Mathf.Clamp(current, 0, max);

                if (healthValueText)
                {
                    healthValueText.text = $"HP: {clamped}/{max}";
                }

                if (healthFillImage)
                {
                    float fill = Mathf.Clamp01((float)clamped / max);
                    healthFillImage.fillAmount = fill;
                }
            }
        }

        void HandleXPChanged(int level, int current, int next)
        {
            if (xpText)
            {
                xpText.text = $"Level {level} – XP: {current}/{Mathf.Max(1, next)}";
            }

            if (xpFillImage)
            {
                float fill = next <= 0 ? 0f : Mathf.Clamp01((float)current / next);
                xpFillImage.fillAmount = fill;
            }
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
    }
}
