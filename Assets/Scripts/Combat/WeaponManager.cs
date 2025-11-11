using UnityEngine;

namespace FF
{
    public class WeaponManager : MonoBehaviour
    {
        [SerializeField] Transform gunPivot;
        [SerializeField] AutoShooter shooter;

        Weapon currentSO;
        GameObject currentWeaponInstance;
        Transform muzzle;

        public Transform GunPivot => gunPivot;
        public AutoShooter Shooter => shooter;

        public void Equip(Weapon newWeapon)
        {
            if (currentWeaponInstance != null)
                Destroy(currentWeaponInstance);

            currentSO = newWeapon;

            currentWeaponInstance = Instantiate(newWeapon.weaponPrefab, gunPivot);
            currentWeaponInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            muzzle = currentWeaponInstance.transform.Find("Muzzle");

            if (!muzzle)
                Debug.LogError("Weapon prefab missing child named 'Muzzle'");

            shooter.InitializeRecoil(gunPivot);
            shooter.SetWeapon(newWeapon, muzzle);
        }
    }
}
