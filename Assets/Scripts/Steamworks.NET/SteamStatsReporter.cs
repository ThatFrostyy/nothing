using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace FF
{
    [DisallowMultipleComponent]
    public class SteamStatsReporter : MonoBehaviour
    {
#if !DISABLESTEAMWORKS
        [Header ("Stats")]
        [SerializeField] private const string KillStatName = "total_kills";
        [SerializeField] private const string TopWaveStatName = "top_wave_survived";

        [Header("Leaderboards")]
        [SerializeField] private const string KillLeaderboardName = "kills";

        [Header("Achievements")]
        [SerializeField] private List<WaveAchievement> waveAchievements = new List<WaveAchievement>()
        {
            new WaveAchievement { waveRequired = 10, achievementId = "WAVE_10" },
            new WaveAchievement { waveRequired = 20, achievementId = "WAVE_20" },
            new WaveAchievement { waveRequired = 30, achievementId = "WAVE_30" },
            new WaveAchievement { waveRequired = 40, achievementId = "WAVE_40" },
            new WaveAchievement { waveRequired = 50, achievementId = "WAVE_50" }
        };

        private readonly struct WeaponAchievementConfig
        {
            public WeaponAchievementConfig(
                string weaponKey,
                string statName,
                (int Threshold, string AchievementName)[] achievementThresholds)
            {
                WeaponKey = weaponKey;
                StatName = statName;
                AchievementThresholds = achievementThresholds;
            }

            public string WeaponKey { get; }
            public string StatName { get; }
            public (int Threshold, string AchievementName)[] AchievementThresholds { get; }
        }

        private readonly Dictionary<string, WeaponAchievementConfig> _weaponAchievementConfigs = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Thompson"] = new WeaponAchievementConfig(
                "Thompson",
                "kills_thompson",
                new (int, string)[]
                {
                    (100, "THOMPSON_100"),
                    (500, "THOMPSON_500"),
                    (1000, "THOMPSON_1000")
                }),
            ["BAR M-1918"] = new WeaponAchievementConfig(
                "BAR M-1918",
                "kills_bar",
                new (int, string)[]
                {
                    (100, "BAR_100"),
                    (500, "BAR_500"),
                    (1000, "BAR_1000")
                }),
            ["M12 Shotgun"] = new WeaponAchievementConfig(
                "M12 Shotgun",
                "kills_m12",
                new (int, string)[]
                {
                    (100, "M12_100"),
                    (500, "M12_500"),
                    (1000, "M12_1000")
                }),
            ["M1 Garand"] = new WeaponAchievementConfig(
                "M1 Garand",
                "kills_m1",
                new (int, string)[]
                {
                    (100, "M1_100"),
                    (500, "M1_500"),
                    (1000, "M1_1000")
                }),
            ["M2 Carbine"] = new WeaponAchievementConfig(
                "M2 Carbine",
                "kills_m2",
                new (int, string)[]
                {
                    (100, "M2_100"),
                    (500, "M2_500"),
                    (1000, "M2_1000")
                }),
            ["M1A1 Bazooka"] = new WeaponAchievementConfig(
                "M1A1 Bazooka",
                "kills_bazooka",
                new (int, string)[]
                {
                    (100, "BAZOOKA_100"),
                    (500, "BAZOOKA_500"),
                    (1000, "BAZOOKA_1000")
                })
        };

        private readonly Dictionary<Weapon, int> _weaponKills = new();
        private readonly Dictionary<string, int> _weaponKillTotals = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _pendingWeaponKillIncrements = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _unlockedWeaponAchievements = new();
        private readonly HashSet<Health> _trackedPlayerHealth = new();
        private Callback<UserStatsReceived_t> _userStatsReceived;
        private Callback<UserStatsStored_t> _userStatsStored;
        private CallResult<LeaderboardFindResult_t> _killLeaderboardFindResult;
        private CallResult<LeaderboardScoreUploaded_t> _killScoreUploadedResult;
        private SteamLeaderboard_t _killLeaderboard;
        private int _lastKillCount;
        private int _killStatBase;
        private int _highestWave;
        private bool _gameManagerHooked;//
        private bool _statsReady;
        private void Start()
        {
            SteamUserStats.RequestUserStats(SteamUser.GetSteamID());
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            if (!SteamManager.Initialized)
            {
                enabled = false;
                return;
            }

            _userStatsReceived = Callback<UserStatsReceived_t>.Create(HandleStatsReceived);
            _userStatsStored = Callback<UserStatsStored_t>.Create(HandleStatsStored);
            _killLeaderboardFindResult = CallResult<LeaderboardFindResult_t>.Create(HandleKillLeaderboardFound);
            _killScoreUploadedResult = CallResult<LeaderboardScoreUploaded_t>.Create(HandleKillScoreUploaded);

            HookGameManager();
            Enemy.OnAnyEnemyKilledByWeapon += HandleEnemyKilledByWeapon;
            PlayerController.OnPlayerReady += HandlePlayerReady;
        }

        private void OnDisable()
        {
            if (_gameManagerHooked && GameManager.I != null)
            {
                GameManager.I.OnKillCountChanged -= HandleKillCountChanged;
                GameManager.I.OnWaveStarted -= HandleWaveStarted;
            }

            Enemy.OnAnyEnemyKilledByWeapon -= HandleEnemyKilledByWeapon;
            PlayerController.OnPlayerReady -= HandlePlayerReady;
            foreach (Health health in _trackedPlayerHealth)
            {
                if (health != null)
                {
                    health.OnDeath -= HandlePlayerDeath;
                }
            }
            _trackedPlayerHealth.Clear();
            _userStatsReceived = null;
            _userStatsStored = null;
            _killLeaderboardFindResult = null;
            _killScoreUploadedResult = null;
            _gameManagerHooked = false;
            _statsReady = false;
            _weaponKillTotals.Clear();
            _pendingWeaponKillIncrements.Clear();
            _unlockedWeaponAchievements.Clear();
        }

        private void Update()
        {
            if (!_gameManagerHooked)
            {
                HookGameManager();
            }
        }

        private void HookGameManager()
        {
            if (_gameManagerHooked || GameManager.I == null)
            {
                return;
            }

            GameManager.I.OnKillCountChanged += HandleKillCountChanged;
            GameManager.I.OnWaveStarted += HandleWaveStarted;

            _lastKillCount = GameManager.I.KillCount;
            _highestWave = Mathf.Max(_highestWave, GameManager.I.Wave);
            _gameManagerHooked = true;
        }

        private void EnsureKillLeaderboard()
        {
            if (!_statsReady || _killLeaderboard.m_SteamLeaderboard != 0)
            {
                return;
            }

            SteamAPICall_t handle = SteamUserStats.FindOrCreateLeaderboard(
                KillLeaderboardName,
                ELeaderboardSortMethod.k_ELeaderboardSortMethodDescending,
                ELeaderboardDisplayType.k_ELeaderboardDisplayTypeNumeric);
            _killLeaderboardFindResult.Set(handle, HandleKillLeaderboardFound);
        }

        private void HandleStatsStored(UserStatsStored_t callback)
        {
            if (callback.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogWarning($"[Steam] Failed to store stats: {callback.m_eResult}");
            }
        }

        private void HandleKillCountChanged(int kills)
        {
            if (kills < _lastKillCount)
            {
                _killStatBase += _lastKillCount;
            }

            _lastKillCount = kills;
        }

        private void HandleWaveStarted(int wave)
        {
            _highestWave = Mathf.Max(_highestWave, wave);

            PushCoreStats();
            PushKillLeaderboardScore();
            TryUnlockWaveAchievements();
        }

        private void HandleKillLeaderboardFound(LeaderboardFindResult_t result, bool failure)
        {
            if (failure || result.m_bLeaderboardFound == 0 || result.m_hSteamLeaderboard.m_SteamLeaderboard == 0)
            {
                Debug.LogWarning("[Steam] Failed to locate kill leaderboard");
                return;
            }

            _killLeaderboard = result.m_hSteamLeaderboard;
            PushKillLeaderboardScore();
        }

        private void HandleKillScoreUploaded(LeaderboardScoreUploaded_t result, bool failure)
        {
            if (failure || result.m_bSuccess == 0)
            {
                Debug.LogWarning("[Steam] Failed to upload kill leaderboard score");
                return;
            }

            if (result.m_bScoreChanged == 1)
            {
                Debug.Log($"[Steam] Leaderboard updated!");
            }
            else
            {
                Debug.Log("[Steam] Score sent, but existing high score was better.");
            }
        }

        private void HandleStatsReceived(UserStatsReceived_t callback)
        {
            if (callback.m_eResult == EResult.k_EResultOK)
            {
                _statsReady = true;

                if (SteamUserStats.GetStat(KillStatName, out int storedKills))
                {
                    _killStatBase = Mathf.Max(0, storedKills);
                }

                if (SteamUserStats.GetStat(TopWaveStatName, out int storedTopWave))
                {
                    _highestWave = Mathf.Max(_highestWave, storedTopWave);
                }

                SyncWeaponKillStats();

                EnsureKillLeaderboard();
                PushCoreStats();
                PushKillLeaderboardScore();
            }
            else
            {
                Debug.LogWarning($"[Steam] Failed to receive stats: {callback.m_eResult}");
            }
        }
       

        private void HandleEnemyKilledByWeapon(Enemy enemy, Weapon weapon)
        {
            if (!weapon)
            {
                return;
            }

            if (_weaponKills.ContainsKey(weapon))
            {
                _weaponKills[weapon]++;
            }
            else
            {
                _weaponKills.Add(weapon, 1);
            }

            PushFavoriteWeapons();
            RegisterWeaponKill(weapon);
        }

        private void HandlePlayerReady(PlayerController controller)
        {
            Health health = controller ? controller.GetComponentInChildren<Health>() : null;
            if (health == null)
            {
                return;
            }

            if (_trackedPlayerHealth.Add(health))
            {
                health.OnDeath += HandlePlayerDeath;
            }
        }

        private void HandlePlayerDeath()
        {
            PushCoreStats();
            PushKillLeaderboardScore();
        }

        private void PushCoreStats()
        {
            if (!_statsReady)
                return;

            SteamUserStats.SetStat(KillStatName, _killStatBase + _lastKillCount);
            SteamUserStats.SetStat(TopWaveStatName, _highestWave);
            SteamUserStats.StoreStats();
        }

        private void SyncWeaponKillStats()
        {
            bool updated = false;

            foreach (WeaponAchievementConfig config in _weaponAchievementConfigs.Values)
            {
                int stored = 0;
                if (SteamUserStats.GetStat(config.StatName, out int statValue))
                {
                    stored = Mathf.Max(0, statValue);
                }

                if (_pendingWeaponKillIncrements.TryGetValue(config.StatName, out int pending) && pending > 0)
                {
                    stored += pending;
                    updated = true;
                }

                _weaponKillTotals[config.StatName] = stored;
                EvaluateWeaponAchievements(config, stored, ref updated);
            }

            _pendingWeaponKillIncrements.Clear();

            if (updated)
            {
                SteamUserStats.StoreStats();
            }
        }

        private void RegisterWeaponKill(Weapon weapon)
        {
            if (!_weaponAchievementConfigs.TryGetValue(ResolveWeaponKey(weapon), out WeaponAchievementConfig config))
            {
                return;
            }

            if (!_statsReady)
            {
                IncrementPendingWeaponKill(config.StatName);
                return;
            }

            int newTotal = (_weaponKillTotals.TryGetValue(config.StatName, out int current) ? current : 0) + 1;
            _weaponKillTotals[config.StatName] = newTotal;

            SteamUserStats.SetStat(config.StatName, newTotal);

            bool statsUpdated = false;
            EvaluateWeaponAchievements(config, newTotal, ref statsUpdated);

            SteamUserStats.StoreStats();
        }

        private void EvaluateWeaponAchievements(WeaponAchievementConfig config, int total, ref bool statsUpdated)
        {
            foreach ((int threshold, string achievementName) in config.AchievementThresholds)
            {
                if (total < threshold || IsAchievementUnlocked(achievementName))
                {
                    continue;
                }

                if (SteamUserStats.SetAchievement(achievementName))
                {
                    _unlockedWeaponAchievements.Add(achievementName);
                    statsUpdated = true;
                    Debug.Log($"[Steam] Unlocked {config.WeaponKey} {threshold} kills achievement");
                }
            }
        }

        private void IncrementPendingWeaponKill(string statName)
        {
            if (_pendingWeaponKillIncrements.ContainsKey(statName))
            {
                _pendingWeaponKillIncrements[statName]++;
            }
            else
            {
                _pendingWeaponKillIncrements.Add(statName, 1);
            }
        }

        private bool IsAchievementUnlocked(string achievementName)
        {
            if (_unlockedWeaponAchievements.Contains(achievementName))
            {
                return true;
            }

            if (SteamUserStats.GetAchievement(achievementName, out bool achieved) && achieved)
            {
                _unlockedWeaponAchievements.Add(achievementName);
                return true;
            }

            return false;
        }

        private string ResolveWeaponKey(Weapon weapon)
        {
            string weaponName = !string.IsNullOrWhiteSpace(weapon.weaponName)
                ? weapon.weaponName
                : weapon.name;

            return weaponName ?? string.Empty;
        }

        private void PushKillLeaderboardScore()
        {
            if (!_statsReady)
                return;

            EnsureKillLeaderboard();
            if (_killLeaderboard.m_SteamLeaderboard == 0)
                return;

            SteamAPICall_t handle = SteamUserStats.UploadLeaderboardScore(
                _killLeaderboard,
                ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest,
                _killStatBase + _lastKillCount,
                null,
                0);

            _killScoreUploadedResult.Set(handle, HandleKillScoreUploaded);
        }

        private void TryUnlockWaveAchievements()
        {
            if (!_statsReady)
                return;

            foreach (var a in waveAchievements)
            {
                if (!a.unlocked && _highestWave >= a.waveRequired)
                {
                    if (SteamUserStats.SetAchievement(a.achievementId))
                    {
                        a.unlocked = true;
                        SteamUserStats.StoreStats();
                        Debug.Log($"[Steam] Unlocked: {a.achievementId}");
                    }
                }
            }
        }

        private void PushFavoriteWeapons()
        {
            var ranked = _weaponKills
                .Where(pair => pair.Key != null)
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key.weaponName)
                .Take(3)
                .ToList();

            int[] ids = new int[3];
            int[] kills = new int[3];

            for (int i = 0; i < ranked.Count; i++)
            {
                ids[i] = Animator.StringToHash(string.IsNullOrWhiteSpace(ranked[i].Key.weaponName)
                    ? ranked[i].Key.name
                    : ranked[i].Key.weaponName);
                kills[i] = ranked[i].Value;
            }

            SteamUserStats.StoreStats();
        }
#endif
    }
}

[System.Serializable]
public class WaveAchievement
{
    public int waveRequired;
    public string achievementId;
    public bool unlocked;
}

