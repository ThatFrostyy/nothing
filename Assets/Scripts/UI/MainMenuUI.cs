using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace FF
{
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private SceneFlowController sceneFlow;
        [SerializeField] private TMP_Text killsStatText;
        [SerializeField] private TMP_Text topWaveStatText;
        [SerializeField] private TMP_Text leaderboardText;
        [SerializeField] private string unavailableText = "Unavailable";
        [Header("Map Selection")]
        [SerializeField] private TMP_Text mapNameText;
        [SerializeField] private TMP_Text mapDescriptionText;
        [SerializeField] private List<MapDefinition> availableMaps = new();

#if !DISABLESTEAMWORKS
        private const string KillStatName = "total_kills";
        private const string TopWaveStatName = "top_wave_survived";
        private const string KillLeaderboardName = "kills";

        private Callback<UserStatsReceived_t> _userStatsReceived;
        private CallResult<LeaderboardFindResult_t> _leaderboardFindResult;
        private CallResult<LeaderboardScoresDownloaded_t> _leaderboardScoresDownloaded;

        private SteamLeaderboard_t _killLeaderboard;
#endif

        private int _mapIndex;
        private int? _cachedKills;
        private int? _cachedTopWave;

        void Awake()
        {
            if (!sceneFlow)
            {
                sceneFlow = FindAnyObjectByType<SceneFlowController>();
            }
        }

        void OnEnable()
        {
            MapSelectionState.OnMapChanged += HandleMapChanged;
            SyncMapIndexWithSelection();
            RefreshMapText();
            CharacterUnlockProgress.OnProgressUpdated += HandleProgressUpdated;
            RefreshLocalStats();

#if !DISABLESTEAMWORKS
            if (!SteamManager.Initialized)
            {
                return;
            }

            _userStatsReceived = Callback<UserStatsReceived_t>.Create(HandleStatsReceived);
            _leaderboardFindResult = CallResult<LeaderboardFindResult_t>.Create(HandleKillLeaderboardFound);
            _leaderboardScoresDownloaded = CallResult<LeaderboardScoresDownloaded_t>.Create(HandleLeaderboardScoresDownloaded);

            // Immediately request the latest stats and leaderboard when the menu appears.
            RefreshSteamStats();
#endif
        }

        public void StartGame()
        {
            MapDefinition selectedMap = ResolveSelectedMap();
            if (selectedMap != null)
            {
                MapSelectionState.SetSelection(selectedMap);
            }

            if (sceneFlow)
            {
                sceneFlow.LoadGameplayScene();
            }
        }

        public void NextMap()
        {
            StepMap(1);
        }

        public void PreviousMap()
        {
            StepMap(-1);
        }

        private void StepMap(int delta)
        {
            if (availableMaps.Count == 0)
            {
                return;
            }

            _mapIndex = Mathf.FloorToInt(Mathf.Repeat(_mapIndex + delta, availableMaps.Count));
            MapDefinition map = ResolveSelectedMap();
            MapSelectionState.SetSelection(map);
            RefreshMapText();
        }

        private void SyncMapIndexWithSelection()
        {
            if (availableMaps.Count == 0)
            {
                _mapIndex = 0;
                return;
            }

            int foundIndex = availableMaps.IndexOf(MapSelectionState.SelectedMap);
            _mapIndex = foundIndex >= 0 ? foundIndex : Mathf.Clamp(_mapIndex, 0, availableMaps.Count - 1);
        }

        public void QuitGame()
        {
            SceneFlowController.QuitGame();
        }

        public void RefreshSteamStats()
        {
#if !DISABLESTEAMWORKS
            if (!SteamManager.Initialized)
            {
                ShowUnavailableText();
                return;
            }

            SteamUserStats.RequestUserStats(SteamUser.GetSteamID());
            RequestKillLeaderboard();
#else
            ShowUnavailableText();
#endif
        }

        void OnDisable()
        {
            MapSelectionState.OnMapChanged -= HandleMapChanged;
            CharacterUnlockProgress.OnProgressUpdated -= HandleProgressUpdated;

#if !DISABLESTEAMWORKS
            _userStatsReceived = null;
            _leaderboardFindResult = null;
            _leaderboardScoresDownloaded = null;
            _killLeaderboard = default;
#endif
        }
#if !DISABLESTEAMWORKS
        private void HandleStatsReceived(UserStatsReceived_t result)
        {
            if (result.m_eResult != EResult.k_EResultOK)
            {
                ShowUnavailableText();
                return;
            }

            if (SteamUserStats.GetStat(KillStatName, out int kills))
            {
                _cachedKills = kills;
                UpdateKillsText(_cachedKills);
            }
            else
            {
                _cachedKills = null;
                UpdateKillsText(_cachedKills);
            }

            if (SteamUserStats.GetStat(TopWaveStatName, out int topWave))
            {
                _cachedTopWave = topWave;
                UpdateTopWaveText(_cachedTopWave);
            }
            else
            {
                _cachedTopWave = null;
                UpdateTopWaveText(_cachedTopWave);
            }
        }
#endif

        private void RequestKillLeaderboard()
        {
            if (_killLeaderboard.m_SteamLeaderboard != 0)
            {
                DownloadKillLeaderboardEntries();
                return;
            }

            SteamAPICall_t handle = SteamUserStats.FindLeaderboard(KillLeaderboardName);
            _leaderboardFindResult.Set(handle, HandleKillLeaderboardFound);
        }

        private void HandleKillLeaderboardFound(LeaderboardFindResult_t result, bool failure)
        {
            if (failure || result.m_bLeaderboardFound == 0 || result.m_hSteamLeaderboard.m_SteamLeaderboard == 0)
            {
                UpdateLeaderboardText(null);
                return;
            }

            _killLeaderboard = result.m_hSteamLeaderboard;
            DownloadKillLeaderboardEntries();
        }

        private void DownloadKillLeaderboardEntries()
        {
            if (_killLeaderboard.m_SteamLeaderboard == 0)
            {
                UpdateLeaderboardText(null);
                return;
            }

            SteamAPICall_t handle = SteamUserStats.DownloadLeaderboardEntries(
                _killLeaderboard,
                ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal,
                1,
                3);
            _leaderboardScoresDownloaded.Set(handle, HandleLeaderboardScoresDownloaded);
        }

        private void HandleLeaderboardScoresDownloaded(LeaderboardScoresDownloaded_t callback, bool failure)
        {
            if (failure || callback.m_cEntryCount <= 0)
            {
                UpdateLeaderboardText(null);
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine("Top 3");
            int entryCount = Mathf.Min(3, callback.m_cEntryCount);
            for (int i = 0; i < entryCount; i++)
            {
                if (!SteamUserStats.GetDownloadedLeaderboardEntry(callback.m_hSteamLeaderboardEntries, i, out LeaderboardEntry_t entry, null, 0))
                {
                    continue;
                }

                string personaName = SteamFriends.GetFriendPersonaName(entry.m_steamIDUser);
                builder.AppendLine($"{entry.m_nGlobalRank}. {personaName} - {entry.m_nScore}");
            }

            UpdateLeaderboardText(builder.ToString());
        }

        private void UpdateKillsText(int? kills)
        {
            if (killsStatText)
            {
                string killLabel = kills.HasValue ? kills.Value.ToString() : unavailableText;
                int bosses = CharacterUnlockProgress.TotalBossesKilled;
                int crates = CharacterUnlockProgress.TotalCratesDestroyed;
                killsStatText.text = $"Kills: {killLabel}\nBosses: {bosses}\nCrates: {crates}";
            }
        }

        private void UpdateTopWaveText(int? wave)
        {
            if (topWaveStatText)
            {
                string waveLabel = wave.HasValue ? wave.Value.ToString() : unavailableText;
                string weaponLabel = GetMostUsedWeaponLabel();
                string longestTimeLabel = GetLongestTimeLabel();
                topWaveStatText.text = $"Top Wave: {waveLabel}\nMost Used: {weaponLabel}\nLongest Time: {longestTimeLabel}";
            }
        }

        private void UpdateLeaderboardText(string text)
        {
            if (leaderboardText)
            {
                leaderboardText.text = string.IsNullOrWhiteSpace(text) ? unavailableText : text.TrimEnd();
            }
        }

        private void HandleMapChanged(MapDefinition _)
        {
            SyncMapIndexWithSelection();
            RefreshMapText();
        }

        private MapDefinition ResolveSelectedMap()
        {
            if (availableMaps.Count == 0)
            {
                return null;
            }

            _mapIndex = Mathf.Clamp(_mapIndex, 0, availableMaps.Count - 1);
            return availableMaps[_mapIndex];
        }

        private void RefreshMapText()
        {
            MapDefinition map = ResolveSelectedMap();
            if (mapNameText)
            {
                mapNameText.text = map ? map.MapName : unavailableText;
            }

            if (mapDescriptionText)
            {
                mapDescriptionText.text = map && !string.IsNullOrWhiteSpace(map.Description)
                    ? map.Description
                    : string.Empty;
            }
        }

        private void ShowUnavailableText()
        {
            _cachedKills = null;
            _cachedTopWave = null;
            RefreshLocalStats();

            if (leaderboardText)
            {
                leaderboardText.text = unavailableText;
            }
        }

        private void HandleProgressUpdated()
        {
            RefreshLocalStats();
        }

        private void RefreshLocalStats()
        {
            UpdateKillsText(_cachedKills);
            UpdateTopWaveText(_cachedTopWave);
        }

        private string GetMostUsedWeaponLabel()
        {
            if (CharacterUnlockProgress.TryGetMostUsedWeapon(out string weaponName, out int killCount))
            {
                return killCount > 0 ? $"{weaponName} ({killCount} kills)" : weaponName;
            }

            return unavailableText;
        }

        private string GetLongestTimeLabel()
        {
            float longestTime = RunStatsProgress.LongestTimeSurvivedSeconds;
            if (longestTime <= 0f)
            {
                return unavailableText;
            }

            return FormatTime(longestTime);
        }

        private static string FormatTime(float seconds)
        {
            if (seconds <= 0f)
            {
                return "0:00";
            }

            TimeSpan span = TimeSpan.FromSeconds(seconds);
            if (span.TotalHours >= 1)
            {
                return $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}";
            }

            return $"{span.Minutes}:{span.Seconds:00}";
        }
    }
}
