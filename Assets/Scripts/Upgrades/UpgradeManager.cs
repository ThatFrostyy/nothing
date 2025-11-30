using UnityEngine;


namespace FF
{
    public class UpgradeManager : MonoBehaviour, ISceneReferenceHandler
    {
        public static UpgradeManager I { get; private set; }

        [SerializeField] Upgrade[] all;
        [SerializeField] PlayerStats stats;
        [SerializeField] XPWallet wallet;
        [SerializeField] UpgradeUI ui;
        [SerializeField] WeaponManager weaponManager;
        [SerializeField, Min(0)] int maxUpgradeSelections = 0;

        int upgradesTaken;
        int pendingUpgrades;
        readonly System.Collections.Generic.Dictionary<Upgrade, int> upgradeCounts = new();
        readonly System.Collections.Generic.Dictionary<Weapon, int> weaponKillCounts = new();
        readonly System.Collections.Generic.Dictionary<Weapon, WeaponUpgradeState> weaponUpgradeStates = new();

        public System.Action<int> OnPendingUpgradesChanged;

        public event System.Action<PlayerStats> OnPlayerStatsRegistered;
        public event System.Action<XPWallet> OnWalletRgistered;
        public event System.Action<UpgradeUI> OnUIRegistered;

        public int GetPendingUpgradeCount() => pendingUpgrades;

        Upgrade RandomUpgrade(System.Collections.Generic.List<Upgrade> pool)
        {
            if (pool == null || pool.Count == 0)
            {
                return null;
            }

            int totalWeight = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                Upgrade candidate = pool[i];
                if (candidate != null)
                {
                    totalWeight += Mathf.Max(1, candidate.GetWeight());
                }
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            int roll = Random.Range(0, totalWeight);
            for (int i = 0; i < pool.Count; i++)
            {
                Upgrade candidate = pool[i];
                if (candidate == null)
                {
                    continue;
                }

                roll -= Mathf.Max(1, candidate.GetWeight());
                if (roll < 0)
                {
                    return candidate;
                }
            }

            return pool[pool.Count - 1];
        }

        void Awake()
        {
            if (I != null && I != this)
            {
                Destroy(gameObject);
                return;
            }

            I = this;
            DontDestroyOnLoad(gameObject);

            TryResolveUI();
            NotifyPendingChanged();
        }

        void Update()
        {
            if (wallet == null)
            {
                TryAutoFindWallet();
            }

            if (weaponManager == null)
            {
                weaponManager = FindAnyObjectByType<WeaponManager>();
            }
        }


        void OnEnable()
        {
            Enemy.OnAnyEnemyKilledByWeapon += HandleWeaponKill;
            SceneReferenceRegistry.Register(this);
        }

        void OnDisable()
        {
            Enemy.OnAnyEnemyKilledByWeapon -= HandleWeaponKill;
            SceneReferenceRegistry.Unregister(this);
        }

        void TryAutoFindWallet()
        {
            var found = FindAnyObjectByType<XPWallet>();
            if (found != null)
            {
                RegisterWallet(found);
                Debug.Log("[UM] Auto-bound XPWallet");
            }
        }

        public void RegisterPlayerStats(PlayerStats playerStats)
        {
            stats = playerStats;
            OnPlayerStatsRegistered?.Invoke(playerStats);
        }

        public void RegisterWallet(XPWallet wallet)
        {
            UnsubscribeWalletEvents();
            this.wallet = wallet;
            OnWalletRgistered?.Invoke(wallet);

            Debug.Log($"[UM] RegisterWallet ? bound wallet on {wallet.gameObject.name}");
            wallet.OnLevelUp += OnLevel;
        }

        public void RegisterWeaponManager(WeaponManager manager)
        {
            weaponManager = manager;
        }

        public void RegisterUI(UpgradeUI upgradeUI)
        {
            ui = upgradeUI;
            OnUIRegistered?.Invoke(upgradeUI);
        }

        void TryResolveUI()
        {
            if (ui == null)
            {
                ui = FindAnyObjectByType<UpgradeUI>(FindObjectsInactive.Include);

                if (ui != null)
                {
                    OnUIRegistered?.Invoke(ui);
                }
            }
        }

