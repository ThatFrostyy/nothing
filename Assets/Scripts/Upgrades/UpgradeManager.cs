using UnityEngine;


namespace FF
{
    public class UpgradeManager : MonoBehaviour
    {
        [SerializeField] Upgrade[] all;
        [SerializeField] PlayerStats stats;
        [SerializeField] XPWallet wallet;
        [SerializeField] UpgradeUI ui;
        [SerializeField, Min(0)] int maxUpgradeSelections = 0;

        int upgradesTaken;
        int pendingUpgrades;

        public System.Action<int> OnPendingUpgradesChanged;

        Upgrade RandomUpgrade() => all[Random.Range(0, all.Length)];

        void Awake()
        {
            wallet.OnLevelUp += OnLevel;
            NotifyPendingChanged();
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

            ui.Show(RandomUpgrade(), RandomUpgrade(), RandomUpgrade(), Pick, pendingUpgrades);
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
            OnPendingUpgradesChanged?.Invoke(pendingUpgrades);
        }
    }
}