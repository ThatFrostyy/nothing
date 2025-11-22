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
        [SerializeField] private PlayerPreview preview;

        private int _index;
        private int _hatIndex;

        private bool HasCharacters => availableCharacters.Count > 0;

        void OnEnable()
        {
            RemoveNullEntries();
            SyncIndexWithSelection();
            SyncHatWithSelection();
            ApplyCurrentSelectionIfMissing();
            Refresh();
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
            ApplyCurrentSelection();
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
            if (!HasCharacters)
            {
                return;
            }

            _index = Mathf.FloorToInt(Mathf.Repeat(_index + delta, availableCharacters.Count));
            _hatIndex = 0;
            SyncHatWithSelection();
            ApplySelectionAndRefresh();
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
            if (weaponNameText)
            {
                weaponNameText.text = weapon != null ? weapon.weaponName : "No Weapon";
            }

            Sprite weaponIcon = character != null ? character.GetWeaponIcon() : null;
            if (weaponIconImage)
            {
                weaponIconImage.enabled = weaponIcon != null;
                weaponIconImage.sprite = weaponIcon;
            }

            if (preview)
            {
                preview.Show(character, hat, weapon);
            }
        }

        private void StepHat(int delta)
        {
            List<HatDefinition> hats = GetAvailableHats();
            if (hats.Count == 0)
            {
                _hatIndex = 0;
                Refresh();
                return;
            }

            _hatIndex = Mathf.FloorToInt(Mathf.Repeat(_hatIndex + delta, hats.Count));
            ApplySelectionAndRefresh();
        }

        private void SyncHatWithSelection()
        {
            if (!CharacterSelectionState.HasSelection)
            {
                _hatIndex = Mathf.Clamp(_hatIndex, 0, Mathf.Max(GetAvailableHats().Count - 1, 0));
                return;
            }

            CharacterDefinition selectedCharacter = CharacterSelectionState.SelectedCharacter;
            List<HatDefinition> hats = GetAvailableHats();
            if (hats.Count == 0)
            {
                _hatIndex = 0;
                return;
            }

            int found = hats.IndexOf(CharacterSelectionState.SelectedHat);
            _hatIndex = found >= 0 ? found : Mathf.Clamp(_hatIndex, 0, hats.Count - 1);
        }

        private List<HatDefinition> GetAvailableHats() => availableHats ?? new List<HatDefinition>();

        private HatDefinition ResolveHatSelection(CharacterDefinition character)
        {
            List<HatDefinition> hats = GetAvailableHats();
            if (hats.Count == 0)
            {
                return character != null ? character.GetDefaultHat() : null;
            }

            _hatIndex = Mathf.Clamp(_hatIndex, 0, hats.Count - 1);
            return hats[_hatIndex];
        }

        private void ApplySelectionAndRefresh()
        {
            ApplyCurrentSelection();
            Refresh();
        }

        private void ApplyCurrentSelection()
        {
            if (availableCharacters.Count == 0)
            {
                return;
            }

            CharacterDefinition character = availableCharacters[Mathf.Clamp(_index, 0, availableCharacters.Count - 1)];
            HatDefinition hat = ResolveHatSelection(character);
            Weapon weapon = character != null ? character.StartingWeapon : null;

            CharacterSelectionState.SetSelection(character, hat, weapon);
        }

        private void ApplyCurrentSelectionIfMissing()
        {
            if (CharacterSelectionState.HasSelection)
            {
                return;
            }

            ApplyCurrentSelection();
        }

        private void RemoveNullEntries()
        {
            availableCharacters.RemoveAll(character => character == null);
            availableHats.RemoveAll(hat => hat == null);
        }
    }
}