        public void ClearSceneReferences()
        {
            UnsubscribeWalletEvents();
            stats = null;
            wallet = null;
            weaponManager = null;

            // FIX: Only clear UI if it is actually destroyed (equals null). 
            // If the UI is DontDestroyOnLoad, we keep the reference.
            if (ui == null)
            {
                ui = null;
            }

            UpgradeUI.ResetStaticState();
        }

        public void ResetState()
        {
            ClearSceneReferences();

            ui = null;
            TryResolveUI();

            upgradesTaken = 0;
            pendingUpgrades = 0;
            upgradeCounts.Clear();
            weaponKillCounts.Clear();
            weaponUpgradeStates.Clear();

            NotifyPendingChanged();
        }

        void UnsubscribeWalletEvents()
        {
            if (wallet != null)
            {
                wallet.OnLevelUp -= OnLevel;
            }
        }

        void OnLevel(int lvl)
        {
            Debug.Log($"[UM] OnLevel({lvl}) called. CanReceiveUpgrades() = {CanReceiveUpgrades()} (ui != null: {ui != null}, all len: {(all != null ? all.Length : -1)}, maxUpgradeSelections: {maxUpgradeSelections}, upgradesTaken: {upgradesTaken})");

            if (!CanReceiveUpgrades())
            {
                return;
            }

            pendingUpgrades = Mathf.Min(pendingUpgrades + 1, GetRemainingSelections());
            Debug.Log($"[UM] pendingUpgrades now = {pendingUpgrades}, GetRemainingSelections() = {GetRemainingSelections()}");
            NotifyPendingChanged();
        }

        void Pick(Upgrade u)
        {
            if (ui == null) return;

            u.Apply(stats);
            upgradesTaken++;
            IncrementUpgradeCount(u);
            pendingUpgrades = Mathf.Max(0, pendingUpgrades - 1);
            NotifyPendingChanged();

            if (!TryShowWeaponUpgradeFollowups())
            {
                ui.Hide();
            }
        }

        public void TryOpenUpgradeMenu()
        {
            TryResolveUI();

            if (ui == null || UpgradeUI.IsShowing)
            {
                return;
            }

            if (!CanReceiveUpgrades())
            {
                pendingUpgrades = 0;
                NotifyPendingChanged();
                return;
            }

            if (pendingUpgrades <= 0)
            {
                return;
            }

            pendingUpgrades = Mathf.Min(pendingUpgrades, GetRemainingSelections());
            NotifyPendingChanged();

            System.Collections.Generic.List<Upgrade> options = BuildUpgradeOptions(3);
            if (options.Count == 0)
            {
                pendingUpgrades = 0;
                NotifyPendingChanged();
                return;
            }

            while (options.Count > 0 && options.Count < 3)
            {
                Upgrade duplicate = options[Random.Range(0, options.Count)];
                options.Add(duplicate);
            }

            ui.Show(options[0], options.Count > 1 ? options[1] : options[0], options.Count > 2 ? options[2] : options[0], Pick, pendingUpgrades);
        }

        bool TryShowWeaponUpgradeFollowups()
        {
            if (ui == null)
            {
                return false;
            }

            Weapon focusWeapon = ResolveFocusWeapon();
            if (!focusWeapon)
            {
                return false;
            }

            var options = BuildWeaponUpgradeOptions(focusWeapon, 3);
            if (options.Count == 0)
            {
                return false;
            }

            while (options.Count > 0 && options.Count < 3)
            {
                WeaponUpgradeOption duplicate = options[Random.Range(0, options.Count)];
                options.Add(duplicate);
            }

            ui.ShowWeaponUpgrades(focusWeapon,
                options[0],
                options.Count > 1 ? options[1] : options[0],
                options.Count > 2 ? options[2] : options[0],
                PickWeaponUpgrade,
                pendingUpgrades);
            return true;
        }

        void PickWeaponUpgrade(WeaponUpgradeOption option)
        {
            ApplyWeaponUpgrade(option);
            ui.Hide();
        }

        int GetRemainingSelections()
        {
            return maxUpgradeSelections <= 0
                ? int.MaxValue
                : Mathf.Max(0, maxUpgradeSelections - upgradesTaken);
        }

