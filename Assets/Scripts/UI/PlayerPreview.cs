using System.Collections.Generic;
using UnityEngine;

namespace FF
{
    public class PlayerPreview : MonoBehaviour
    {
        [SerializeField] private PlayerCosmetics cosmetics;
        [SerializeField] private Transform weaponAnchor;
        [SerializeField] private Transform specialItemAnchor;

        private GameObject _weaponInstance;
        private readonly List<GameObject> _specialItemInstances = new();

        public void Show(CharacterDefinition character, HatDefinition hat, Weapon weapon, SpecialItemDefinition specialWeapon)
        {
            if (cosmetics)
            {
                cosmetics.Apply(hat ?? character?.GetDefaultHat(), character ? character.PlayerSprite : null);
            }

            UpdateWeapon(weapon ?? character?.StartingWeapon);
            UpdateSpecialWeapon(specialWeapon ?? character?.GetStartingSpecialWeapon());
        }

        private void UpdateWeapon(Weapon weapon)
        {
            if (_weaponInstance)
            {
                Destroy(_weaponInstance);
                _weaponInstance = null;
            }

            if (!weaponAnchor || weapon == null || !weapon.weaponPrefab)
            {
                return;
            }

            _weaponInstance = Instantiate(weapon.weaponPrefab, weaponAnchor);
            _weaponInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        private void UpdateSpecialWeapon(SpecialItemDefinition specialWeapon)
        {
            ClearSpecialItems();

            if (specialWeapon == null)
            {
                return;
            }

            if (specialWeapon.ItemPrefab == null)
            {
                return;
            }

            Transform parent = specialItemAnchor ? specialItemAnchor : transform;
            GameObject instance = Instantiate(specialWeapon.ItemPrefab, parent);
            instance.transform.localPosition = specialWeapon.ItemPrefab.transform.localPosition;
            instance.transform.localRotation = specialWeapon.ItemPrefab.transform.localRotation;
            instance.transform.localScale = specialWeapon.ItemPrefab.transform.localScale;
            _specialItemInstances.Add(instance);
        }

        private void ClearSpecialItems()
        {
            for (int i = 0; i < _specialItemInstances.Count; i++)
            {
                if (_specialItemInstances[i])
                {
                    Destroy(_specialItemInstances[i]);
                }
            }

            _specialItemInstances.Clear();
        }
    }
}
