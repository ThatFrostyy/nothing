using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FF
{
    public class CharacterSelectionUI : MonoBehaviour
    {
        [SerializeField] private List<CharacterDefinition> availableCharacters = new();
        [Header("Character Info")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text abilityText;
        [SerializeField] private Image portraitImage;

        [Header("Hat Selection")]
        [SerializeField] private List<HatDefinition> availableHats = new();
        [SerializeField] private TMP_Text hatNameText;
        [SerializeField] private Image hatIconImage;

        [Header("Loadout Preview")]
        [SerializeField] private TMP_Text weaponNameText;
        [SerializeField] private Image weaponIconImage;
        [SerializeField] private TMP_Text specialItemsText;
        [SerializeField] private List<Image> specialItemIcons = new();
        [SerializeField] private PlayerPreview preview;

        private int _index;
        private int _hatIndex;
        private int _loadoutViewIndex;

        void OnEnable()
        {
            CharacterSelectionState.OnSelectedChanged += HandleSelectionChanged;
            SyncIndexWithSelection();
            SyncHatWithSelection();
            Refresh();
        }

        void OnDisable()
        {
            CharacterSelectionState.OnSelectedChanged -= HandleSelectionChanged;
        }

        public void Next()
        {
            Step(1);
        }

        public void Previous()
        {
            Step(-1);
        }

        public void ConfirmSelection()
        {
            if (availableCharacters.Count == 0)
            {
                return;
            }

            CharacterDefinition character = availableCharacters[_index];
            HatDefinition hat = ResolveHatSelection(character);
            Weapon weapon = character != null ? character.StartingWeapon : null;
            Weapon specialWeapon = character != null ? character.SpecialWeapon : null;

            CharacterSelectionState.SetSelection(character, hat, weapon, specialWeapon);
            Refresh();
        }

        public void NextHat()
        {
            StepHat(1);
        }

        public void PreviousHat()
        {
            StepHat(-1);
        }

        private void Step(int delta)
        {
            if (availableCharacters.Count == 0)
            {
                return;
            }

            _index = Mathf.FloorToInt(Mathf.Repeat(_index + delta, availableCharacters.Count));
            SyncHatWithSelection();
            _loadoutViewIndex = 0;
            Refresh();
        }

        public void NextLoadoutDisplay()
        {
            StepLoadoutDisplay(1);
        }

        public void PreviousLoadoutDisplay()
        {
            StepLoadoutDisplay(-1);
        }

        private void StepLoadoutDisplay(int delta)
        {
            const int viewCount = 2;
            _loadoutViewIndex = Mathf.FloorToInt(Mathf.Repeat(_loadoutViewIndex + delta, viewCount));
            Refresh();
        }

        void HandleSelectionChanged(CharacterLoadout _)
        {
            SyncIndexWithSelection();
            SyncHatWithSelection();
            Refresh();
        }

        private void SyncIndexWithSelection()
        {
            if (!CharacterSelectionState.HasSelection || availableCharacters.Count == 0)
            {
                _index = Mathf.Clamp(_index, 0, Mathf.Max(availableCharacters.Count - 1, 0));
                return;
            }

            int found = availableCharacters.IndexOf(CharacterSelectionState.SelectedCharacter);
            if (found >= 0)
            {
                _index = found;
            }
        }

        private void Refresh()
        {
            if (availableCharacters.Count == 0)
            {
                if (nameText) nameText.text = "No characters configured";
                if (descriptionText) descriptionText.text = "Add CharacterDefinition assets to Available Characters.";
                if (abilityText) abilityText.text = string.Empty;
                if (portraitImage) portraitImage.sprite = null;
                if (specialItemsText) specialItemsText.text = string.Empty;
                return;
            }

            CharacterDefinition character = availableCharacters[_index];
            if (nameText) nameText.text = character != null ? character.DisplayName : "Unknown";
            if (descriptionText) descriptionText.text = character != null ? character.Description : string.Empty;
            if (abilityText) abilityText.text = character != null ? $"Ability: {character.AbilityId}" : string.Empty;
            if (portraitImage)
            {
                portraitImage.enabled = character != null && character.Portrait != null;
                portraitImage.sprite = character != null ? character.Portrait : null;
            }

            HatDefinition hat = ResolveHatSelection(character);
            if (hatNameText)
            {
                hatNameText.text = hat != null ? hat.DisplayName : "No Hat";
            }

            if (hatIconImage)
            {
                hatIconImage.enabled = hat != null && hat.Icon != null;
                hatIconImage.sprite = hat != null ? hat.Icon : null;
            }

            Weapon weapon = character != null ? character.StartingWeapon : null;
            Weapon specialWeapon = character != null ? character.SpecialWeapon : null;
            bool showSpecialWeapon = _loadoutViewIndex == 1;

            if (weaponNameText)
            {
                weaponNameText.text = showSpecialWeapon
                    ? GetSpecialWeaponLabel(specialWeapon)
                    : GetWeaponLabel(weapon);
            }

            Sprite weaponIcon = showSpecialWeapon
                ? GetSpecialWeaponIcon(specialWeapon)
                : character != null ? character.GetWeaponIcon() : null;
            if (weaponIconImage)
            {
                weaponIconImage.enabled = weaponIcon != null;
                weaponIconImage.sprite = weaponIcon;
            }

            if (specialItemsText)
            {
                specialItemsText.text = showSpecialWeapon ? "Special Weapon" : "Weapon";
            }

            DisableSpecialItemIcons();

            if (preview)
            {
                preview.Show(character, hat, weapon, specialWeapon);
            }
        }

        private void StepHat(int delta)
        {
            CharacterDefinition character = availableCharacters.Count > 0 ? availableCharacters[_index] : null;
            List<HatDefinition> hats = GetHatsForCharacter(character);
            if (hats.Count == 0)
            {
                _hatIndex = 0;
                Refresh();
                return;
            }

            _hatIndex = Mathf.FloorToInt(Mathf.Repeat(_hatIndex + delta, hats.Count));
            Refresh();
        }

        private void SyncHatWithSelection()
        {
            if (!CharacterSelectionState.HasSelection)
            {
                _hatIndex = Mathf.Clamp(_hatIndex, 0, Mathf.Max(GetHatsForCharacter(null).Count - 1, 0));
                return;
            }

            CharacterDefinition selectedCharacter = CharacterSelectionState.SelectedCharacter;
            List<HatDefinition> hats = GetHatsForCharacter(selectedCharacter);
            if (hats.Count == 0)
            {
                _hatIndex = 0;
                return;
            }

            int found = hats.IndexOf(CharacterSelectionState.SelectedHat);
            _hatIndex = found >= 0 ? found : Mathf.Clamp(_hatIndex, 0, hats.Count - 1);
        }

        private List<HatDefinition> GetHatsForCharacter(CharacterDefinition character)
        {
            if (character != null && character.AvailableHats != null && character.AvailableHats.Count > 0)
            {
                return character.AvailableHats;
            }

            return availableHats;
        }

        private HatDefinition ResolveHatSelection(CharacterDefinition character)
        {
            List<HatDefinition> hats = GetHatsForCharacter(character);
            if (hats.Count == 0)
            {
                return character != null ? character.GetDefaultHat() : null;
            }

            _hatIndex = Mathf.Clamp(_hatIndex, 0, hats.Count - 1);
            return hats[_hatIndex];
        }

        private string GetWeaponLabel(Weapon weapon)
        {
            if (weapon != null && !string.IsNullOrEmpty(weapon.weaponName))
            {
                return weapon.weaponName;
            }

            return weapon != null ? weapon.name : "No Weapon";
        }

        private string GetSpecialWeaponLabel(Weapon specialWeapon)
        {
            if (specialWeapon != null)
            {
                return specialWeapon.weaponName;
            }

            return "No Special Weapon";
        }

        private Sprite GetSpecialWeaponIcon(Weapon specialWeapon)
        {
            return specialWeapon != null ? specialWeapon.weaponIcon : null;
        }

        private void DisableSpecialItemIcons()
        {
            if (specialItemIcons == null)
            {
                return;
            }

            for (int i = 0; i < specialItemIcons.Count; i++)
            {
                if (specialItemIcons[i])
                {
                    specialItemIcons[i].enabled = false;
                    specialItemIcons[i].sprite = null;
                }
            }
        }
    }
}
