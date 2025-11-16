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
        [SerializeField] private string timeFormat = "m\:ss";
        [SerializeField, Min(0f)] private float warningTime = 5f;
        [SerializeField] private Color warningColor = Color.red;
        [SerializeField, Min(0f)] private float warningPulseSpeed = 8f;
        [SerializeField, Min(0f)] private float warningPulseAmplitude = 0.1f;

        private float startTime;
        private float duration;
        private Color initialTimerColor;
        private Vector3 baseTimerScale;

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

            if (timerText)
            {
                initialTimerColor = timerText.color;
                baseTimerScale = timerText.rectTransform.localScale;
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

            UpdateWarningVisuals(remaining);
        }

        private void UpdateWarningVisuals(float remainingSeconds)
        {
            if (!timerText)
            {
                return;
            }

            bool inWarning = remainingSeconds <= warningTime;
            timerText.color = inWarning ? warningColor : initialTimerColor;

            if (inWarning)
            {
                float pulse = 1f + Mathf.Abs(Mathf.Sin(Time.time * warningPulseSpeed)) * warningPulseAmplitude;
                timerText.rectTransform.localScale = baseTimerScale * pulse;
            }
            else
            {
                timerText.rectTransform.localScale = baseTimerScale;
            }
        }

        private float GetRemainingTime()
        {
            return (startTime + duration) - Time.time;
        }
    }
}
