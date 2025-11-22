using UnityEngine;
using UnityEngine.SceneManagement;


namespace FF
{
    public class UpgradeManager : MonoBehaviour
    {
        public static UpgradeManager I;

        [SerializeField] Upgrade[] all;
        [SerializeField] PlayerStats stats;
        [SerializeField] XPWallet wallet;
        [SerializeField] UpgradeUI ui;
        [SerializeField, Min(0)] int maxUpgradeSelections = 0;

        [Header("Scene Linking")]
        [SerializeField] private bool persistAcrossScenes = true;
        [SerializeField] private bool autoAssignReferencesOnSceneLoad = true;
        [SerializeField] private bool resetUpgradesOnSceneLoad = true;

        int upgradesTaken;
        int pendingUpgrades;
        readonly System.Collections.Generic.Dictionary<Upgrade, int> upgradeCounts = new();

        public System.Action<int> OnPendingUpgradesChanged;

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
            if (I != null)
            {
                Destroy(gameObject);
                return;
            }

            I = this;

            if (persistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }

            AutoAssignReferences();

            if (resetUpgradesOnSceneLoad)
            {
                ResetProgress();
            }

            NotifyPendingChanged();
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            AttachWallet(null);
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
            upgradesTaken++;
            IncrementUpgradeCount(u);
            pendingUpgrades = Mathf.Max(0, pendingUpgrades - 1);
            ui.Hide();
            NotifyPendingChanged();
        }

        public void TryOpenUpgradeMenu()
        {
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

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (autoAssignReferencesOnSceneLoad)
            {
                AutoAssignReferences();
            }

            if (resetUpgradesOnSceneLoad)
            {
                ResetProgress();
            }
        }

        void AutoAssignReferences()
        {
            stats = stats ? stats : FindFirstObjectByType<PlayerStats>();
            ui = ui ? ui : FindFirstObjectByType<UpgradeUI>();
            AttachWallet(wallet ? wallet : FindFirstObjectByType<XPWallet>());
        }

        void AttachWallet(XPWallet newWallet)
        {
            if (wallet == newWallet)
            {
                return;
            }

            if (wallet != null)
            {
                wallet.OnLevelUp -= OnLevel;
            }

            wallet = newWallet;

            if (wallet != null)
            {
                wallet.OnLevelUp += OnLevel;
            }
        }

        void ResetProgress()
        {
            upgradesTaken = 0;
            pendingUpgrades = 0;
            upgradeCounts.Clear();
            NotifyPendingChanged();
        }

        void NotifyPendingChanged()
        {
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
    }
}
