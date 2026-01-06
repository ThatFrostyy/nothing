using System.Collections.Generic;
using UnityEngine;


namespace FF
{
    public class UpgradeManager : MonoBehaviour, ISceneReferenceHandler
    {
        public static UpgradeManager I { get; private set; }

        [SerializeField] Upgrade[] all;
        [SerializeField] WeaponUpgradeCard[] weaponUpgradeCards;
        [SerializeField] PlayerStats stats;
        [SerializeField] XPWallet wallet;
        [SerializeField] UpgradeUI ui;
        [SerializeField] WeaponManager weaponManager;
        [SerializeField, Min(0)] int maxUpgradeSelections = 0;

        [Header("Global Upgrade Limits")]
        [SerializeField, Min(1f)] float maxFireRateRpm = 500f;
        [SerializeField, Min(0.01f)] float minFireCooldownSeconds = 0.1f;

        [Header("Weapon Upgrade Scaling")]
        [SerializeField, Min(0f)] float baseWeaponUpgradeBonus = 0.06f;
        [SerializeField, Min(0f)] float killBonusPerKill = 0.0006f;
        [SerializeField, Min(0f)] float stackBonusPerCard = 0.008f;
        [SerializeField, Min(0f)] float maxStackBonus = 0.04f;
        [SerializeField, Min(0)] int weaponUpgradeKillCap = 200;

        const int WeaponCardsPerSelection = 3;

        int upgradesTaken;
        int pendingUpgrades;
        readonly Dictionary<Upgrade, int> upgradeCounts = new();
        readonly Dictionary<Weapon, int> weaponKillCounts = new();
        readonly Dictionary<Weapon, WeaponUpgradeState> weaponUpgradeStates = new();
        readonly Queue<string> pendingUpgradePopups = new();
        int characterShotgunExtraProjectiles;
        float characterSmgFireRateBonus;
        float characterSmgCooldownReduction;
        float characterFlamethrowerRangeBonus;
        float characterFlamethrowerBurnDurationBonus;

        public System.Action<int> OnPendingUpgradesChanged;

        public event System.Action<PlayerStats> OnPlayerStatsRegistered;
        public event System.Action<XPWallet> OnWalletRgistered;
        public event System.Action<UpgradeUI> OnUIRegistered;

        public int GetPendingUpgradeCount() => pendingUpgrades;
        public System.Collections.Generic.IReadOnlyDictionary<Upgrade, int> GetUpgradeCounts() => upgradeCounts;
        public System.Collections.Generic.IReadOnlyDictionary<Weapon, WeaponUpgradeState> GetWeaponUpgradeStates() => weaponUpgradeStates;

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

        WeaponUpgradeCard RandomWeaponUpgradeCard(System.Collections.Generic.List<WeaponUpgradeCard> pool)
        {
            if (pool == null || pool.Count == 0)
            {
                return null;
            }

            int totalWeight = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                WeaponUpgradeCard candidate = pool[i];
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
                WeaponUpgradeCard candidate = pool[i];
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
            UpgradeUI.OnVisibilityChanged += HandleUpgradeVisibilityChanged;
        }

        void OnDisable()
        {
            Enemy.OnAnyEnemyKilledByWeapon -= HandleWeaponKill;
            SceneReferenceRegistry.Unregister(this);
            UpgradeUI.OnVisibilityChanged -= HandleUpgradeVisibilityChanged;
        }

