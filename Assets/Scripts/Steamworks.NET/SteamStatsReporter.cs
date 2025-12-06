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
        private const string KillStatName = "total_kills";
        private const string TopWaveStatName = "top_wave_survived";
        private const string FavoriteWeapon1StatName = "favorite_weapon_1";
        private const string FavoriteWeapon2StatName = "favorite_weapon_2";
        private const string FavoriteWeapon3StatName = "favorite_weapon_3";
        private const string FavoriteWeapon1KillsStatName = "favorite_weapon_1_kills";
        private const string FavoriteWeapon2KillsStatName = "favorite_weapon_2_kills";
        private const string FavoriteWeapon3KillsStatName = "favorite_weapon_3_kills";

        private readonly Dictionary<Weapon, int> _weaponKills = new();
        private readonly HashSet<Health> _trackedPlayerHealth = new();
        private Callback<UserStatsReceived_t> _userStatsReceived;
        private Callback<UserStatsStored_t> _userStatsStored;
        private int _lastKillCount;
        private int _highestWave;
        private bool _gameManagerHooked;
        private bool _statsReady;

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

            SteamUserStats.RequestCurrentStats();

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
            _gameManagerHooked = false;
            _statsReady = false;
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

        private void HandleStatsReceived(UserStatsReceived_t callback)
        {
            if (callback.m_nGameID == SteamUtils.GetAppID().ToUInt64() && callback.m_eResult == EResult.k_EResultOK)
            {
                _statsReady = true;
                PushCoreStats();
                PushFavoriteWeapons();
            }
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
            _lastKillCount = kills;
            PushCoreStats();
        }

        private void HandleWaveStarted(int wave)
        {
            _highestWave = Mathf.Max(_highestWave, wave);

            PushCoreStats();
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
            PushFavoriteWeapons();
        }

        private void PushCoreStats()
        {
            if (!EnsureStatsReady())
            {
                return;
            }

            SteamUserStats.SetStat(KillStatName, _lastKillCount);
            SteamUserStats.SetStat(TopWaveStatName, _highestWave);
            SteamUserStats.StoreStats();
        }

        private void PushFavoriteWeapons()
        {
            if (!EnsureStatsReady())
            {
                return;
            }

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

            SteamUserStats.SetStat(FavoriteWeapon1StatName, ids[0]);
            SteamUserStats.SetStat(FavoriteWeapon2StatName, ids[1]);
            SteamUserStats.SetStat(FavoriteWeapon3StatName, ids[2]);

            SteamUserStats.SetStat(FavoriteWeapon1KillsStatName, kills[0]);
            SteamUserStats.SetStat(FavoriteWeapon2KillsStatName, kills[1]);
            SteamUserStats.SetStat(FavoriteWeapon3KillsStatName, kills[2]);

            SteamUserStats.StoreStats();
        }

        private bool EnsureStatsReady()
        {
            if (!_statsReady)
            {
                SteamUserStats.RequestCurrentStats();
            }

            return _statsReady;
        }
#endif
    }
}