        bool CanReceiveUpgrades()
        {
            if (ui == null)
            {
                return false;
            }

            if (maxUpgradeSelections > 0 && upgradesTaken >= maxUpgradeSelections)
            {
                return false;
            }

            if (all == null || all.Length == 0)
            {
                return false;
            }

            return true;
        }

        void NotifyPendingChanged()
        {
            var listeners = OnPendingUpgradesChanged?.GetInvocationList();
            int count = listeners?.Length ?? 0;
            Debug.Log($"[UM] NotifyPendingChanged ? pendingUpgrades={pendingUpgrades}, listeners={count}");
            OnPendingUpgradesChanged?.Invoke(pendingUpgrades);
        }

        System.Collections.Generic.List<Upgrade> BuildUpgradeOptions(int count)
        {
            var available = new System.Collections.Generic.List<Upgrade>();
            if (all != null)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    Upgrade upgrade = all[i];
                    if (IsUpgradeAvailable(upgrade))
                    {
                        available.Add(upgrade);
                    }
                }
            }

            var selections = new System.Collections.Generic.List<Upgrade>();
            if (available.Count == 0 || count <= 0)
            {
                return selections;
            }

            var workingPool = new System.Collections.Generic.List<Upgrade>(available);
            for (int i = 0; i < count; i++)
            {
                Upgrade pick = RandomUpgrade(workingPool);
                if (pick == null)
                {
                    break;
                }

                selections.Add(pick);

                if (workingPool.Count > 1)
                {
                    workingPool.Remove(pick);
                }
            }

