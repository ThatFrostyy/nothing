using UnityEngine;


namespace FF
{
    public class UpgradeManager : MonoBehaviour
    {
        [SerializeField] Upgrade[] all;
        [SerializeField] PlayerStats stats;
        [SerializeField] XPWallet wallet;
        [SerializeField] UpgradeUI ui;

        Upgrade RandomUpgrade() => all[Random.Range(0, all.Length)];

        void Awake()
        {
            wallet.OnLevelUp += OnLevel;
        }

        void OnLevel(int lvl)
        {
            if (ui == null) return;

            ui.Show(RandomUpgrade(), RandomUpgrade(), RandomUpgrade(), Pick);
        }

        void Pick(Upgrade u)
        {
            if (ui == null) return;

            u.Apply(stats);
            ui.Hide();
        }
    }
}