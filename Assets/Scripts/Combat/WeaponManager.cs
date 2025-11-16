using System;
using UnityEngine;

namespace FF
{
    public class WeaponManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] Transform gunPivot;
        [SerializeField] AutoShooter shooter;

        Weapon currentSO;
        GameObject currentWeaponInstance;
        Transform muzzle;
        Transform eject;

        public Transform GunPivot => gunPivot;
        public AutoShooter Shooter => shooter;
        public Weapon CurrentWeapon => currentSO;

        public event Action<Weapon> OnWeaponEquipped;

        public void Equip(Weapon newWeapon)
        {
            if (!newWeapon)
            {
                return;
            }

            if (currentWeaponInstance != null)
            {
                Destroy(currentWeaponInstance);
            }

            currentSO = newWeapon;

            currentWeaponInstance = Instantiate(newWeapon.weaponPrefab, gunPivot);
            currentWeaponInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            muzzle = currentWeaponInstance.transform.Find("Muzzle");
            if (!muzzle)
            {
                Debug.LogError("Weapon prefab missing child named 'Muzzle'");
            }

            eject = currentWeaponInstance.transform.Find("Eject");
            if (!eject)
            {
                Debug.LogError("Weapon prefab missing child named 'Eject'");
            }

            shooter.InitializeRecoil(gunPivot);
            shooter.SetWeapon(newWeapon, muzzle, eject);

            OnWeaponEquipped?.Invoke(currentSO);
        }
    }
}
