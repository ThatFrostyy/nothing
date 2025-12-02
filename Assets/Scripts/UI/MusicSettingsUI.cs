using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FF
{
    public class MusicSettingsUI : MonoBehaviour
    {
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private TMP_Text musicValueLabel;
        [SerializeField, Min(0f)] private float sliderScale = 100f;

        private void Awake()
        {
            MusicManager.EnsureInstance();
            BindSlider();
            RefreshDisplay();
        }

        private void OnEnable()
        {
            RefreshDisplay();
        }

        private void OnDestroy()
        {
            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.onValueChanged.RemoveListener(HandleSliderChanged);
            }
        }

        public void RefreshDisplay()
        {
            float volume = MusicManager.MusicVolume;
            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.SetValueWithoutNotify(volume);
            }

            UpdateLabel(volume);
        }

        private void BindSlider()
        {
            if (musicVolumeSlider == null)
            {
                return;
            }

            musicVolumeSlider.minValue = 0f;
            musicVolumeSlider.maxValue = 1f;
            musicVolumeSlider.wholeNumbers = false;
            musicVolumeSlider.onValueChanged.AddListener(HandleSliderChanged);
        }

        private void HandleSliderChanged(float value)
        {
            MusicManager.SetVolume(value);
            UpdateLabel(value);
        }

        private void UpdateLabel(float rawValue)
        {
            if (musicValueLabel == null)
            {
                return;
            }

            float scaled = Mathf.Round(rawValue * sliderScale);
            musicValueLabel.text = $"{scaled:0}%";
        }
    }
}
