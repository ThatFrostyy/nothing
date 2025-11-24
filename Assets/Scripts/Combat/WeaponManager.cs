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

        readonly WeaponInstance[] loadout = new WeaponInstance[3];
        Weapon currentSO;
        WeaponInstance currentInstance;
        int currentSlotIndex;
        int lastPrimarySlotIndex;

        public Transform GunPivot => gunPivot;
        public AutoShooter Shooter => shooter;
        public Weapon CurrentWeapon => currentSO;
        public WeaponInstance CurrentInstance => currentInstance;
        public int CurrentSlotIndex => currentSlotIndex;
        public int SlotCount => loadout.Length;

        public event Action<WeaponInstance> OnWeaponChanged;
        public event Action OnInventoryChanged;

        public Weapon GetWeaponInSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= loadout.Length)
            {
                return null;
            }

            return loadout[slotIndex]?.Weapon;
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

        void Awake()
        {
            if (!ValidateDependencies())
            {
                Debug.LogError($"{nameof(WeaponManager)} on {name} disabled due to missing dependencies.", this);
                enabled = false;
                return;
            }
        }

        void OnValidate()
        {
            if (!gunPivot)
            {
                Transform foundPivot = transform.Find("GunPivot");
                if (foundPivot)
                {
                    gunPivot = foundPivot;
                }
            }

            if (!shooter)
            {
                shooter = GetComponentInChildren<AutoShooter>();
            }
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
            WeaponInstance existing = loadout[slotIndex];

            if (existing != null && existing.Weapon == weapon)
            {
                OnInventoryChanged?.Invoke();
                return;
            }

            if (existing != null)
            {
                existing.SetActive(false);
                if (existing.Instance)
                {
                    Destroy(existing.Instance);
                }
            }

            loadout[slotIndex] = CreateInstance(weapon);
            OnInventoryChanged?.Invoke();
        }

        void EquipCurrentSlotWeapon()
        {
            if (currentInstance != null)
            {
                currentInstance.SetActive(false);
            }

            currentInstance = loadout[currentSlotIndex];
            currentSO = currentInstance?.Weapon;

            if (currentInstance == null || currentSO == null)
            {
                shooter?.ClearWeapon();
                OnWeaponChanged?.Invoke(null);
                return;
            }

            currentInstance.SetActive(true);

            if (shooter)
            {
                shooter.SetWeapon(currentInstance);
            }
            else
            {
                Debug.LogWarning("WeaponManager is missing a shooter reference.");
            }

            OnWeaponChanged?.Invoke(currentInstance);
        }

        WeaponInstance CreateInstance(Weapon weapon)
        {
            if (!weapon)
            {
                return null;
            }

            if (!weapon.weaponPrefab)
            {
                Debug.LogError($"Weapon '{weapon.name}' is missing a weapon prefab.", this);
                return null;
            }

            GameObject instance = Instantiate(weapon.weaponPrefab, gunPivot);
            instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.SetActive(false);

            Transform muzzle = instance.transform.Find("Muzzle");
            if (!muzzle)
            {
                Debug.LogError("Weapon prefab missing child named 'Muzzle'", instance);
            }

            Transform eject = instance.transform.Find("Eject");
            if (!eject)
            {
                Debug.LogError("Weapon prefab missing child named 'Eject'", instance);
            }

            return new WeaponInstance(weapon, instance, muzzle, eject);
        }

        bool ValidateDependencies()
        {
            bool ok = true;

            if (!gunPivot)
            {
                Debug.LogError("Missing gun pivot reference.", this);
                ok = false;
            }

            if (!shooter)
            {
                Debug.LogError("Missing AutoShooter reference.", this);
                ok = false;
            }

            return ok;
        }
    }
}
