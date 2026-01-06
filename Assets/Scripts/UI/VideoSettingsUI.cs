using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FF
{
    public class VideoSettingsUI : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Toggle vsyncToggle;

        [Header("Effects")]
        [SerializeField] private Toggle postProcessingToggle;
        [SerializeField] private Slider bloomSlider;
        [SerializeField] private TMP_Text bloomValueLabel;
        [SerializeField, Min(0f)] private float bloomValueScale = 100f;
        [SerializeField] private Slider motionBlurSlider;
        [SerializeField] private TMP_Text motionBlurValueLabel;
        [SerializeField, Min(0f)] private float motionBlurValueScale = 100f;

        private readonly List<Resolution> _resolutions = new();
        private bool _suppressCallbacks;

        private void Awake()
        {
            GameVideoSettings.Initialize();
            BuildDropdowns();
            ConfigureSliders();
            BindListeners();
            RefreshDisplay();
        }

        private void OnEnable()
        {
            RefreshDisplay();
        }

        private void OnDestroy()
        {
            if (qualityDropdown != null)
            {
                qualityDropdown.onValueChanged.RemoveListener(HandleQualityChanged);
            }

            if (resolutionDropdown != null)
            {
                resolutionDropdown.onValueChanged.RemoveListener(HandleResolutionChanged);
            }

            if (vsyncToggle != null)
            {
                vsyncToggle.onValueChanged.RemoveListener(HandleVsyncChanged);
            }

            if (postProcessingToggle != null)
            {
                postProcessingToggle.onValueChanged.RemoveListener(HandlePostProcessingChanged);
            }

            if (bloomSlider != null)
            {
                bloomSlider.onValueChanged.RemoveListener(HandleBloomChanged);
            }

            if (motionBlurSlider != null)
            {
                motionBlurSlider.onValueChanged.RemoveListener(HandleMotionBlurChanged);
            }
        }

        public void RefreshDisplay()
        {
            _suppressCallbacks = true;

            if (qualityDropdown != null)
            {
                qualityDropdown.SetValueWithoutNotify(Mathf.Clamp(GameVideoSettings.QualityLevel, 0, qualityDropdown.options.Count - 1));
            }

            if (resolutionDropdown != null)
            {
                int resIndex = FindResolutionIndex(GameVideoSettings.Resolution);
                resolutionDropdown.SetValueWithoutNotify(resIndex);
            }

            if (vsyncToggle != null)
            {
                vsyncToggle.SetIsOnWithoutNotify(GameVideoSettings.VSyncEnabled);
            }

            if (postProcessingToggle != null)
            {
                postProcessingToggle.SetIsOnWithoutNotify(GameVideoSettings.PostProcessingEnabled);
            }

            if (bloomSlider != null)
            {
                bloomSlider.SetValueWithoutNotify(GameVideoSettings.BloomIntensity);
            }
            UpdateScaledLabel(bloomValueLabel, GameVideoSettings.BloomIntensity, bloomValueScale);

            if (motionBlurSlider != null)
            {
                motionBlurSlider.SetValueWithoutNotify(GameVideoSettings.MotionBlurIntensity);
            }
            UpdateScaledLabel(motionBlurValueLabel, GameVideoSettings.MotionBlurIntensity, motionBlurValueScale);

            _suppressCallbacks = false;
        }

        private void BuildDropdowns()
        {
            if (qualityDropdown)
            {
                qualityDropdown.ClearOptions();
                qualityDropdown.AddOptions(QualitySettings.names.ToList());
            }

            _resolutions.Clear();
            _resolutions.AddRange(GameVideoSettings.AvailableResolutions);
            if (resolutionDropdown)
            {
                resolutionDropdown.ClearOptions();
                var options = _resolutions.Select(FormatResolutionLabel).ToList();
                resolutionDropdown.AddOptions(options);
            }
        }

        private void ConfigureSliders()
        {
            ConfigureSlider(bloomSlider, 0f, 5f);
            ConfigureSlider(motionBlurSlider, 0f, 1f);
        }

        private void BindListeners()
        {
            if (qualityDropdown)
            {
                qualityDropdown.onValueChanged.AddListener(HandleQualityChanged);
            }

            if (resolutionDropdown)
            {
                resolutionDropdown.onValueChanged.AddListener(HandleResolutionChanged);
            }

            if (vsyncToggle)
            {
                vsyncToggle.onValueChanged.AddListener(HandleVsyncChanged);
            }

            if (postProcessingToggle)
            {
                postProcessingToggle.onValueChanged.AddListener(HandlePostProcessingChanged);
            }

            if (bloomSlider)
            {
                bloomSlider.onValueChanged.AddListener(HandleBloomChanged);
            }

            if (motionBlurSlider)
            {
                motionBlurSlider.onValueChanged.AddListener(HandleMotionBlurChanged);
            }
        }

        private void HandleQualityChanged(int index)
        {
            if (_suppressCallbacks)
            {
                return;
            }

            GameVideoSettings.SetQualityLevel(index);
        }

        private void HandleResolutionChanged(int index)
        {
            if (_suppressCallbacks || index < 0 || index >= _resolutions.Count)
            {
                return;
            }

            GameVideoSettings.SetResolution(_resolutions[index]);
        }

        private void HandleVsyncChanged(bool enabled)
        {
            if (_suppressCallbacks)
            {
                return;
            }

            GameVideoSettings.SetVSync(enabled);
        }

        private void HandlePostProcessingChanged(bool enabled)
        {
            if (_suppressCallbacks)
            {
                return;
            }

            GameVideoSettings.SetPostProcessingEnabled(enabled);
        }

        private void HandleBloomChanged(float value)
        {
            UpdateScaledLabel(bloomValueLabel, value, bloomValueScale);
            if (_suppressCallbacks)
            {
                return;
            }

            GameVideoSettings.SetBloomIntensity(value);
        }

        private void HandleMotionBlurChanged(float value)
        {
            UpdateScaledLabel(motionBlurValueLabel, value, motionBlurValueScale);
            if (_suppressCallbacks)
            {
                return;
            }

            GameVideoSettings.SetMotionBlurIntensity(value);
        }

        private void UpdateScaledLabel(TMP_Text label, float value, float scale)
        {
            if (label == null)
            {
                return;
            }

            float scaledValue = Mathf.Round(value * scale);
            label.text = scale > 1f ? $"{scaledValue:0}%" : $"{value:0.00}";
        }

        private void ConfigureSlider(Slider slider, float min, float max)
        {
            if (slider == null)
            {
                return;
            }

            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = false;
        }

        private int FindResolutionIndex(Resolution target)
        {
            for (int i = 0; i < _resolutions.Count; i++)
            {
                var res = _resolutions[i];
                if (res.width == target.width && res.height == target.height && (int)res.refreshRateRatio.value == (int)target.refreshRateRatio.value)
                {
                    return i;
                }
            }

            return Mathf.Clamp(_resolutions.FindIndex(r => r.width == target.width && r.height == target.height), 0, Mathf.Max(_resolutions.Count - 1, 0));
        }

        private string FormatResolutionLabel(Resolution resolution)
        {
            string refresh = (int)resolution.refreshRateRatio.value > 0 ? $" @ {(int)resolution.refreshRateRatio.value}Hz" : string.Empty;
            return $"{resolution.width} x {resolution.height}{refresh}";
        }
    }
}
