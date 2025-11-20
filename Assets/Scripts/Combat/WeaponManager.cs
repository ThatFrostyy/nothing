using System;
using UnityEngine;

namespace FF
{
    public class WeaponManager : MonoBehaviour
    {
        private const int PrimarySlotCount = 2;
        private const int SpecialSlotIndex = 2;

        [Header("References")]
        [SerializeField] Transform gunPivot;
        [SerializeField] AutoShooter shooter;

        readonly Weapon[] loadout = new Weapon[3];
        Weapon currentSO;
        GameObject currentWeaponInstance;
        Transform muzzle;
        Transform eject;
        int currentSlotIndex;
        int lastPrimarySlotIndex;

        public Transform GunPivot => gunPivot;
        public AutoShooter Shooter => shooter;
        public Weapon CurrentWeapon => currentSO;
        public int CurrentSlotIndex => currentSlotIndex;
        public int SlotCount => loadout.Length;

        public event Action<Weapon> OnWeaponEquipped;
        public event Action OnInventoryChanged;

        public Weapon GetWeaponInSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= loadout.Length)
            {
                return null;
            }

            return loadout[slotIndex];
        }

        public void Equip(Weapon newWeapon)
        {
            TryEquip(newWeapon);
        }

        public bool TryEquip(Weapon newWeapon)
        {
            if (!newWeapon)
            {
                return false;
            }

            int targetSlot = ResolveTargetSlot(newWeapon);
            if (!IsSlotValidForWeapon(targetSlot, newWeapon))
            {
                Debug.LogWarning($"Weapon '{newWeapon.name}' does not fit in slot {targetSlot}.");
                return false;
            }

            AssignWeaponToSlot(targetSlot, newWeapon);
            SelectSlot(targetSlot);
            return true;
        }

        public void SelectNextSlot()
        {
            int next = (currentSlotIndex + 1) % loadout.Length;
            SelectSlot(next);
        }

        public void SelectPreviousSlot()
        {
            int previous = (currentSlotIndex - 1 + loadout.Length) % loadout.Length;
            SelectSlot(previous);
        }

        public void SelectSlot(int slotIndex)
        {
            int clampedIndex = Mathf.Clamp(slotIndex, 0, loadout.Length - 1);
            currentSlotIndex = clampedIndex;

            if (currentSlotIndex < PrimarySlotCount)
            {
                lastPrimarySlotIndex = currentSlotIndex;
            }

            EquipCurrentSlotWeapon();
        }

        int ResolveTargetSlot(Weapon weapon)
        {
            if (weapon.isSpecial)
            {
                return SpecialSlotIndex;
            }

            return currentSlotIndex >= PrimarySlotCount ? lastPrimarySlotIndex : currentSlotIndex;
        }

        bool IsSlotValidForWeapon(int slotIndex, Weapon weapon)
        {
            if (!weapon)
            {
                return false;
            }

            bool slotIsSpecial = slotIndex == SpecialSlotIndex;
            return slotIsSpecial == weapon.isSpecial;
        }

        void AssignWeaponToSlot(int slotIndex, Weapon weapon)
        {
            loadout[slotIndex] = weapon;
            OnInventoryChanged?.Invoke();
        }

        void EquipCurrentSlotWeapon()
        {
            if (currentWeaponInstance != null)
            {
                Destroy(currentWeaponInstance);
            }

            currentSO = loadout[currentSlotIndex];

            if (!currentSO)
            {
                if (shooter)
                {
                    shooter.ClearWeapon();
                }

                OnWeaponEquipped?.Invoke(currentSO);
                return;
            }

            currentWeaponInstance = Instantiate(currentSO.weaponPrefab, gunPivot);
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

            if (shooter)
            {
                shooter.InitializeRecoil(gunPivot);
                shooter.SetWeapon(currentSO, muzzle, eject);
            }
            else
            {
                Debug.LogWarning("WeaponManager is missing a shooter reference.");
            }

            OnWeaponEquipped?.Invoke(currentSO);
        }
    }
}
