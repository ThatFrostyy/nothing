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
        [SerializeField] private const string RoundsFiredStatName = "total_rounds_fired";
        [SerializeField] private const string HatsEquippedStatName = "total_hats_equipped";
        [SerializeField] private const string UniqueHatsStatName = "unique_hats_equipped";
        [SerializeField] private const string SupplyCratesOpenedStatName = "supply_crates_opened";
        [SerializeField] private const string UpgradePickupsCollectedStatName = "upgrade_pickups_collected";
        [SerializeField] private const string TotalHealingStatName = "total_healing_received";
        [SerializeField] private const string BulletTimeStatName = "bullet_time_moments";

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

        [Header("Inventory Drops")]
        [SerializeField, Min(1)] private int waveDropWaveThreshold = 20;
        [SerializeField] private int waveDropListDefinitionId = 100;
        [Header("Performance")]
        [SerializeField, Min(0f)] private float statsStoreCooldown = 1.5f;
        [SerializeField, Min(0f)] private float leaderboardUploadCooldown = 3f;

        private const string AchievementRoundsFired = "ROUNDS_5000";
        private const string AchievementFirstHat = "HAT_FIRST";
        private const string AchievementLegendaryHat = "HAT_LEGENDARY";
        private const string AchievementCollectHats = "HAT_COLLECT_10";
        private const string AchievementSupplyCrates = "CRATES_25";
        private const string AchievementUpgradePickups = "PICKUPS_10";
        private const string AchievementTotalHealing = "HEAL_500";
        private const string AchievementWaveNoMiss = "WAVE_NO_MISS";
        private const string AchievementKillsInMinute = "KILLS_100_MIN";
        private const string AchievementTwentyWavesNoDamage = "WAVES_20_NO_DAMAGE";
        private const string AchievementTenBosses = "BOSSES_10";
        private const string AchievementNoMovementWave = "WAVE_NO_MOVE";
        private const string AchievementBulletTimeMoments = "BULLET_TIME_100";

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
            ,
            ["M24 Grenade"] = new WeaponAchievementConfig(
                "M24 Grenade",
                "kills_m24_grenade",
                new (int, string)[]
                {
                    (100, "M24_GRENADE_100")
                })
        };

        private readonly Dictionary<Weapon, int> _weaponKills = new();
        private readonly Dictionary<string, int> _weaponKillTotals = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _pendingWeaponKillIncrements = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _unlockedAchievements = new();
        private readonly HashSet<Health> _trackedPlayerHealth = new();
        private readonly Dictionary<string, int> _progressStats = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _pendingProgressStatIncrements = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<HatDefinition> _equippedHats = new();
        private readonly HashSet<AutoShooter> _playerShooters = new();
        private readonly Queue<float> _recentKillTimestamps = new();
        private float _nextCoreStatsPushTime;
        private float _nextLeaderboardPushTime;
        private bool _pendingCoreStatsPush;
        private bool _pendingLeaderboardPush;
        private Callback<UserStatsReceived_t> _userStatsReceived;
        private Callback<UserStatsStored_t> _userStatsStored;
        private CallResult<LeaderboardFindResult_t> _killLeaderboardFindResult;
        private CallResult<LeaderboardScoreUploaded_t> _killScoreUploadedResult;
        private CallResult<LeaderboardScoresDownloaded_t> _killLeaderboardScoresDownloaded;
        private SteamLeaderboard_t _killLeaderboard;
        private int _lastKillCount;
        private int _killStatBase;
        private int _highestWave;
        private int _shotsThisWave;
        private int _hitsThisWave;
        private int _consecutiveNoDamageWaves;
        private int _bossKills;
        private bool _damageTakenThisWave;
        private bool _movementThisWave;
        private bool _gameManagerHooked;//
        private bool _statsReady;
        private Rigidbody2D _playerBody;
        private bool _waveDropTriggered;
        private SteamInventoryResult_t _waveDropResultHandle = SteamInventoryResult_t.Invalid;
        private Callback<SteamInventoryResultReady_t> _inventoryResultReady;
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
            AutoShooter.OnRoundsFired += HandleRoundsFired;
            Enemy.OnAnyEnemyKilled += HandleEnemyKilled;
            PlayerCosmetics.OnHatEquipped += HandleHatEquipped;
            UpgradePickup.OnAnyCollected += HandleUpgradePickupCollected;
            WeaponCrate.OnAnyBroken += HandleSupplyCrateBroken;
            Health.OnAnyHealed += HandleAnyHealed;
            Health.OnAnyDamaged += HandleAnyDamaged;
            KillSlowMotion.OnBulletTimeTriggered += HandleBulletTimeTriggered;
            _killScoreUploadedResult = CallResult<LeaderboardScoreUploaded_t>.Create(HandleKillScoreUploaded);
            _killLeaderboardScoresDownloaded = CallResult<LeaderboardScoresDownloaded_t>.Create(HandleUserLeaderboardScoresDownloaded);
            _inventoryResultReady = Callback<SteamInventoryResultReady_t>.Create(HandleInventoryResultReady);
        }

        private void OnDisable()
        {
            FlushPendingPushes();

            if (_gameManagerHooked && GameManager.I != null)
            {
                GameManager.I.OnKillCountChanged -= HandleKillCountChanged;
                GameManager.I.OnWaveStarted -= HandleWaveStarted;
            }

            Enemy.OnAnyEnemyKilledByWeapon -= HandleEnemyKilledByWeapon;
            PlayerController.OnPlayerReady -= HandlePlayerReady;
            AutoShooter.OnRoundsFired -= HandleRoundsFired;
            Enemy.OnAnyEnemyKilled -= HandleEnemyKilled;
            PlayerCosmetics.OnHatEquipped -= HandleHatEquipped;
            UpgradePickup.OnAnyCollected -= HandleUpgradePickupCollected;
            WeaponCrate.OnAnyBroken -= HandleSupplyCrateBroken;
            Health.OnAnyHealed -= HandleAnyHealed;
            Health.OnAnyDamaged -= HandleAnyDamaged;
            KillSlowMotion.OnBulletTimeTriggered -= HandleBulletTimeTriggered;

            _killScoreUploadedResult = null;
            _killLeaderboardScoresDownloaded = null;
            _inventoryResultReady?.Dispose();
            _inventoryResultReady = null;
            ReleaseWaveDropHandle();
            foreach (Health health in _trackedPlayerHealth)
            {
                if (health != null)
                {
                    health.OnDeath -= HandlePlayerDeath;
                    health.OnDamaged -= HandlePlayerDamaged;
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
            _unlockedAchievements.Clear();
            _progressStats.Clear();
            _pendingProgressStatIncrements.Clear();
            _equippedHats.Clear();
            _playerShooters.Clear();
            _recentKillTimestamps.Clear();
            _bossKills = 0;
            _consecutiveNoDamageWaves = 0;
            _pendingCoreStatsPush = false;
            _pendingLeaderboardPush = false;
            _nextCoreStatsPushTime = 0f;
            _nextLeaderboardPushTime = 0f;
            ResetWaveTrackers();
            _playerBody = null;
        }

        private void FlushPendingPushes()
        {
            if (!_statsReady) return;

            // Force push core stats regardless of timer
            if (_pendingCoreStatsPush)
            {
                PushCoreStats();
                _pendingCoreStatsPush = false;
            }

            // Force push leaderboard regardless of timer
            if (_pendingLeaderboardPush)
            {
                if (TryPushKillLeaderboardScore())
                {
                    _pendingLeaderboardPush = false;
                }
            }
        }

        private void Update()
        {
            if (!_gameManagerHooked)
            {
                HookGameManager();
            }

            TrackPlayerMovement();
            ProcessPendingSteamPushes();
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
            EvaluateWaveBasedAchievements(wave);
            _highestWave = Mathf.Max(_highestWave, wave);

            QueueCoreStatsPush();
            QueueKillLeaderboardPush();
            TryUnlockWaveAchievements();
            TryTriggerWaveDrop(wave);

            ResetWaveTrackers();
        }

        private void HandleKillLeaderboardFound(LeaderboardFindResult_t result, bool failure)
        {
            if (failure || result.m_bLeaderboardFound == 0 || result.m_hSteamLeaderboard.m_SteamLeaderboard == 0)
            {

                return;
            }

            _killLeaderboard = result.m_hSteamLeaderboard;
            QueueKillLeaderboardPush();
        }

        private void HandleKillScoreUploaded(LeaderboardScoreUploaded_t result, bool failure)
        {
            if (failure || result.m_bSuccess == 0)
            {

                return;
            }



            if (result.m_bScoreChanged == 1)
            {

            }
            else
            {



            }

            // Request rows around the current user so we can confirm whether the account has a leaderboard row.
            RequestKillLeaderboardAroundUser();
        }

        private void TryTriggerWaveDrop(int wave)
        {
            if (_waveDropTriggered)
            {
                return;
            }

            if (wave < waveDropWaveThreshold)
            {
                return;
            }

            if (waveDropListDefinitionId <= 0)
            {

                _waveDropTriggered = true;
                return;
            }

            ReleaseWaveDropHandle();

            if (!SteamInventory.TriggerItemDrop(out _waveDropResultHandle, new SteamItemDef_t(waveDropListDefinitionId)))
            {

                _waveDropResultHandle = SteamInventoryResult_t.Invalid;
                return;
            }

            _waveDropTriggered = true;

        }

        private void HandleInventoryResultReady(SteamInventoryResultReady_t result)
        {
            if (result.m_handle != _waveDropResultHandle)
            {
                return;
            }

            if (result.m_result != EResult.k_EResultOK)
            {

            }
            else
            {

            }

            ReleaseWaveDropHandle();
        }

        private void ReleaseWaveDropHandle()
        {
            if (_waveDropResultHandle != SteamInventoryResult_t.Invalid)
            {
                SteamInventory.DestroyResult(_waveDropResultHandle);
                _waveDropResultHandle = SteamInventoryResult_t.Invalid;
            }
        }

        private void RequestKillLeaderboardAroundUser()
        {
            if (_killLeaderboard.m_SteamLeaderboard == 0)
            {

                return;
            }

            // Request rows in range [-5, 5] around the current user
            SteamAPICall_t handle = SteamUserStats.DownloadLeaderboardEntries(
                _killLeaderboard,
                ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobalAroundUser,
                -5,
                5);
            _killLeaderboardScoresDownloaded.Set(handle, HandleUserLeaderboardScoresDownloaded);

        }

        private void HandleUserLeaderboardScoresDownloaded(LeaderboardScoresDownloaded_t callback, bool failure)
        {
            if (failure)
            {

                return;
            }

            if (callback.m_cEntryCount <= 0)
            {
                return;
            }

            bool foundSelf = false;
            for (int i = 0; i < callback.m_cEntryCount; i++)
            {
                if (!SteamUserStats.GetDownloadedLeaderboardEntry(callback.m_hSteamLeaderboardEntries, i, out LeaderboardEntry_t entry, null, 0))
                    continue;

                string name = SteamFriends.GetFriendPersonaName(entry.m_steamIDUser);
                string meMark = entry.m_steamIDUser == SteamUser.GetSteamID() ? " <-- THIS ACCOUNT" : string.Empty;

                if (entry.m_steamIDUser == SteamUser.GetSteamID())
                    foundSelf = true;
            }

            if (!foundSelf)
            {
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
                SyncProgressStats();

                EnsureKillLeaderboard();
                QueueCoreStatsPush();
                QueueKillLeaderboardPush();
            }
            else
            {
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

        private void HandleEnemyKilled(Enemy enemy)
        {
            if (!_statsReady || enemy == null)
            {
                return;
            }

            _recentKillTimestamps.Enqueue(Time.time);
            float cutoff = Time.time - 60f;
            while (_recentKillTimestamps.Count > 0 && _recentKillTimestamps.Peek() < cutoff)
            {
                _recentKillTimestamps.Dequeue();
            }

            if (_recentKillTimestamps.Count >= 100)
            {
                TryUnlockAchievement(AchievementKillsInMinute);
            }

            if (enemy.IsBoss)
            {
                _bossKills++;
                if (_bossKills >= 10)
                {
                    TryUnlockAchievement(AchievementTenBosses);
                }
            }
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
                health.OnDamaged += HandlePlayerDamaged;
            }

            AutoShooter shooter = controller ? controller.GetComponentInChildren<AutoShooter>() : null;
            if (shooter != null)
            {
                _playerShooters.Add(shooter);
            }

            if (!_playerBody && controller)
            {
                _playerBody = controller.GetComponent<Rigidbody2D>();
            }
        }

        private void HandlePlayerDeath()
        {
            QueueCoreStatsPush();
            QueueKillLeaderboardPush();
        }

        private void HandlePlayerDamaged(int _)
        {
            _damageTakenThisWave = true;
            _consecutiveNoDamageWaves = 0;
        }

        private void PushCoreStats()
        {
            if (!_statsReady)
                return;

            SteamUserStats.SetStat(KillStatName, _killStatBase + _lastKillCount);
            SteamUserStats.SetStat(TopWaveStatName, _highestWave);
            SteamUserStats.StoreStats();
        }

        private void QueueCoreStatsPush()
        {
            _pendingCoreStatsPush = true;
        }

        private void QueueKillLeaderboardPush()
        {
            _pendingLeaderboardPush = true;
        }

        private void ProcessPendingSteamPushes()
        {
            if (!_statsReady)
            {
                return;
            }

            float now = Time.unscaledTime;

            if (_pendingCoreStatsPush && now >= _nextCoreStatsPushTime)
            {
                PushCoreStats();
                _pendingCoreStatsPush = false;
                _nextCoreStatsPushTime = now + Mathf.Max(0f, statsStoreCooldown);
            }

            if (_pendingLeaderboardPush && now >= _nextLeaderboardPushTime)
            {
                if (TryPushKillLeaderboardScore())
                {
                    _pendingLeaderboardPush = false;
                    _nextLeaderboardPushTime = now + Mathf.Max(0f, leaderboardUploadCooldown);
                }
            }
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
                    _unlockedAchievements.Add(achievementName);
                    statsUpdated = true;
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
            if (_unlockedAchievements.Contains(achievementName))
            {
                return true;
            }

            if (SteamUserStats.GetAchievement(achievementName, out bool achieved) && achieved)
            {
                _unlockedAchievements.Add(achievementName);
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

        private bool TryPushKillLeaderboardScore()
        {
            if (!_statsReady)
            {
                return false;
            }

            int score = _killStatBase + _lastKillCount;
            EnsureKillLeaderboard();

            if (_killLeaderboard.m_SteamLeaderboard == 0)
            {
                return false;
            }


            SteamAPICall_t handle = SteamUserStats.UploadLeaderboardScore(
                _killLeaderboard,
                ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest,
                score,
                null,
                0);

            _killScoreUploadedResult.Set(handle, HandleKillScoreUploaded);
            return true;
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
                    }
                }
            }
        }

        private void SyncProgressStats()
        {
            LoadProgressStat(RoundsFiredStatName);
            LoadProgressStat(HatsEquippedStatName);
            LoadProgressStat(UniqueHatsStatName);
            LoadProgressStat(SupplyCratesOpenedStatName);
            LoadProgressStat(UpgradePickupsCollectedStatName);
            LoadProgressStat(TotalHealingStatName);
            LoadProgressStat(BulletTimeStatName);

            FlushPendingProgressStatIncrements();
            EvaluateProgressAchievements();
        }

        private void LoadProgressStat(string statName)
        {
            if (SteamUserStats.GetStat(statName, out int stored))
            {
                _progressStats[statName] = Mathf.Max(0, stored);
            }
            else
            {
                _progressStats[statName] = 0;
            }
        }

        private void FlushPendingProgressStatIncrements()
        {
            if (!_statsReady || _pendingProgressStatIncrements.Count == 0)
            {
                return;
            }

            foreach (var kvp in _pendingProgressStatIncrements)
            {
                IncrementProgressStat(kvp.Key, kvp.Value, out _);
            }

            _pendingProgressStatIncrements.Clear();
        }

        private void HandleRoundsFired(AutoShooter shooter, int count)
        {
            if (shooter == null || !_playerShooters.Contains(shooter))
            {
                return;
            }

            _shotsThisWave += Mathf.Max(0, count);

            IncrementProgressStat(RoundsFiredStatName, count, out int total);
            if (total >= 5000)
            {
                TryUnlockAchievement(AchievementRoundsFired);
            }
        }

        private void HandleAnyDamaged(Health health, int amount, Weapon sourceWeapon)
        {
            if (health == null || sourceWeapon == null || amount <= 0)
            {
                return;
            }

            if (health.TryGetComponent<Enemy>(out _))
            {
                _hitsThisWave++;
            }
        }

        private void HandleHatEquipped(HatDefinition hat)
        {
            if (hat == null)
            {
                return;
            }

            IncrementProgressStat(HatsEquippedStatName, 1, out int totalHats);
            if (totalHats >= 1)
            {
                TryUnlockAchievement(AchievementFirstHat);
            }

            if (_equippedHats.Add(hat))
            {
                IncrementProgressStat(UniqueHatsStatName, 1, out int uniqueHats);
                if (uniqueHats >= 10)
                {
                    TryUnlockAchievement(AchievementCollectHats);
                }
            }

            if (!string.IsNullOrWhiteSpace(hat.RarityText) && string.Equals(hat.RarityText, "Legendary", StringComparison.OrdinalIgnoreCase))
            {
                TryUnlockAchievement(AchievementLegendaryHat);
            }
        }

        private void HandleSupplyCrateBroken(WeaponCrate crate)
        {
            IncrementProgressStat(SupplyCratesOpenedStatName, 1, out int totalCrates);
            if (totalCrates >= 25)
            {
                TryUnlockAchievement(AchievementSupplyCrates);
            }
        }

        private void HandleUpgradePickupCollected(UpgradePickup pickup)
        {
            IncrementProgressStat(UpgradePickupsCollectedStatName, 1, out int totalPickups);
            if (totalPickups >= 10)
            {
                TryUnlockAchievement(AchievementUpgradePickups);
            }
        }

        private void HandleAnyHealed(Health health, int amount)
        {
            IncrementProgressStat(TotalHealingStatName, amount, out int totalHealing);
            if (totalHealing >= 500)
            {
                TryUnlockAchievement(AchievementTotalHealing);
            }
        }

        private void HandleBulletTimeTriggered()
        {
            IncrementProgressStat(BulletTimeStatName, 1, out int total);
            if (total >= 100)
            {
                TryUnlockAchievement(AchievementBulletTimeMoments);
            }
        }

        private void EvaluateWaveBasedAchievements(int wave)
        {
            if (!_statsReady)
            {
                return;
            }

            if (wave > 1 && _shotsThisWave > 0 && _hitsThisWave >= _shotsThisWave)
            {
                TryUnlockAchievement(AchievementWaveNoMiss);
            }

            if (!_damageTakenThisWave)
            {
                if (wave > 1)
                {
                    _consecutiveNoDamageWaves++;
                    if (_consecutiveNoDamageWaves >= 20)
                    {
                        TryUnlockAchievement(AchievementTwentyWavesNoDamage);
                    }
                }
            }
            else
            {
                _consecutiveNoDamageWaves = 0;
            }

            if (wave > 1 && !_movementThisWave)
            {
                TryUnlockAchievement(AchievementNoMovementWave);
            }
        }

        private void ResetWaveTrackers()
        {
            _shotsThisWave = 0;
            _hitsThisWave = 0;
            _damageTakenThisWave = false;
            _movementThisWave = false;
        }

        private void TrackPlayerMovement()
        {
            if (_playerBody == null)
            {
                return;
            }

            if (_playerBody.linearVelocity.sqrMagnitude > 0.01f)
            {
                _movementThisWave = true;
            }
        }

        private void EvaluateProgressAchievements()
        {
            if (GetProgressStat(RoundsFiredStatName) >= 5000)
            {
                TryUnlockAchievement(AchievementRoundsFired);
            }

            if (GetProgressStat(HatsEquippedStatName) >= 1)
            {
                TryUnlockAchievement(AchievementFirstHat);
            }

            if (GetProgressStat(UniqueHatsStatName) >= 10)
            {
                TryUnlockAchievement(AchievementCollectHats);
            }

            if (GetProgressStat(SupplyCratesOpenedStatName) >= 25)
            {
                TryUnlockAchievement(AchievementSupplyCrates);
            }

            if (GetProgressStat(UpgradePickupsCollectedStatName) >= 10)
            {
                TryUnlockAchievement(AchievementUpgradePickups);
            }

            if (GetProgressStat(TotalHealingStatName) >= 500)
            {
                TryUnlockAchievement(AchievementTotalHealing);
            }

            if (GetProgressStat(BulletTimeStatName) >= 100)
            {
                TryUnlockAchievement(AchievementBulletTimeMoments);
            }
        }

        private int GetProgressStat(string statName)
        {
            return _progressStats.TryGetValue(statName, out int value) ? value : 0;
        }

        private void IncrementProgressStat(string statName, int amount, out int newTotal)
        {
            newTotal = GetProgressStat(statName);
            if (amount <= 0)
            {
                return;
            }

            if (!_statsReady)
            {
                if (_pendingProgressStatIncrements.ContainsKey(statName))
                {
                    _pendingProgressStatIncrements[statName] += amount;
                }
                else
                {
                    _pendingProgressStatIncrements.Add(statName, amount);
                }

                return;
            }

            newTotal += amount;
            _progressStats[statName] = newTotal;
            SteamUserStats.SetStat(statName, newTotal);
            SteamUserStats.StoreStats();
        }

        private void TryUnlockAchievement(string achievementId)
        {
            if (string.IsNullOrWhiteSpace(achievementId) || IsAchievementUnlocked(achievementId) || !_statsReady)
            {
                return;
            }

            if (SteamUserStats.SetAchievement(achievementId))
            {
                _unlockedAchievements.Add(achievementId);
                SteamUserStats.StoreStats();
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

        private void OnApplicationQuit()
        {
            FlushPendingPushes();
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