            return selections;
        }

        System.Collections.Generic.List<WeaponUpgradeOption> BuildWeaponUpgradeOptions(Weapon weapon, int count)
        {
            var selections = new System.Collections.Generic.List<WeaponUpgradeOption>();
            if (weapon == null || count <= 0)
            {
                return selections;
            }

            WeaponUpgradeState state = GetOrCreateWeaponState(weapon);
            int killCount = weaponKillCounts.TryGetValue(weapon, out int kills) ? kills : 0;
            float magnitude = CalculateWeaponUpgradeMagnitude(killCount, state != null ? state.CardsTaken : 0);

            var pool = new System.Collections.Generic.List<WeaponUpgradeOption>
            {
                CreateWeaponUpgradeOption(weapon, WeaponUpgradeType.Damage, magnitude, killCount, state?.CardsTaken ?? 0),
                CreateWeaponUpgradeOption(weapon, WeaponUpgradeType.FireRate, magnitude, killCount, state?.CardsTaken ?? 0),
                CreateWeaponUpgradeOption(weapon, WeaponUpgradeType.ProjectileSpeed, magnitude, killCount, state?.CardsTaken ?? 0)
            };

            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int pickIndex = Random.Range(0, pool.Count);
                selections.Add(pool[pickIndex]);

                if (pool.Count > 1)
                {
                    pool.RemoveAt(pickIndex);
                }
            }

            return selections;
        }

        bool IsUpgradeAvailable(Upgrade upgrade)
        {
            if (upgrade == null)
            {
                return false;
            }

            int takenCount = upgradeCounts.TryGetValue(upgrade, out int timesTaken) ? timesTaken : 0;
            return upgrade.CanApply(stats, takenCount);
        }

        void IncrementUpgradeCount(Upgrade upgrade)
        {
            if (upgrade == null)
            {
                return;
            }

            if (upgradeCounts.ContainsKey(upgrade))
            {
                upgradeCounts[upgrade]++;
            }
            else
            {
                upgradeCounts.Add(upgrade, 1);
            }
        }

        Weapon ResolveFocusWeapon()
        {
            Weapon bestWeapon = null;
            int bestKills = -1;

            foreach (var pair in weaponKillCounts)
            {
                if (pair.Key == null)
                {
                    continue;
                }

                if (pair.Value > bestKills)
                {
                    bestWeapon = pair.Key;
                    bestKills = pair.Value;
                }
            }

            if (bestWeapon != null)
            {
                return bestWeapon;
            }

            if (weaponManager != null && weaponManager.CurrentWeapon != null)
            {
                return weaponManager.CurrentWeapon;
            }

            return null;
        }

        void HandleWeaponKill(Enemy enemy, Weapon weapon)
        {
            if (weapon == null)
            {
                return;
            }

            if (weaponKillCounts.ContainsKey(weapon))
            {
                weaponKillCounts[weapon]++;
            }
            else
            {
                weaponKillCounts.Add(weapon, 1);
            }

            GetOrCreateWeaponState(weapon);
        }

        void ApplyWeaponUpgrade(WeaponUpgradeOption option)
        {
            if (option.Weapon == null)
            {
                return;
            }

            WeaponUpgradeState state = GetOrCreateWeaponState(option.Weapon);
            state?.Apply(option);
        }

        float CalculateWeaponUpgradeMagnitude(int killCount, int cardsTaken)
        {
            float baseBonus = 0.08f;
            float killBonus = Mathf.Clamp(killCount, 0, 250) * 0.0008f;
            float stackBonus = Mathf.Min(0.05f, cardsTaken * 0.01f);
            return baseBonus + killBonus + stackBonus;
        }

        WeaponUpgradeOption CreateWeaponUpgradeOption(
    Weapon weapon,
    WeaponUpgradeType type,
    float magnitude,
    int killCount,
    int cardsTaken)
        {
            string weaponName = weapon != null ? weapon.weaponName : "Weapon";

            int percentage = Mathf.RoundToInt(magnitude * 100f);

            // Titles (NO COLOR — clean text)
            string baseTitle = type switch
            {
                WeaponUpgradeType.Damage => "Damage Boost",
                WeaponUpgradeType.FireRate => "Fire Rate Boost",
                WeaponUpgradeType.ProjectileSpeed => "Bullet Speed Boost",
                _ => "Upgrade"
            };

            // Base descriptions (default UI color)
            string baseDescription = type switch
            {
                WeaponUpgradeType.Damage => "Increase weapon damage by ",
                WeaponUpgradeType.FireRate => "Shoot faster by ",
                WeaponUpgradeType.ProjectileSpeed => "Increase bullet velocity by ",
                _ => "Boost weapon performance by "
            };

            // Choose % color per upgrade type
            string percentColor = type switch
            {
                WeaponUpgradeType.Damage => "#FF4040",         
                WeaponUpgradeType.FireRate => "#D17A22",       
                WeaponUpgradeType.ProjectileSpeed => "#DBBE50",
                _ => "#FFD966"
            };

            // Build the colored % value
            string coloredPercent = $"<color={percentColor}>+{percentage}%</color>.";

            // Kills (default UI color)
            string extra = $"(Kills: {killCount})";

            return new WeaponUpgradeOption(
                weapon,
                type,
                magnitude,
                baseTitle,
                baseDescription + coloredPercent,   // but UI uses this only in main description
                baseTitle,                          // final title = same as base title (NO COLOR)
                extra                                // final description shown in EXTRA field
            );
        }


        WeaponUpgradeState GetOrCreateWeaponState(Weapon weapon)
        {
            if (weapon == null)
            {
                return null;
            }

            if (!weaponUpgradeStates.TryGetValue(weapon, out WeaponUpgradeState state) || state == null)
            {
                state = new WeaponUpgradeState(weapon);
                weaponUpgradeStates[weapon] = state;
            }

            return state;
        }

        bool TryGetWeaponState(Weapon weapon, out WeaponUpgradeState state)
        {
            state = null;
            if (weapon == null)
            {
                return false;
            }

            if (weaponUpgradeStates.TryGetValue(weapon, out state) && state != null)
            {
                return true;
            }

            return false;
        }

        public float GetWeaponDamageMultiplier(Weapon weapon)
        {
            return TryGetWeaponState(weapon, out var state) ? state.GetDamageMultiplier() : 1f;
        }

        public float GetWeaponFireRateMultiplier(Weapon weapon)
        {
            return TryGetWeaponState(weapon, out var state) ? state.GetFireRateMultiplier() : 1f;
        }

        public float GetWeaponProjectileSpeedMultiplier(Weapon weapon)
        {
            return TryGetWeaponState(weapon, out var state) ? state.GetProjectileSpeedMultiplier() : 1f;
        }
    }
}