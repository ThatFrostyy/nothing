using System;
using System.Collections.Generic;
using System.Linq;
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
        readonly List<WeaponPickup> nearbyPickups = new();
        Weapon currentSO;
        GameObject currentWeaponInstance;
        Transform muzzle;
        Transform eject;
        int currentSlotIndex;
        int lastPrimarySlotIndex;
        bool hasNearbyPickups;

        public Transform GunPivot => gunPivot;
        public AutoShooter Shooter => shooter;
        public Weapon CurrentWeapon => currentSO;
        public int CurrentSlotIndex => currentSlotIndex;
        public int SlotCount => loadout.Length;
        public Transform CurrentMuzzle => muzzle;
        public bool HasNearbyPickups => hasNearbyPickups;

        public event Action<Weapon> OnWeaponEquipped;
        public event Action OnInventoryChanged;
        public event Action<bool> OnPickupAvailabilityChanged;

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
            TryEquip(newWeapon, out _, selectSlot: true);
        }

        public bool TryEquip(Weapon newWeapon) => TryEquip(newWeapon, out _, selectSlot: true);

        public bool TryEquip(Weapon newWeapon, bool selectSlot) => TryEquip(newWeapon, out _, selectSlot);

        public bool TryEquip(Weapon newWeapon, out Weapon replacedWeapon, bool selectSlot = true)
        {
            replacedWeapon = null;

            if (!newWeapon)
            {
                return false;
            }

            int targetSlot = ResolveTargetSlot(newWeapon);
            if (!IsSlotValidForWeapon(targetSlot, newWeapon))
            {
                return false;
            }

            Weapon previous = loadout[targetSlot];

            AssignWeaponToSlot(targetSlot, newWeapon);

            if (selectSlot)
            {
                SelectSlot(targetSlot);
            }

            if (previous && previous != newWeapon)
            {
                replacedWeapon = previous;
            }

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

        public void SelectSpecialSlot()
        {
            if (currentSlotIndex == SpecialSlotIndex)
            {
                int targetPrimary = lastPrimarySlotIndex;

                if (loadout[targetPrimary] == null)
                {
                    for (int i = 0; i < PrimarySlotCount; i++)
                    {
                        if (loadout[i] != null)
                        {
                            targetPrimary = i;
                            break;
                        }
                    }
                }

                SelectSlot(targetPrimary);
                return;
            }

            SelectSlot(SpecialSlotIndex);
        }

        public void RegisterNearbyPickup(WeaponPickup pickup)
        {
            if (!pickup)
            {
                return;
            }

            if (!nearbyPickups.Contains(pickup))
            {
                nearbyPickups.Add(pickup);
            }

            CleanupNearbyPickups();
            UpdatePickupAvailability();
        }

        public void UnregisterNearbyPickup(WeaponPickup pickup)
        {
            if (pickup == null)
            {
                nearbyPickups.RemoveAll(p => p == null);
                UpdatePickupAvailability();
                return;
            }

            nearbyPickups.Remove(pickup);
            UpdatePickupAvailability();
        }

        public bool TryCollectNearbyPickup()
        {
            CleanupNearbyPickups();

            WeaponPickup closest = GetClosestPickup();
            if (!closest)
            {
                return false;
            }

            return closest.TryCollect(this);
        }

        int ResolveTargetSlot(Weapon weapon)
        {
            if (weapon.isSpecial)
            {
                return SpecialSlotIndex;
            }

            for (int i = 0; i < PrimarySlotCount; i++)
            {
                if (loadout[i] == null)
                {
                    return i;
                }
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

        void CleanupNearbyPickups()
        {
            nearbyPickups.RemoveAll(p => p == null);
            UpdatePickupAvailability();
        }

        WeaponPickup GetClosestPickup()
        {
            if (nearbyPickups.Count == 0)
            {
                return null;
            }

            WeaponPickup closest = null;
            float closestSqrDist = float.MaxValue;
            Vector3 origin = transform.position;

            foreach (var pickup in nearbyPickups)
            {
                if (!pickup)
                {
                    continue;
                }

                float sqrDist = (pickup.transform.position - origin).sqrMagnitude;
                if (sqrDist < closestSqrDist)
                {
                    closestSqrDist = sqrDist;
                    closest = pickup;
                }
            }

            return closest;
        }

        void UpdatePickupAvailability()
        {
            bool hasPickups = nearbyPickups.Any(p => p != null);
            if (hasPickups == hasNearbyPickups)
            {
                return;
            }

            hasNearbyPickups = hasPickups;
            OnPickupAvailabilityChanged?.Invoke(hasNearbyPickups);
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
