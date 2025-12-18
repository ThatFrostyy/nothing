using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FF
{
    public static class GameVideoSettings
    {
        private const string QualityPrefKey = "Video.QualityLevel";
        private const string ResolutionWidthPrefKey = "Video.Resolution.Width";
        private const string ResolutionHeightPrefKey = "Video.Resolution.Height";
        private const string ResolutionRefreshPrefKey = "Video.Resolution.Refresh";
        private const string VsyncPrefKey = "Video.VSync";
        private const string PostProcessingPrefKey = "Video.PostProcessing";
        private const string BloomPrefKey = "Video.Bloom";
        private const string MotionBlurPrefKey = "Video.MotionBlur";

        private const float DefaultBloomIntensity = 1f;
        private const float DefaultMotionBlurIntensity = 0f;

        private static readonly Dictionary<int, bool> _originalVolumeStates = new();

        private static bool _initialized;
        private static List<Resolution> _availableResolutions = new();

        public static int QualityLevel { get; private set; }
        public static Resolution Resolution { get; private set; }
        public static bool VSyncEnabled { get; private set; }
        public static bool PostProcessingEnabled { get; private set; }
        public static float BloomIntensity { get; private set; }
        public static float MotionBlurIntensity { get; private set; }

        public static IReadOnlyList<Resolution> AvailableResolutions => _availableResolutions;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            CacheResolutions();
            LoadPreferences();
            ApplyDisplaySettings();
            ApplyPostProcessingSettings();
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        public static void SetQualityLevel(int index)
        {
            EnsureInitialized();
            int clamped = Mathf.Clamp(index, 0, Mathf.Max(QualitySettings.names.Length - 1, 0));
            if (QualityLevel == clamped)
            {
                return;
            }

            QualityLevel = clamped;
            QualitySettings.SetQualityLevel(QualityLevel, true);
            PlayerPrefs.SetInt(QualityPrefKey, QualityLevel);
            PlayerPrefs.Save();
        }

        public static void SetResolution(Resolution resolution)
        {
            EnsureInitialized();
            if (resolution.width <= 0 || resolution.height <= 0)
            {
                return;
            }

            Resolution = resolution;
            ApplyResolution(resolution);
            PlayerPrefs.SetInt(ResolutionWidthPrefKey, resolution.width);
            PlayerPrefs.SetInt(ResolutionHeightPrefKey, resolution.height);
            PlayerPrefs.SetInt(ResolutionRefreshPrefKey, resolution.refreshRate);
            PlayerPrefs.Save();
        }

        public static void SetVSync(bool enabled)
        {
            EnsureInitialized();
            if (VSyncEnabled == enabled)
            {
                return;
            }

            VSyncEnabled = enabled;
            QualitySettings.vSyncCount = enabled ? 1 : 0;
            PlayerPrefs.SetInt(VsyncPrefKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void SetPostProcessingEnabled(bool enabled)
        {
            EnsureInitialized();
            if (PostProcessingEnabled == enabled)
            {
                return;
            }

            PostProcessingEnabled = enabled;
            PlayerPrefs.SetInt(PostProcessingPrefKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
            ApplyPostProcessingSettings();
        }

        public static void SetBloomIntensity(float value)
        {
            EnsureInitialized();
            float clamped = Mathf.Max(0f, value);
            if (Mathf.Approximately(BloomIntensity, clamped))
            {
                return;
            }

            BloomIntensity = clamped;
            PlayerPrefs.SetFloat(BloomPrefKey, BloomIntensity);
            PlayerPrefs.Save();
            ApplyPostProcessingSettings();
        }

        public static void SetMotionBlurIntensity(float value)
        {
            EnsureInitialized();
            float clamped = Mathf.Max(0f, value);
            if (Mathf.Approximately(MotionBlurIntensity, clamped))
            {
                return;
            }

            MotionBlurIntensity = clamped;
            PlayerPrefs.SetFloat(MotionBlurPrefKey, MotionBlurIntensity);
            PlayerPrefs.Save();
            ApplyPostProcessingSettings();
        }

        private static void EnsureInitialized()
        {
            if (!_initialized)
            {
                Initialize();
            }
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyPostProcessingSettings();
        }

        private static void LoadPreferences()
        {
            QualityLevel = Mathf.Clamp(PlayerPrefs.GetInt(QualityPrefKey, QualitySettings.GetQualityLevel()), 0, Mathf.Max(QualitySettings.names.Length - 1, 0));
            VSyncEnabled = PlayerPrefs.GetInt(VsyncPrefKey, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
            PostProcessingEnabled = PlayerPrefs.GetInt(PostProcessingPrefKey, 1) == 1;
            BloomIntensity = Mathf.Max(0f, PlayerPrefs.GetFloat(BloomPrefKey, DefaultBloomIntensity));
            MotionBlurIntensity = Mathf.Max(0f, PlayerPrefs.GetFloat(MotionBlurPrefKey, DefaultMotionBlurIntensity));

            int storedWidth = PlayerPrefs.GetInt(ResolutionWidthPrefKey, Screen.currentResolution.width);
            int storedHeight = PlayerPrefs.GetInt(ResolutionHeightPrefKey, Screen.currentResolution.height);
            int storedRefresh = PlayerPrefs.GetInt(ResolutionRefreshPrefKey, Screen.currentResolution.refreshRate);
            Resolution = ResolveResolution(storedWidth, storedHeight, storedRefresh);
        }

        private static void CacheResolutions()
        {
            _availableResolutions = Screen.resolutions
                .GroupBy(r => (r.width, r.height, r.refreshRate))
                .Select(g => g.First())
                .OrderBy(r => r.width)
                .ThenBy(r => r.height)
                .ThenBy(r => r.refreshRate)
                .ToList();

            if (_availableResolutions.Count == 0)
            {
                _availableResolutions.Add(Screen.currentResolution);
            }
        }

        private static Resolution ResolveResolution(int width, int height, int refreshRate)
        {
            EnsureInitialized();

            foreach (var res in _availableResolutions)
            {
                if (res.width == width && res.height == height && res.refreshRate == refreshRate)
                {
                    return res;
                }
            }

            foreach (var res in _availableResolutions)
            {
                if (res.width == width && res.height == height)
                {
                    return res;
                }
            }

            return Screen.currentResolution;
        }

        private static void ApplyDisplaySettings()
        {
            QualitySettings.SetQualityLevel(QualityLevel, true);
            QualitySettings.vSyncCount = VSyncEnabled ? 1 : 0;
            ApplyResolution(Resolution);
        }

        private static void ApplyResolution(Resolution resolution)
        {
            if (resolution.width <= 0 || resolution.height <= 0)
            {
                return;
            }

            Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreenMode, resolution.refreshRate);
        }

        private static void ApplyPostProcessingSettings()
        {
            EnsureInitialized();
            var volumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            var seen = new HashSet<int>();
            foreach (var volume in volumes)
            {
                if (volume == null)
                {
                    continue;
                }

                int id = volume.GetInstanceID();
                seen.Add(id);

                if (!_originalVolumeStates.ContainsKey(id))
                {
                    _originalVolumeStates[id] = volume.enabled;
                }

                volume.enabled = PostProcessingEnabled && _originalVolumeStates[id];

                VolumeProfile profile = volume.sharedProfile != null ? volume.sharedProfile : volume.profile;
                if (profile == null)
                {
                    continue;
                }

                ApplyBloomSettings(profile);
                ApplyMotionBlurSettings(profile);
            }

            var orphanedIds = _originalVolumeStates.Keys.Where(id => !seen.Contains(id)).ToList();
            foreach (var id in orphanedIds)
            {
                _originalVolumeStates.Remove(id);
            }
        }

        private static void ApplyBloomSettings(VolumeProfile profile)
        {
            if (profile.TryGet(out Bloom bloom))
            {
                bloom.active = PostProcessingEnabled;
                bloom.intensity.value = PostProcessingEnabled ? BloomIntensity : 0f;
            }
        }

        private static void ApplyMotionBlurSettings(VolumeProfile profile)
        {
            if (profile.TryGet(out MotionBlur motionBlur))
            {
                motionBlur.quality.value = MotionBlurQuality.High;
                motionBlur.intensity.value = MotionBlurIntensity;
                motionBlur.active = PostProcessingEnabled && MotionBlurIntensity > 0f;
            }
        }
    }
}
