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

        const int WeaponCardsPerSelection = 3;

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
                WeaponUpgradeType.ProjectileSpeed
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
                        int pickIndex = Random.Range(0, localPool.Count);
                        WeaponUpgradeCard card = localPool[pickIndex];
                        option = BuildWeaponUpgradeOption(card, request.Weapon, magnitude, killCount, state?.CardsTaken ?? 0);

                        if (localPool.Count > 1)
                        {
                            localPool.RemoveAt(pickIndex);
                        }
                    }
                    else
                    {
                        WeaponUpgradeType type = fallbackTypes[fallbackIndex % fallbackTypes.Length];
                        option = CreateWeaponUpgradeOption(request.Weapon, type, magnitude, killCount, state?.CardsTaken ?? 0);
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
                if (weapon.weaponClass == Weapon.WeaponClass.Special || weapon.isSpecial)
                {
                    return Weapon.WeaponClass.Special;
                }

                return weapon.weaponClass;
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

            return CreateWeaponUpgradeOption(weapon, WeaponUpgradeType.Damage, magnitude, killCount, cardsTaken);
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
                _ => "Upgrade"
            };

            // Append weapon name to the title
            string titledWithWeapon = string.IsNullOrEmpty(weaponName)
                ? baseTitle
                : $"{baseTitle}\n\n({weaponName})";

            // Base descriptions (default UI color)
            string baseDescription = type switch
            {
                WeaponUpgradeType.Damage => "Increase weapon damage by ",
                WeaponUpgradeType.FireRate => "Shoot faster by ",
                WeaponUpgradeType.ProjectileSpeed => "Increase bullet velocity by ",
                WeaponUpgradeType.Pierce => $"Pierce {flatAmount} additional enemies.",
                WeaponUpgradeType.ExtraProjectiles => $"Fire {flatAmount} extra projectile{(flatAmount == 1 ? string.Empty : "s")}.",
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
                _ => "#FFD966"
            };

            // Build the colored % value
            string coloredPercent = $"<color={percentColor}>+{percentage}%</color>.";
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

        public int GetWeaponPierceCount(Weapon weapon)
        {
            return TryGetWeaponState(weapon, out var state) ? state.GetPierceCount() : 0;
        }

        public int GetWeaponExtraProjectiles(Weapon weapon)
        {
            return TryGetWeaponState(weapon, out var state) ? state.GetExtraProjectiles() : 0;
        }
    }
}