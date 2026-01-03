using System.Collections.Generic;
using UnityEngine;

namespace FF
{//
    [CreateAssetMenu(menuName = "FF/Character", fileName = "Character_")]
    public class CharacterDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Optional override used to save progression for this character. Defaults to the asset name.")]
        public string ProgressionId = string.Empty;
        public string DisplayName = "New Character";
        [TextArea] public string Description;
        [Tooltip("Used to hook up unique abilities later.")]
        public string AbilityId = "Default";
        public Sprite Portrait;

        [Header("Cosmetics")]
        public Sprite PlayerSprite;
        public HatDefinition DefaultHat;
        public List<HatDefinition> AvailableHats = new();

        [Header("Loadout")]
        public Weapon StartingWeapon;
        [Tooltip("Overrides the icon displayed in the menu. If left empty, the weapon's own icon is used.")]
        public Sprite WeaponIconOverride;
        [Header("Special Weapon")]
        public Weapon SpecialWeapon;

        [Header("Unlocking")]
        public List<CharacterUnlockRequirement> UnlockRequirements = new();

        [Header("Progression")]
        public CharacterProgressionSettings Progression = new();

        [Header("Testing")]
        [Tooltip("When enabled the character will be treated as unlocked regardless of requirements (editor/testing only).")]
        public bool ForceUnlocked = false;

        public string GetProgressionKey()
        {
            return string.IsNullOrWhiteSpace(ProgressionId) ? name : ProgressionId;
        }

        public Sprite GetWeaponIcon()
        {
            if (WeaponIconOverride)
            {
                return WeaponIconOverride;
            }

            return StartingWeapon ? StartingWeapon.weaponIcon : null;
        }

        public HatDefinition GetDefaultHat()
        {
            if (DefaultHat)
            {
                return DefaultHat;
            }

            if (AvailableHats != null && AvailableHats.Count > 0)
            {
                return AvailableHats[0];
            }

            return null;
        }

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(ProgressionId))
            {
                ProgressionId = name;
            }
        }
    }
}
