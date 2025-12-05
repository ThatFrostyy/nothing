using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FF
{
    public class MusicSettingsUI : MonoBehaviour
    {
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private TMP_Text musicValueLabel;
        [SerializeField] private Slider ambienceVolumeSlider;
        [SerializeField] private TMP_Text ambienceValueLabel;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private TMP_Text sfxValueLabel;
        [SerializeField, Min(0f)] private float sliderScale = 100f;

        private void Awake()
        {
            BindSlider(musicVolumeSlider, HandleMusicSliderChanged);
            BindSlider(ambienceVolumeSlider, HandleAmbienceSliderChanged);
            BindSlider(sfxVolumeSlider, HandleSfxSliderChanged);
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
                musicVolumeSlider.onValueChanged.RemoveListener(HandleMusicSliderChanged);
            }

            if (ambienceVolumeSlider != null)
            {
                ambienceVolumeSlider.onValueChanged.RemoveListener(HandleAmbienceSliderChanged);
            }

            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.onValueChanged.RemoveListener(HandleSfxSliderChanged);
            }
        }

        public void RefreshDisplay()
        {
            float volume = MusicManager.MusicVolume;
            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.SetValueWithoutNotify(volume);
            }

            UpdateLabel(musicValueLabel, volume);

            float ambience = GameAudioSettings.AmbienceVolume;
            if (ambienceVolumeSlider != null)
            {
                ambienceVolumeSlider.SetValueWithoutNotify(ambience);
            }
            UpdateLabel(ambienceValueLabel, ambience);

            float sfx = GameAudioSettings.SfxVolume;
            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.SetValueWithoutNotify(sfx);
            }
            UpdateLabel(sfxValueLabel, sfx);
        }

        private void BindSlider(Slider slider, UnityEngine.Events.UnityAction<float> callback)
        {
            if (slider == null)
            {
                return;
            }

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.onValueChanged.AddListener(callback);
        }

        private void HandleMusicSliderChanged(float value)
        {
            MusicManager.SetVolume(value);
            UpdateLabel(musicValueLabel, value);
        }

        private void HandleAmbienceSliderChanged(float value)
        {
            GameAudioSettings.SetAmbienceVolume(value);
            UpdateLabel(ambienceValueLabel, value);
        }

        private void HandleSfxSliderChanged(float value)
        {
            GameAudioSettings.SetSfxVolume(value);
            UpdateLabel(sfxValueLabel, value);
        }

        private void UpdateLabel(TMP_Text label, float rawValue)
        {
            if (label == null)
            {
                return;
            }

            float scaled = Mathf.Round(rawValue * sliderScale);
            label.text = $"{scaled:0}%";
        }
    }
}
