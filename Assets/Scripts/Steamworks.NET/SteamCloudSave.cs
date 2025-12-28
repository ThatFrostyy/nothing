using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace FF
{
    public static class SteamCloudSave
    {
        private const string CloudFileName = "ff_cloud_prefs.json";

        private enum PrefValueType
        {
            Int = 0,
            Float = 1,
            String = 2
        }

        [Serializable]
        private class CloudPrefEntry
        {
            public string Key;
            public PrefValueType Type;
            public string Value;
        }

        [Serializable]
        private class CloudPrefPayload
        {
            public int Version = 1;
            public List<CloudPrefEntry> Entries = new();
        }

        private static readonly (string Key, PrefValueType Type)[] PrefKeys =
        {
            ("InputBindingOverrides", PrefValueType.String),
            ("Video.QualityLevel", PrefValueType.Int),
            ("Video.Resolution.Width", PrefValueType.Int),
            ("Video.Resolution.Height", PrefValueType.Int),
            ("Video.Resolution.Refresh", PrefValueType.Int),
            ("Video.VSync", PrefValueType.Int),
            ("Video.PostProcessing", PrefValueType.Int),
            ("Video.Bloom", PrefValueType.Float),
            ("Video.MotionBlur", PrefValueType.Float),
            ("MusicVolume", PrefValueType.Float),
            ("SfxVolume", PrefValueType.Float),
            ("AmbienceVolume", PrefValueType.Float),
            ("FF_CharacterProgression_v1", PrefValueType.String),
            ("FF_CharacterUnlockProgress_v1", PrefValueType.String),
            ("FF_RunStats_v1", PrefValueType.String)
        };

        public static void SaveToCloud()
        {
#if !DISABLESTEAMWORKS
            if (!SteamManager.Initialized)
            {
                return;
            }

            if (!SteamRemoteStorage.IsCloudEnabledForAccount() || !SteamRemoteStorage.IsCloudEnabledForApp())
            {
                return;
            }

            var payload = new CloudPrefPayload();
            for (int i = 0; i < PrefKeys.Length; i++)
            {
                (string key, PrefValueType type) = PrefKeys[i];
                if (!PlayerPrefs.HasKey(key))
                {
                    continue;
                }

                string value = type switch
                {
                    PrefValueType.Int => PlayerPrefs.GetInt(key).ToString(CultureInfo.InvariantCulture),
                    PrefValueType.Float => PlayerPrefs.GetFloat(key).ToString(CultureInfo.InvariantCulture),
                    PrefValueType.String => PlayerPrefs.GetString(key, string.Empty),
                    _ => string.Empty
                };

                payload.Entries.Add(new CloudPrefEntry
                {
                    Key = key,
                    Type = type,
                    Value = value
                });
            }

            string json = JsonUtility.ToJson(payload);
            byte[] data = Encoding.UTF8.GetBytes(json);
            SteamRemoteStorage.FileWrite(CloudFileName, data, data.Length);
#endif
        }

        public static void LoadFromCloud()
        {
#if !DISABLESTEAMWORKS
            if (!SteamManager.Initialized)
            {
                return;
            }

            if (!SteamRemoteStorage.IsCloudEnabledForAccount() || !SteamRemoteStorage.IsCloudEnabledForApp())
            {
                return;
            }

            if (!SteamRemoteStorage.FileExists(CloudFileName))
            {
                SaveToCloud();
                return;
            }

            int size = SteamRemoteStorage.GetFileSize(CloudFileName);
            if (size <= 0)
            {
                return;
            }

            byte[] data = new byte[size];
            int bytesRead = SteamRemoteStorage.FileRead(CloudFileName, data, size);
            if (bytesRead <= 0)
            {
                return;
            }

            string json = Encoding.UTF8.GetString(data, 0, bytesRead);
            CloudPrefPayload payload = JsonUtility.FromJson<CloudPrefPayload>(json);
            if (payload?.Entries == null)
            {
                return;
            }

            for (int i = 0; i < payload.Entries.Count; i++)
            {
                CloudPrefEntry entry = payload.Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                {
                    continue;
                }

                switch (entry.Type)
                {
                    case PrefValueType.Int:
                        if (int.TryParse(entry.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
                        {
                            PlayerPrefs.SetInt(entry.Key, intValue);
                        }
                        break;
                    case PrefValueType.Float:
                        if (float.TryParse(entry.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue))
                        {
                            PlayerPrefs.SetFloat(entry.Key, floatValue);
                        }
                        break;
                    case PrefValueType.String:
                        PlayerPrefs.SetString(entry.Key, entry.Value ?? string.Empty);
                        break;
                }
            }

            PlayerPrefs.Save();

            // Standard Audio/Video Reloads
            GameVideoSettings.ReloadPreferences();
            GameAudioSettings.ReloadPreferences();
            MusicManager.RefreshFromPrefs();

            // -----------------------------------------------------------------------
            // CRITICAL FIX: Reload cached systems
            // These systems may have initialized before SteamManager.Awake.
            // We force them to drop their cache and re-read the updated PlayerPrefs.
            // -----------------------------------------------------------------------

            // Note: Ensure the following methods exist in your codebase or alias them 
            // to the appropriate cache-clearing logic.
            InputBindingManager.Reload();
            CharacterProgressionService.Reload();
            CharacterUnlockProgress.Reload();
            RunStatsProgress.Reload();
#endif
        }
    }
}