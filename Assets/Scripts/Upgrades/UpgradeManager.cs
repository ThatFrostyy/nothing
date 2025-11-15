using UnityEngine;

namespace FF
{
    public class UpgradeManager : MonoBehaviour
    {
        [SerializeField] private Upgrade[] all;
        [SerializeField] private PlayerStats stats;
        [SerializeField] private XPWallet wallet;
        [SerializeField] private UpgradeUI ui;

        private void OnEnable()
        {
            if (wallet != null)
            {
                wallet.OnLevelUp += OnLevel;
            }
            else
            {
                Debug.LogWarning("UpgradeManager wallet reference is not assigned.", this);
            }
        }

        private void OnDisable()
        {
            if (wallet != null)
            {
                wallet.OnLevelUp -= OnLevel;
            }
        }

        private Upgrade RandomUpgrade()
        {
            if (all == null || all.Length == 0)
            {
                Debug.LogWarning("No upgrades configured for UpgradeManager.", this);
                return null;
            }

            return all[Random.Range(0, all.Length)];
        }

        private void OnLevel(int lvl)
        {
            if (ui == null)
            {
                Debug.LogWarning("UpgradeUI reference is missing on UpgradeManager.", this);
                return;
            }

            var first = RandomUpgrade();
            var second = RandomUpgrade();
            var third = RandomUpgrade();

            if (first == null || second == null || third == null)
            {
                return;
            }

            ui.Show(first, second, third, Pick);
        }

        private void Pick(Upgrade upgrade)
        {
            if (ui == null || upgrade == null)
            {
                return;
            }

            if (stats == null)
            {
                Debug.LogWarning("PlayerStats reference is missing on UpgradeManager.", this);
                return;
            }

            upgrade.Apply(stats);
            ui.Hide();
        }
    }
}
