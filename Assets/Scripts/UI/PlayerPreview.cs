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

        public void Show(CharacterDefinition character, HatDefinition hat, Weapon weapon)
        {
            if (cosmetics)
            {
                cosmetics.Apply(hat ?? character?.GetDefaultHat(), character ? character.PlayerSprite : null);
            }

            UpdateWeapon(weapon ?? character?.StartingWeapon);
            UpdateSpecialItems(character != null ? character.GetStartingSpecialItems() : null);
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

        private void UpdateSpecialItems(IReadOnlyList<SpecialItemDefinition> items)
        {
            ClearSpecialItems();

            if (items == null || items.Count == 0)
            {
                return;
            }

            Transform parent = specialItemAnchor ? specialItemAnchor : transform;
            for (int i = 0; i < items.Count; i++)
            {
                SpecialItemDefinition item = items[i];
                if (item == null || item.ItemPrefab == null)
                {
                    continue;
                }

                GameObject instance = Instantiate(item.ItemPrefab, parent);
                instance.transform.localPosition = item.ItemPrefab.transform.localPosition;
                instance.transform.localRotation = item.ItemPrefab.transform.localRotation;
                instance.transform.localScale = item.ItemPrefab.transform.localScale;
                _specialItemInstances.Add(instance);
            }
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
