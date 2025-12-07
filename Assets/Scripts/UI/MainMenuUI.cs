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

#if !DISABLESTEAMWORKS
            if (!SteamManager.Initialized)
            {
                return;
            }

            _userStatsReceived = Callback<UserStatsReceived_t>.Create(HandleStatsReceived);
            _leaderboardFindResult = CallResult<LeaderboardFindResult_t>.Create(HandleKillLeaderboardFound);
            _leaderboardScoresDownloaded = CallResult<LeaderboardScoresDownloaded_t>.Create(HandleLeaderboardScoresDownloaded);
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

#if !DISABLESTEAMWORKS
            _userStatsReceived = null;
            _leaderboardFindResult = null;
            _leaderboardScoresDownloaded = null;
            _killLeaderboard = default;
#endif
        }
        private void HandleStatsReceived(UserStatsReceived_t result)
        {
            if (result.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogWarning($"[Steam] Failed to receive stats: {result.m_eResult}");
                ShowUnavailableText();
                return;
            }

            if (SteamUserStats.GetStat(KillStatName, out int kills))
            {
                Debug.Log($"[Steam] Received total kills: {kills}");
                UpdateKillsText(kills);
            }
            else
            {
                UpdateKillsText(null);
            }

            if (SteamUserStats.GetStat(TopWaveStatName, out int topWave))
            {
                Debug.Log($"[Steam] Received top wave survived: {topWave}");
                UpdateTopWaveText(topWave);
            }
            else
            {
                UpdateTopWaveText(null);
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
                Debug.LogWarning("[Steam] Failed to locate kill leaderboard");
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
                10);
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
            for (int i = 0; i < callback.m_cEntryCount; i++)
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
                killsStatText.text = kills.HasValue ? kills.Value.ToString() : unavailableText;
            }
        }

        private void UpdateTopWaveText(int? wave)
        {
            if (topWaveStatText)
            {
                topWaveStatText.text = wave.HasValue ? wave.Value.ToString() : unavailableText;
            }
        }

        private void UpdateLeaderboardText(string text)
        {
            if (leaderboardText)
            {
                leaderboardText.text = string.IsNullOrWhiteSpace(text) ? unavailableText : text.TrimEnd();
            }
        }
#endif

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
            if (killsStatText)
            {
                killsStatText.text = unavailableText;
            }

            if (topWaveStatText)
            {
                topWaveStatText.text = unavailableText;
            }

            if (leaderboardText)
            {
                leaderboardText.text = unavailableText;
            }
        }
    }
}
