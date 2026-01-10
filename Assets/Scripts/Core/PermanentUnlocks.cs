using System.Collections.Generic;
using UnityEngine;

namespace FF
{
    public class PermanentUnlocks : MonoBehaviour
    {
        public static PermanentUnlocks Instance { get; private set; }

        private const string UnlockedWeaponsKey = "PermanentUnlocks_Weapons";

        [SerializeField] private List<Weapon> unlockedWeapons = new List<Weapon>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadUnlockedWeapons();
        }

        public void UnlockWeapon(Weapon weapon)
        {
            if (weapon == null || unlockedWeapons.Contains(weapon))
            {
                return;
            }

            unlockedWeapons.Add(weapon);
            SaveUnlockedWeapons();
        }

        public bool IsWeaponUnlocked(Weapon weapon)
        {
            return unlockedWeapons.Contains(weapon);
        }

        private void SaveUnlockedWeapons()
        {
            string data = "";
            for (int i = 0; i < unlockedWeapons.Count; i++)
            {
                data += unlockedWeapons[i].guid;
                if (i < unlockedWeapons.Count - 1)
                {
                    data += ",";
                }
            }
            PlayerPrefs.SetString(UnlockedWeaponsKey, data);
        }

        private void LoadUnlockedWeapons()
        {
            var allWeapons = Resources.LoadAll<Weapon>("Weapons");
            if (PlayerPrefs.HasKey(UnlockedWeaponsKey))
            {
                string[] weaponGuids = PlayerPrefs.GetString(UnlockedWeaponsKey).Split(',');
                foreach (var weaponGuid in weaponGuids)
                {
                    foreach (var weapon in allWeapons)
                    {
                        if (weapon.guid == weaponGuid && !unlockedWeapons.Contains(weapon))
                        {
                            unlockedWeapons.Add(weapon);
                            break;
                        }
                    }
                }
            }
        }
    }
}