        void TryAutoFindWallet()
        {
            var found = FindAnyObjectByType<XPWallet>();
            if (found != null)
            {
                RegisterWallet(found);
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
            pendingUpgradePopups.Clear();

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

            if (!CanReceiveUpgrades())
            {
                return;
            }

            pendingUpgrades = Mathf.Min(pendingUpgrades + 1, GetRemainingSelections());
            NotifyPendingChanged();
        }

        void Pick(Upgrade u)
        {
            if (ui == null) return;

            u.Apply(stats);
            QueueUpgradePopup(u);
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

            if (PauseMenuController.IsMenuOpen)
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

            var options = BuildWeaponUpgradeOptions(WeaponCardsPerSelection, out string phaseTitle);
            if (options.Count == 0)
            {
                return false;
            }

            while (options.Count > 0 && options.Count < WeaponCardsPerSelection)
            {
                WeaponUpgradeOption duplicate = options[Random.Range(0, options.Count)];
                options.Add(duplicate);
            }

            Weapon headerWeapon = options[0].Weapon != null ? options[0].Weapon : ResolveFocusWeapon();

            ui.ShowWeaponUpgrades(headerWeapon,
                options[0],
                options.Count > 1 ? options[1] : options[0],
                options.Count > 2 ? options[2] : options[0],
                PickWeaponUpgrade,
                pendingUpgrades,
                phaseTitle);
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

        System.Collections.Generic.List<WeaponUpgradeOption> BuildWeaponUpgradeOptions(int totalCount, out string phaseTitle)
        {
            phaseTitle = null;
            var selections = new System.Collections.Generic.List<WeaponUpgradeOption>();
            if (totalCount <= 0)
            {
                return selections;
            }

            var requests = BuildWeaponCardRequests(totalCount);
            if (requests.Count == 0)
            {
                Weapon fallbackWeapon = ResolveFocusWeapon();
                if (fallbackWeapon != null)
                {
                    requests.Add(new WeaponCardRequest(fallbackWeapon, totalCount));
                }
            }

            phaseTitle = BuildPhaseTitle(requests);

            var cardPool = GetWeaponUpgradeCardPool();
            WeaponUpgradeType[] fallbackTypes =
            {
                WeaponUpgradeType.Damage,
                WeaponUpgradeType.FireRate,
                WeaponUpgradeType.ProjectileSpeed,
                WeaponUpgradeType.FireCooldownReduction,
                WeaponUpgradeType.CritChance,
                WeaponUpgradeType.CritDamage,
                WeaponUpgradeType.Accuracy
            };

            foreach (var request in requests)
            {
                if (request.Weapon == null)
                {
                    continue;
                }

                WeaponUpgradeState state = GetOrCreateWeaponState(request.Weapon);
                int killCount = weaponKillCounts.TryGetValue(request.Weapon, out int kills) ? kills : 0;
                float magnitude = CalculateWeaponUpgradeMagnitude(killCount, state != null ? state.CardsTaken : 0);

                var localPool = FilterCardPool(cardPool, request.Weapon);
                int fallbackIndex = 0;

                for (int i = 0; i < request.CardCount && selections.Count < totalCount; i++)
                {
                    WeaponUpgradeOption option;

                    if (localPool.Count > 0)
                    {
                        WeaponUpgradeCard card = RandomWeaponUpgradeCard(localPool);
                        option = BuildWeaponUpgradeOption(card, request.Weapon, magnitude, killCount, state?.CardsTaken ?? 0);

                        if (localPool.Count > 1 && card != null)
                        {
                            localPool.Remove(card);
                        }
                    }
                    else
                    {
                        WeaponUpgradeType type = fallbackTypes[fallbackIndex % fallbackTypes.Length];
                        option = CreateWeaponUpgradeOption(request.Weapon, type, magnitude, killCount, state?.CardsTaken ?? 0,
                            Upgrade.Rarity.Common);
                        fallbackIndex++;
                    }

                    selections.Add(option);
                }
            }

            return selections;
        }

        System.Collections.Generic.List<WeaponCardRequest> BuildWeaponCardRequests(int totalCount)
        {
            var requests = new System.Collections.Generic.List<WeaponCardRequest>();
            if (totalCount <= 0)
            {
                return requests;
            }

            var rankedWeapons = new System.Collections.Generic.List<WeaponCardRequest>();
            foreach (var pair in weaponKillCounts)
            {
                if (pair.Key == null)
                {
                    continue;
                }

                rankedWeapons.Add(new WeaponCardRequest(pair.Key, pair.Value));
            }

            if (weaponManager != null && weaponManager.CurrentWeapon != null && !rankedWeapons.Exists(r => r.Weapon == weaponManager.CurrentWeapon))
            {
                rankedWeapons.Add(new WeaponCardRequest(weaponManager.CurrentWeapon, 0));
            }

            rankedWeapons.Sort((a, b) => b.KillCount.CompareTo(a.KillCount));
            if (rankedWeapons.Count == 0)
            {
                return requests;
            }

            int weaponSlots = Mathf.Min(totalCount, rankedWeapons.Count);
            int baseCardsPerWeapon = weaponSlots > 0 ? Mathf.Max(1, totalCount / weaponSlots) : 0;
            int cardsRemaining = totalCount;

            for (int i = 0; i < weaponSlots; i++)
            {
                WeaponCardRequest weaponRequest = rankedWeapons[i];
                int assigned = Mathf.Min(baseCardsPerWeapon, cardsRemaining);
                requests.Add(new WeaponCardRequest(weaponRequest.Weapon, assigned, weaponRequest.KillCount));
                cardsRemaining -= assigned;
            }

            if (cardsRemaining > 0)
            {
                DistributeExtraCards(requests, cardsRemaining);
            }

            return requests;
        }

        void DistributeExtraCards(System.Collections.Generic.List<WeaponCardRequest> requests, int extras)
        {
            if (requests == null || requests.Count == 0 || extras <= 0)
            {
                return;
            }

            int totalKills = 0;
            foreach (var request in requests)
            {
                totalKills += Mathf.Max(0, request.KillCount);
            }

            if (totalKills <= 0)
            {
                totalKills = requests.Count;
            }

            var fractions = new System.Collections.Generic.List<(int index, float fraction)>();
            for (int i = 0; i < requests.Count; i++)
            {
                float share = (float)Mathf.Max(0, requests[i].KillCount) / Mathf.Max(1, totalKills);
                fractions.Add((i, share));
            }

            while (extras > 0)
            {
                fractions.Sort((a, b) => b.fraction.CompareTo(a.fraction));
                int targetIndex = fractions[0].index;
                WeaponCardRequest target = requests[targetIndex];
                requests[targetIndex] = new WeaponCardRequest(target.Weapon, target.CardCount + 1, target.KillCount);
                extras--;
            }
        }

        string BuildPhaseTitle(System.Collections.Generic.List<WeaponCardRequest> requests)
        {
            if (requests == null || requests.Count == 0)
            {
                return null;
            }

            var builder = new System.Text.StringBuilder();
            builder.Append("Top weapons: ");

            for (int i = 0; i < requests.Count; i++)
            {
                string name = GetWeaponDisplayName(requests[i].Weapon);
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                builder.Append(name);

                if (i < requests.Count - 1)
                {
                    builder.Append(", ");
                }
            }

            string built = builder.ToString();
            return built.Trim().EndsWith(":", System.StringComparison.Ordinal) ? null : built;
        }

        string GetWeaponDisplayName(Weapon weapon)
        {
            if (weapon == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(weapon.weaponName))
            {
                return weapon.weaponName;
            }

            return weapon.name;
        }

        System.Collections.Generic.List<WeaponUpgradeCard> GetWeaponUpgradeCardPool()
        {
            var pool = new System.Collections.Generic.List<WeaponUpgradeCard>();
            if (weaponUpgradeCards != null && weaponUpgradeCards.Length > 0)
            {
                pool.AddRange(weaponUpgradeCards);
            }

            return pool;
        }

        System.Collections.Generic.List<WeaponUpgradeCard> FilterCardPool(System.Collections.Generic.List<WeaponUpgradeCard> pool, Weapon weapon)
        {
            var filtered = new System.Collections.Generic.List<WeaponUpgradeCard>();
            if (pool == null)
            {
                return filtered;
            }

            foreach (var card in pool)
            {
                if (card == null)
                {
                    continue;
                }

                if (!DoesCardAllowWeapon(card, weapon))
                {
                    continue;
                }

                filtered.Add(card);
            }

            return filtered;
        }

        bool DoesCardAllowWeapon(WeaponUpgradeCard card, Weapon weapon)
        {
            if (card == null || weapon == null)
            {
                return false;
            }

            if (card.OnlyForSemiAuto && weapon.isAuto)
            {
                return false;
            }

            Weapon.WeaponClass resolvedClass = ResolveWeaponClass(weapon);
            WeaponUpgradeCard.WeaponClassFilter weaponMask = ToFilter(resolvedClass);

            if (card.AllowedClasses != WeaponUpgradeCard.WeaponClassFilter.All
                && (card.AllowedClasses & weaponMask) == 0)
            {
                return false;
            }

            return true;
        }

        Weapon.WeaponClass ResolveWeaponClass(Weapon weapon)
        {
            if (weapon == null)
            {
                return Weapon.WeaponClass.General;
            }

            if (weapon.weaponClass != Weapon.WeaponClass.General)
            {
                if (weapon.weaponClass == Weapon.WeaponClass.Flamethrower)
                {
                    return Weapon.WeaponClass.Flamethrower;
                }

                if (weapon.weaponClass == Weapon.WeaponClass.Special || weapon.isSpecial)
                {
                    return Weapon.WeaponClass.Special;
                }

                return weapon.weaponClass;
            }

            if (weapon.isFlamethrower)
            {
                return Weapon.WeaponClass.Flamethrower;
            }

            if (weapon.isSpecial)
            {
                return Weapon.WeaponClass.Special;
            }

            if (!weapon.isAuto)
            {
                return Weapon.WeaponClass.SemiRifle;
            }

            return Weapon.WeaponClass.General;
        }

        WeaponUpgradeCard.WeaponClassFilter ToFilter(Weapon.WeaponClass weaponClass)
        {
            return weaponClass switch
            {
                Weapon.WeaponClass.SemiRifle => WeaponUpgradeCard.WeaponClassFilter.SemiRifle,
                Weapon.WeaponClass.MG => WeaponUpgradeCard.WeaponClassFilter.MG,
                Weapon.WeaponClass.SMG => WeaponUpgradeCard.WeaponClassFilter.SMG,
                Weapon.WeaponClass.Special => WeaponUpgradeCard.WeaponClassFilter.Special,
                Weapon.WeaponClass.Flamethrower => WeaponUpgradeCard.WeaponClassFilter.Flamethrower,
                _ => WeaponUpgradeCard.WeaponClassFilter.General
            };
        }

        float AdjustMagnitudeForType(WeaponUpgradeType type, float magnitude)
        {
            return type switch
            {
                WeaponUpgradeType.Pierce => Mathf.Max(1f, Mathf.Round(magnitude)),
                WeaponUpgradeType.ExtraProjectiles => Mathf.Max(1f, Mathf.Round(magnitude)),
                _ => magnitude
            };
        }

        WeaponUpgradeOption BuildWeaponUpgradeOption(WeaponUpgradeCard card, Weapon weapon, float magnitude, int killCount, int cardsTaken)
        {
            if (card != null)
            {
                float adjustedMagnitude = AdjustMagnitudeForType(card.Type, magnitude);
                return card.BuildOption(weapon, adjustedMagnitude, killCount, cardsTaken);
            }

            return CreateWeaponUpgradeOption(weapon, WeaponUpgradeType.Damage, magnitude, killCount, cardsTaken, Upgrade.Rarity.Common);
        }

        struct WeaponCardRequest
        {
            public Weapon Weapon { get; }
            public int CardCount { get; }
            public int KillCount { get; }

            public WeaponCardRequest(Weapon weapon, int cardCount, int killCount = 0)
            {
                Weapon = weapon;
                CardCount = Mathf.Max(0, cardCount);
                KillCount = Mathf.Max(0, killCount);
            }
        }

        bool IsUpgradeAvailable(Upgrade upgrade)
        {
            if (upgrade == null)
            {
                return false;
            }

            if (!IsUpgradeUnlocked(upgrade))
            {
                return false;
            }

            int takenCount = upgradeCounts.TryGetValue(upgrade, out int timesTaken) ? timesTaken : 0;
            return upgrade.CanApply(stats, takenCount);
        }

        bool IsUpgradeUnlocked(Upgrade upgrade)
        {
            int unlockWave = upgrade.UnlockWave;
            if (unlockWave <= 0)
            {
                return true;
            }

            int currentWave = GameManager.I != null ? GameManager.I.Wave : 0;
            return currentWave >= unlockWave;
        }

        void HandleUpgradeVisibilityChanged(bool isVisible)
        {
            if (!isVisible)
            {
                FlushUpgradePopups();
            }
        }

        void QueueUpgradePopup(Upgrade upgrade)
        {
            if (upgrade == null || stats == null)
            {
                return;
            }

            string text = upgrade.GetPopupText();
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (!UpgradeUI.IsShowing)
            {
                ShowUpgradePopupText(text);
                return;
            }

            pendingUpgradePopups.Enqueue(text);
        }

        void FlushUpgradePopups()
        {
            if (stats == null)
            {
                pendingUpgradePopups.Clear();
                return;
            }

            while (pendingUpgradePopups.Count > 0)
            {
                ShowUpgradePopupText(pendingUpgradePopups.Dequeue());
            }
        }

        void ShowUpgradePopupText(string text)
        {
            DamageNumberManager.ShowText(stats.transform.position, text);
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

        public Weapon GetMostUsedWeapon(out int killCount)
        {
            Weapon bestWeapon = null;
            int bestKills = 0;

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

            killCount = bestKills;
            return bestWeapon;
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
            float killBonus = Mathf.Clamp(killCount, 0, weaponUpgradeKillCap) * killBonusPerKill;
            float stackBonus = Mathf.Min(maxStackBonus, cardsTaken * stackBonusPerCard);
            return baseWeaponUpgradeBonus + killBonus + stackBonus;
        }

        WeaponUpgradeOption CreateWeaponUpgradeOption(
    Weapon weapon,
    WeaponUpgradeType type,
    float magnitude,
    int killCount,
    int cardsTaken,
    Upgrade.Rarity rarity)
        {
            string weaponName = GetWeaponDisplayName(weapon);

            int percentage = Mathf.RoundToInt(magnitude * 100f);
            int flatAmount = Mathf.RoundToInt(magnitude);

            // Titles (NO COLOR  clean text)
            string baseTitle = type switch
            {
                WeaponUpgradeType.Damage => "Damage Boost",
                WeaponUpgradeType.FireRate => "Fire Rate Boost",
                WeaponUpgradeType.ProjectileSpeed => "Bullet Speed Boost",
                WeaponUpgradeType.Pierce => "Piercing Rounds",
                WeaponUpgradeType.ExtraProjectiles => "Multi-Shot",
                WeaponUpgradeType.FireCooldownReduction => "Cooldown Reduction",
                WeaponUpgradeType.CritChance => "Critical Chance",
                WeaponUpgradeType.CritDamage => "Critical Damage",
                WeaponUpgradeType.Accuracy => "Accuracy Boost",
                WeaponUpgradeType.FlamethrowerCooldown => "Faster Venting",
                WeaponUpgradeType.FlamethrowerRange => "Longer Flame",
                _ => "Upgrade"
            };

            // Append weapon name to the title
            string titledWithWeapon = string.IsNullOrEmpty(weaponName)
                ? baseTitle
                : $"{baseTitle}\n({weaponName})";

            // Base descriptions (default UI color)
            string baseDescription = type switch
            {
                WeaponUpgradeType.Damage => "Increase weapon damage by ",
                WeaponUpgradeType.FireRate => "Shoot faster by ",
                WeaponUpgradeType.ProjectileSpeed => "Increase bullet velocity by ",
                WeaponUpgradeType.Pierce => $"Pierce {flatAmount} additional enemies.",
                WeaponUpgradeType.ExtraProjectiles => $"Fire {flatAmount} extra projectile{(flatAmount == 1 ? string.Empty : "s")}.",
                WeaponUpgradeType.FireCooldownReduction => "Reduce weapon cooldown by ",
                WeaponUpgradeType.CritChance => "Increase critical chance by ",
                WeaponUpgradeType.CritDamage => "Increase critical damage by ",
                WeaponUpgradeType.Accuracy => "Improve accuracy by ",
                WeaponUpgradeType.FlamethrowerCooldown => "Decrease overheat recovery by ",
                WeaponUpgradeType.FlamethrowerRange => "Increase flamethrower reach by ",
                _ => "Boost weapon performance by "
            };

            // Choose % color per upgrade type
            string percentColor = type switch
            {
                WeaponUpgradeType.Damage => "#FF4040",
                WeaponUpgradeType.FireRate => "#D17A22",
                WeaponUpgradeType.ProjectileSpeed => "#DBBE50",
                WeaponUpgradeType.Pierce => "#7EC8E3",
                WeaponUpgradeType.ExtraProjectiles => "#A36FF0",
                WeaponUpgradeType.FireCooldownReduction => "#7FD1C9",
                WeaponUpgradeType.CritChance => "#FF9F1C",
                WeaponUpgradeType.CritDamage => "#F94144",
                WeaponUpgradeType.Accuracy => "#4ECDC4",
                WeaponUpgradeType.FlamethrowerCooldown => "#FF8C42",
                WeaponUpgradeType.FlamethrowerRange => "#FFA69E",
                _ => "#FFD966"
            };

            // Build the colored % value
            bool isCooldownType = type == WeaponUpgradeType.FireCooldownReduction || type == WeaponUpgradeType.FlamethrowerCooldown;
            string percentText = isCooldownType ? $"{percentage}%" : $"+{percentage}%";
            string coloredPercent = $"<color={percentColor}>{percentText}</color>.";
            string finalDescription = type switch
            {
                WeaponUpgradeType.Pierce => baseDescription,
                WeaponUpgradeType.ExtraProjectiles => baseDescription,
                _ => baseDescription + coloredPercent
            };

            // Kills (default UI color)
            string extra = $"(Kills: {killCount})";

            return new WeaponUpgradeOption(
                weapon,
                type,
                magnitude,
                titledWithWeapon,
                finalDescription,                   // but UI uses this only in main description
                titledWithWeapon,                   // final title = same as base title (NO COLOR)
                extra,                               // final description shown in EXTRA field
                rarity
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
            float value = TryGetWeaponState(weapon, out var state) ? state.GetFireRateMultiplier() : 1f;
            if (weapon != null && weapon.weaponClass == Weapon.WeaponClass.SMG)
            {
                value *= 1f + characterSmgFireRateBonus;
            }

            return ClampFireRateMultiplier(value);
        }

        public float GetWeaponProjectileSpeedMultiplier(Weapon weapon)
        {
            return TryGetWeaponState(weapon, out var state) ? state.GetProjectileSpeedMultiplier() : 1f;
        }

        public float GetWeaponFireCooldownMultiplier(Weapon weapon)
        {
            float value = TryGetWeaponState(weapon, out var state) ? state.GetFireCooldownMultiplier() : 1f;
            if (weapon != null && weapon.weaponClass == Weapon.WeaponClass.SMG)
            {
                value *= Mathf.Max(0.01f, 1f - characterSmgCooldownReduction);
            }

            return ClampCooldownMultiplier(value);
        }

        public int GetWeaponPierceCount(Weapon weapon)
        {
            return TryGetWeaponState(weapon, out var state) ? state.GetPierceCount() : 0;
        }

        public int GetWeaponExtraProjectiles(Weapon weapon)
        {
            int extra = TryGetWeaponState(weapon, out var state) ? state.GetExtraProjectiles() : 0;
            if (weapon != null && (weapon.isShotgun || weapon.weaponClass == Weapon.WeaponClass.Shotgun))
            {
                extra += characterShotgunExtraProjectiles;
            }

            return extra;
        }

        public float GetWeaponCritChance(Weapon weapon)
        {
            return TryGetWeaponState(weapon, out var state) ? state.GetCritChanceBonus() : 0f;
        }

        public float GetWeaponCritDamageMultiplier(Weapon weapon)
        {
            return TryGetWeaponState(weapon, out var state) ? state.GetCritDamageMultiplier() : 1f;
        }

        public float GetWeaponAccuracyMultiplier(Weapon weapon)
        {
            return TryGetWeaponState(weapon, out var state) ? state.GetAccuracyMultiplier() : 1f;
        }

        public float GetFlamethrowerCooldownMultiplier(Weapon weapon)
        {
            return TryGetWeaponState(weapon, out var state) ? state.GetFlamethrowerCooldownMultiplier() : 1f;
        }

        public float GetFlamethrowerRangeMultiplier(Weapon weapon)
        {
            float multiplier = TryGetWeaponState(weapon, out var state) ? state.GetFlamethrowerRangeMultiplier() : 1f;
            return multiplier * (1f + characterFlamethrowerRangeBonus);
        }

        public float GetCharacterFlamethrowerBurnDurationBonus(Weapon weapon)
        {
            if (weapon == null || !weapon.isFlamethrower)
            {
                return 0f;
            }

            return Mathf.Max(0f, characterFlamethrowerBurnDurationBonus);
        }

        public void SetCharacterWeaponClassBonuses(
            int shotgunExtraProjectiles,
            float smgFireRateBonus,
            float smgCooldownReduction,
            float flamethrowerRangeBonus,
            float flamethrowerBurnDurationBonus)
        {
            characterShotgunExtraProjectiles = Mathf.Max(0, shotgunExtraProjectiles);
            characterSmgFireRateBonus = Mathf.Max(0f, smgFireRateBonus);
            characterSmgCooldownReduction = Mathf.Clamp01(smgCooldownReduction);
            characterFlamethrowerRangeBonus = Mathf.Max(0f, flamethrowerRangeBonus);
            characterFlamethrowerBurnDurationBonus = Mathf.Max(0f, flamethrowerBurnDurationBonus);
        }

        public float ClampFireRateMultiplier(float value)
        {
            return Mathf.Max(0.01f, value);
        }

        public float ClampCooldownMultiplier(float value)
        {
            return Mathf.Max(0.01f, value);
        }

        public float ClampFireRateRpm(float rpm)
        {
            float maxRpm = Mathf.Max(1f, maxFireRateRpm);
            return Mathf.Clamp(rpm, 0.01f, maxRpm);
        }

        public float ClampCooldownSeconds(float cooldownSeconds)
        {
            float minimum = Mathf.Max(0.01f, minFireCooldownSeconds);
            return Mathf.Max(minimum, cooldownSeconds);
        }
    }
}
