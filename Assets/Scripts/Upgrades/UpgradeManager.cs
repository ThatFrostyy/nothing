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

        Upgrade RandomUpgrade() => all[Random.Range(0, all.Length)];

        void Awake()
        {
            wallet.OnLevelUp += OnLevel;
        }

        void OnLevel(int lvl)
        {
            if (ui == null) return;

            if (maxUpgradeSelections > 0 && upgradesTaken >= maxUpgradeSelections)
            {
                return;
            }

            if (all == null || all.Length == 0)
            {
                return;
            }

            ui.Show(RandomUpgrade(), RandomUpgrade(), RandomUpgrade(), Pick);
        }

        void Pick(Upgrade u)
        {
            if (ui == null) return;

            u.Apply(stats);
            upgradesTaken++;
            ui.Hide();
        }
    }
}