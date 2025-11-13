using System;
using UnityEngine;
using UnityEngine.UI;

namespace FF
{
    public class GameHUD : MonoBehaviour
    {
        [Header("Data Sources")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private Health playerHealth;
        [SerializeField] private XPWallet wallet;

        [Header("UI References")]
        [SerializeField] private Text healthText;
        [SerializeField] private Text waveText;
        [SerializeField] private Text timeText;
        [SerializeField] private Text xpText;
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
                }
            }

            if (!wallet && playerHealth)
            {
                wallet = playerHealth.GetComponent<XPWallet>();
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
            UpdateWaveDisplay();
            UpdateTimeDisplay();
        }

        void HandleHealthChanged(int current, int max)
        {
            if (!healthText)
            {
                return;
            }

            if (max <= 0)
            {
                healthText.text = "HP: --";
            }
            else
            {
                healthText.text = $"HP: {Mathf.Clamp(current, 0, max)}/{max}";
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
            timeText.text = $"Time: {span.ToString(timeFormat)}";
        }
    }
}
