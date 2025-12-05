using System.Collections.Generic;
using UnityEngine;

namespace FF
{
    public class PlayerPreview : MonoBehaviour
    {
        [SerializeField] private PlayerCosmetics cosmetics;
        [SerializeField] private Transform weaponAnchor;

        private GameObject _weaponInstance;
        private readonly List<GameObject> _specialItemInstances = new();

        public void Show(CharacterDefinition character, HatDefinition hat, Weapon weapon, Weapon specialWeapon)
        {
            if (cosmetics)
            {
                cosmetics.Apply(hat ?? character?.GetDefaultHat(), character ? character.PlayerSprite : null);
            }

            UpdateWeapon(weapon ?? character?.StartingWeapon);
            UpdateSpecialWeapon(specialWeapon ?? character?.SpecialWeapon);
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

        private void UpdateSpecialWeapon(Weapon specialWeapon)
        {
            ClearSpecialItems();
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
