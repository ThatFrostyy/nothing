using UnityEngine;


namespace FF
{
    public class UpgradeManager : MonoBehaviour
    {
        [SerializeField] Upgrade[] all;
        [SerializeField] PlayerStats stats;
        [SerializeField] XPWallet wallet;
        [SerializeField] UpgradeUI ui;

        void Awake()
        {
            wallet.OnLevelUp += OnLevel;
        }


        void OnLevel(int lvl)
        {
            ui?.Show(RandomUpgrade(), RandomUpgrade(), RandomUpgrade(), Pick);
        }


        Upgrade RandomUpgrade() => all[Random.Range(0, all.Length)];


        void Pick(Upgrade u)
        {
            u.Apply(stats);
            ui?.Hide();
        }
    }
}