using UnityEngine;

namespace FF
{
    public class ScrapShopManager : MonoBehaviour
    {
        public static event System.Action<Weapon, Weapon> OnWeaponReplaceRequest;
        public static ScrapShopManager Instance { get; private set; }

        [SerializeField] private WeaponManager weaponManager;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        [SerializeField] private System.Collections.Generic.List<Weapon> weaponsForSale;

        public void PurchaseWeapon(Weapon weaponToBuy)
        {
            if (weaponToBuy == null) return;

            int currentScrap = SteamStatsReporter.Instance.GetScrap();
            if (currentScrap < weaponToBuy.scrapCost)
            {
                return;
            }

            if (weaponManager != null)
            {
                if (!weaponManager.TryEquip(weaponToBuy, out var replacedWeapon))
                {
                    OnWeaponReplaceRequest?.Invoke(weaponToBuy, replacedWeapon);
                }
                else
                {
                    SteamStatsReporter.Instance.SpendScrap(weaponToBuy.scrapCost);
                }
            }
        }

        public void ReplaceWeapon(Weapon newWeapon, Weapon oldWeapon)
        {
            if (newWeapon == null || oldWeapon == null) return;

            SteamStatsReporter.Instance.SpendScrap(newWeapon.scrapCost);

            if (weaponManager != null)
            {
                weaponManager.ReplaceWeapon(oldWeapon, newWeapon);
            }
        }
    }
}
