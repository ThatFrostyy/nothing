using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FF
{
    public class ActivePickupEntry : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text multiplierText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private string timeFormat = "m\\:ss";

        private float startTime;
        private float duration;

        public void Initialize(Sprite icon, float multiplier, float durationSeconds)
        {
            startTime = Time.time;
            duration = durationSeconds;

            if (iconImage)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }

            if (multiplierText)
            {
                multiplierText.text = $"{multiplier:0.##}x";
            }

            UpdateTimer();
        }

        public bool UpdateEntry()
        {
            UpdateTimer();
            return GetRemainingTime() > 0f;
        }

        private void UpdateTimer()
        {
            if (!timerText)
            {
                return;
            }

            float remaining = GetRemainingTime();
            TimeSpan span = TimeSpan.FromSeconds(Mathf.Max(0f, remaining));
            timerText.text = span.ToString(timeFormat);
        }

        private float GetRemainingTime()
        {
            return (startTime + duration) - Time.time;
        }
    }
}
