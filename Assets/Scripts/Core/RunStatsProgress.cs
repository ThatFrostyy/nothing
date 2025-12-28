using System;
using UnityEngine;

namespace FF
{
    public static class RunStatsProgress
    {
        private const string PlayerPrefsKey = "FF_RunStats_v1";

        private static RunStatsData _data;
        private static bool _loaded;

        public static float LongestTimeSurvivedSeconds
        {
            get
            {
                EnsureLoaded();
                return _data.LongestTimeSurvivedSeconds;
            }
        }

        /// <summary>
        /// Forces the system to discard cached data and re-read from PlayerPrefs.
        /// Call this after applying a Steam Cloud save.
        /// </summary>
        public static void Reload()
        {
            _loaded = false;
            _data = null;
            EnsureLoaded();
        }

        public static void RecordRunTime(float seconds)
        {
            if (seconds <= 0f)
            {
                return;
            }

            EnsureLoaded();
            if (seconds <= _data.LongestTimeSurvivedSeconds)
            {
                return;
            }

            _data.LongestTimeSurvivedSeconds = seconds;
            Save();
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    _data = JsonUtility.FromJson<RunStatsData>(json);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to load run stats: {e.Message}");
                }
            }

            if (_data == null)
            {
                _data = new RunStatsData();
            }
        }

        private static void Save()
        {
            string json = JsonUtility.ToJson(_data);
            PlayerPrefs.SetString(PlayerPrefsKey, json);
            PlayerPrefs.Save();
            SteamCloudSave.SaveToCloud();
        }
    }

    [Serializable]
    public class RunStatsData
    {
        public float LongestTimeSurvivedSeconds;
    }
}