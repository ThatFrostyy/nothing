using System.Collections.Generic;
using UnityEngine;

namespace FF
{//
    [CreateAssetMenu(menuName = "FF/Character", fileName = "Character_")]
    public class CharacterDefinition : ScriptableObject
    {
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
    }
}
