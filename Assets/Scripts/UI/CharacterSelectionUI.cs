using System.Collections.Generic;
using System.Text;
using Steamworks;
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
        [SerializeField] private GameObject lockIconText;
        [SerializeField] private string lockedNameSuffix = " - Locked";
        [SerializeField] private string requirementsHeader = "Unlock Requirements:";

        [Header("Hat Selection")]
        [SerializeField] private List<HatDefinition> availableHats = new();
        [SerializeField] private TMP_Text hatNameText;
        [SerializeField] private TMP_Text hatRarityText;
        [SerializeField] private Image hatIconImage;

        [Header("Steam Inventory")]
        [SerializeField] private List<HatDefinition> steamOnlyHats = new();

        [Header("Loadout Preview")]
        [SerializeField] private TMP_Text weaponNameText;
        [SerializeField] private Image weaponIconImage;
        [SerializeField] private TMP_Text specialItemsText;
        [SerializeField] private List<Image> specialItemIcons = new();
        [SerializeField] private PlayerPreview preview;

        [Header("Progression")]
        [SerializeField] private CharacterLevelBar levelBar;

        private int _index;
        private int _hatIndex;
        private int _loadoutViewIndex;
        private Color _defaultHatRarityColor = Color.white;
        private readonly List<HatDefinition> _ownedSteamHats = new();
        private readonly Dictionary<int, HatDefinition> _hatsByItemDefinitionId = new();
        private Callback<SteamInventoryResultReady_t> _inventoryCallback;
        private SteamInventoryResult_t _currentHandle;

        void OnEnable()
        {
            CacheHatLookup();
            RequestSteamInventory();
            CacheDefaults();
            CharacterSelectionState.OnSelectedChanged += HandleSelectionChanged;
            CharacterUnlockProgress.OnProgressUpdated += HandleProgressUpdated;
            SyncIndexWithSelection();
            SyncHatWithSelection();
            Refresh();
        }

        void OnDisable()
        {
            CharacterSelectionState.OnSelectedChanged -= HandleSelectionChanged;
            CharacterUnlockProgress.OnProgressUpdated -= HandleProgressUpdated;

            if (_inventoryCallback != null)
            {
                _inventoryCallback.Dispose();
                _inventoryCallback = null;
            }
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
            if (character && !CharacterUnlockProgress.IsUnlocked(character))
            {
                Refresh();
                return;
            }

            HatDefinition hat = ResolveHatSelection(character);
            Weapon weapon = character != null ? character.StartingWeapon : null;
            Weapon secondaryWeapon = character != null ? character.SecondaryWeapon : null;
            Weapon specialWeapon = character != null ? character.SpecialWeapon : null;

            CharacterSelectionState.SetSelection(character, hat, weapon, secondaryWeapon, specialWeapon);
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
            const int viewCount = 3;
            _loadoutViewIndex = Mathf.FloorToInt(Mathf.Repeat(_loadoutViewIndex + delta, viewCount));
            Refresh();
        }

        void HandleSelectionChanged(CharacterLoadout _)
        {
            SyncIndexWithSelection();
            SyncHatWithSelection();
            Refresh();
        }

        private void HandleProgressUpdated()
        {
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
            bool isUnlocked = CharacterUnlockProgress.IsUnlocked(character);
            if (nameText) nameText.text = character != null ? GetDisplayName(character, isUnlocked) : "Unknown";
            if (descriptionText) descriptionText.text = character != null
                ? isUnlocked ? character.Description : GetUnlockRequirementsLabel(character)
                : string.Empty;
            if (abilityText) abilityText.text = character != null ? GetAbilityLabel(character.AbilityId) : string.Empty;
            if (portraitImage)
            {
                portraitImage.enabled = character != null && character.Portrait != null;
                portraitImage.sprite = character != null ? character.Portrait : null;
            }

            if (lockIconText)
            {
                lockIconText.SetActive(!isUnlocked);
            }

            HatDefinition hat = ResolveHatSelection(character);
            UpdateHatDisplay(hat);

            Weapon weapon = character != null ? character.StartingWeapon : null;
            Weapon secondaryWeapon = character != null ? character.SecondaryWeapon : null;
            Weapon specialWeapon = character != null ? character.SpecialWeapon : null;
            bool showSecondaryWeapon = _loadoutViewIndex == 1;
            bool showSpecialWeapon = _loadoutViewIndex == 2;
            Weapon displayedWeapon = showSpecialWeapon ? specialWeapon : showSecondaryWeapon ? secondaryWeapon : weapon;

            if (weaponNameText)
            {
                weaponNameText.text = showSpecialWeapon
                    ? GetSpecialWeaponLabel(specialWeapon)
                    : showSecondaryWeapon ? GetWeaponLabel(secondaryWeapon) : GetWeaponLabel(weapon);
            }

            Sprite weaponIcon = showSpecialWeapon
                ? GetSpecialWeaponIcon(specialWeapon)
                : showSecondaryWeapon ? GetWeaponIcon(secondaryWeapon) : character != null ? character.GetWeaponIcon() : null;
            if (weaponIconImage)
            {
                weaponIconImage.enabled = weaponIcon != null;
                weaponIconImage.sprite = weaponIcon;
            }

            if (specialItemsText)
            {
                specialItemsText.text = showSpecialWeapon ? "Special Weapon" : showSecondaryWeapon ? "Secondary Weapon" : "Weapon";
            }

            DisableSpecialItemIcons();

            if (preview)
            {
                // When viewing secondary or special slots, a null displayedWeapon should NOT fall back to the character's primary.
                // Pass allowWeaponFallback = false in those cases so the preview shows "no weapon" visually.
                bool allowWeaponFallback = true;
                if (showSecondaryWeapon && displayedWeapon == null)
                {
                    allowWeaponFallback = false;
                }
                else if (showSpecialWeapon && displayedWeapon == null)
                {
                    allowWeaponFallback = false;
                }

                preview.Show(character, hat, displayedWeapon, specialWeapon, allowWeaponFallback);
            }

            RefreshProgression(character);
        }

        private string GetAbilityLabel(string abilityId)
        {
            if (string.IsNullOrWhiteSpace(abilityId))
            {
                return "Ability: Unknown";
            }

            string lower = abilityId.ToLowerInvariant();
            switch (lower)
            {
                case "dash":
                    return "Ability: Dash (Left Shift / Gamepad B)";
                case "suppression":
                case "suppress":
                case "suppresion":
                    return "Ability: Suppression (slows nearby enemies)";
                case "sharpshooter":
                    return "Ability: Sharpshooter (bonus crits)";
                case "at":
                    return "Ability: Shockwave (explosive stun)";
                default:
                    return $"Ability: {abilityId}";
            }
        }

        private string GetUnlockRequirementsLabel(CharacterDefinition character)
        {
            IReadOnlyList<CharacterUnlockRequirementStatus> statuses = CharacterUnlockProgress.GetRequirementStatuses(character);
            if (statuses.Count == 0)
            {
                return GetAbilityLabel(character.AbilityId);
            }

            StringBuilder builder = new();
            for (int i = 0; i < statuses.Count; i++)
            {
                CharacterUnlockRequirementStatus status = statuses[i];
                string description = CharacterUnlockProgress.GetRequirementDescription(status.Requirement);
                string state = status.IsCompleted ? "<b>Completed</b>" : "<b>In Progress</b>";
                builder.AppendLine($"{description} - {state}");
            }

            return builder.ToString().TrimEnd();
        }

        private string GetDisplayName(CharacterDefinition character, bool isUnlocked)
        {
            if (!character)
            {
                return "Unknown";
            }

            return isUnlocked ? character.DisplayName : $"{character.DisplayName}{lockedNameSuffix}";
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
            List<HatDefinition> hats = new();

            // 1. Character-specific hats (always available if configured for the character)
           if (character != null && character.AvailableHats != null && character.AvailableHats.Count > 0)
            {
                hats.AddRange(character.AvailableHats);
            }
            else
            {
             // 2. Global default hats (always available to all characters)
            hats.AddRange(availableHats);
            }

            // 3. Steam-owned hats (added only if the Steam Inventory reported them as owned)
            AppendSteamHats(hats);

            return hats;
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

        private Sprite GetWeaponIcon(Weapon weapon)
        {
            return weapon != null ? weapon.weaponIcon : null;
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

        private void UpdateHatDisplay(HatDefinition hat)
        {
            if (hatNameText)
            {
                hatNameText.text = hat != null ? hat.DisplayName : "No Hat";
            }

            if (hatRarityText)
            {
                hatRarityText.text = hat != null ? hat.RarityText : string.Empty;
                hatRarityText.color = hat != null ? hat.RarityColor : _defaultHatRarityColor;
            }

            if (hatIconImage)
            {
                hatIconImage.enabled = hat != null && hat.Icon != null;
                hatIconImage.sprite = hat != null ? hat.Icon : null;
            }
        }

        private void RefreshProgression(CharacterDefinition character)
        {
            if (levelBar)
            {
                levelBar.Show(character);
            }
        }

        private void CacheDefaults()
        {
            _defaultHatRarityColor = hatRarityText ? hatRarityText.color : _defaultHatRarityColor;
        }

        private void CacheHatLookup()
        {
            _hatsByItemDefinitionId.Clear();

      // We cache ALL hats (global, character-specific, and steam-only) for the Steam lookup
      foreach (HatDefinition hat in EnumerateAllHats())
            {
                if (!hat || hat.SteamItemDefinitionId == 0)
                {
                    continue;
                }

                if (!_hatsByItemDefinitionId.ContainsKey(hat.SteamItemDefinitionId))
                {
                    _hatsByItemDefinitionId.Add(hat.SteamItemDefinitionId, hat);
                }
            }
        }

        private IEnumerable<HatDefinition> EnumerateAllHats()
        {
            HashSet<HatDefinition> seen = new();

      // Add Global Hats (AvailableHats)
      for (int i = 0; i < availableHats.Count; i++)
            {
                HatDefinition hat = availableHats[i];
                if (hat && seen.Add(hat))
                {
                    yield return hat;
                }
            }

      // Add Hats from Character Definitions (DefaultHat and AvailableHats)
      for (int i = 0; i < availableCharacters.Count; i++)
            {
                CharacterDefinition character = availableCharacters[i];
                if (!character) continue;

                if (character.DefaultHat && seen.Add(character.DefaultHat))
                {
                    yield return character.DefaultHat;
                }

                if (character.AvailableHats == null) continue;

                for (int h = 0; h < character.AvailableHats.Count; h++)
                {
                    HatDefinition hat = character.AvailableHats[h];
                    if (hat && seen.Add(hat))
                    {
                        yield return hat;
                    }
                }
            }

             for (int i = 0; i < steamOnlyHats.Count; i++)
            {
                HatDefinition hat = steamOnlyHats[i];
                if (hat && seen.Add(hat))
                {
                    yield return hat;
                }
            }
        }

        private void AppendSteamHats(List<HatDefinition> hats)
        {
            if (_ownedSteamHats.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _ownedSteamHats.Count; i++)
            {
                HatDefinition ownedHat = _ownedSteamHats[i];
                if (ownedHat && !hats.Contains(ownedHat))
                {
                    hats.Add(ownedHat);
                }
            }
        }

        private void RequestSteamInventory()
        {
            if (!SteamManager.Initialized)
            {
                return;
            }

            // Create the callback if it doesn't exist
            if (_inventoryCallback == null)
            {
                _inventoryCallback = Callback<SteamInventoryResultReady_t>.Create(OnSteamInventoryReady);
            }

            // SteamInventory returns a result handle, NOT an API Call handle.
            // We store this handle to verify the callback later.
            SteamInventory.GetAllItems(out _currentHandle);
        }

        // Note: Callback handlers have a different signature than CallResult handlers
        // (They do not have the 'bool ioFailure' parameter)
        private void OnSteamInventoryReady(SteamInventoryResultReady_t result)
        {
            // Check if this result matches the request we made
            if (result.m_handle != _currentHandle)
            {
                return;
            }

            if (result.m_result != EResult.k_EResultOK)
            {
                // Even on failure, we must destroy the result to free memory
                SteamInventory.DestroyResult(result.m_handle);
                return;
            }

            UpdateOwnedHatsFromInventory(result.m_handle);

            // Clean up memory
            SteamInventory.DestroyResult(result.m_handle);
            _currentHandle = SteamInventoryResult_t.Invalid;

            SyncHatWithSelection();
            Refresh();
        }

        private void UpdateOwnedHatsFromInventory(SteamInventoryResult_t handle)
        {
            _ownedSteamHats.Clear();

            HashSet<HatDefinition> addedHats = new();

            uint itemCount = 0;
            if (!SteamInventory.GetResultItems(handle, null, ref itemCount) || itemCount == 0)
            {
                return;
            }

            SteamItemDetails_t[] items = new SteamItemDetails_t[itemCount];
            if (!SteamInventory.GetResultItems(handle, items, ref itemCount))
            {
                return;
            }

            for (int i = 0; i < itemCount; i++)
            {
                SteamItemDetails_t item = items[i];
                int definitionId = item.m_iDefinition.m_SteamItemDef;

                if (_hatsByItemDefinitionId.TryGetValue(definitionId, out HatDefinition hat) && hat && addedHats.Add(hat))
                {
                    _ownedSteamHats.Add(hat);
                }
            }
        }
    }
}
