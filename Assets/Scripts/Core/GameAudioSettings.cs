using System;
using UnityEngine;

namespace FF
{
    public static class GameAudioSettings
    {
        private const string SfxVolumePrefKey = "SfxVolume";
        private const string AmbienceVolumePrefKey = "AmbienceVolume";

        private const float DefaultSfxVolume = 1f;
        private const float DefaultAmbienceVolume = 1f;

        public static float SfxVolume { get; private set; } = DefaultSfxVolume;
        public static float AmbienceVolume { get; private set; } = DefaultAmbienceVolume;

        public static event Action<float> OnSfxVolumeChanged;
        public static event Action<float> OnAmbienceVolumeChanged;

        static GameAudioSettings()
        {
            LoadPreferences();
        }

        public static void SetSfxVolume(float value)
        {
            float clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(SfxVolume, clamped))
            {
                return;
            }

            SfxVolume = clamped;
            PlayerPrefs.SetFloat(SfxVolumePrefKey, SfxVolume);
            PlayerPrefs.Save();
            OnSfxVolumeChanged?.Invoke(SfxVolume);
        }

        public static void SetAmbienceVolume(float value)
        {
            float clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(AmbienceVolume, clamped))
            {
                return;
            }

            AmbienceVolume = clamped;
            PlayerPrefs.SetFloat(AmbienceVolumePrefKey, AmbienceVolume);
            PlayerPrefs.Save();
            OnAmbienceVolumeChanged?.Invoke(AmbienceVolume);
        }

        private static void LoadPreferences()
        {
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumePrefKey, DefaultSfxVolume));
            AmbienceVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(AmbienceVolumePrefKey, DefaultAmbienceVolume));
        }
    }
}
