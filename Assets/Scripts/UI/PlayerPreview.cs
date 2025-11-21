using UnityEngine;

namespace FF
{
    public class PlayerPreview : MonoBehaviour
    {
        [SerializeField] private PlayerCosmetics cosmetics;
        [SerializeField] private Transform weaponAnchor;

        private GameObject _weaponInstance;

        public void Show(CharacterDefinition character, HatDefinition hat, Weapon weapon)
        {
            if (cosmetics)
            {
                cosmetics.Apply(hat ?? character?.GetDefaultHat(), character ? character.PlayerSprite : null);
            }

            UpdateWeapon(weapon ?? character?.StartingWeapon);
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
    }
}
