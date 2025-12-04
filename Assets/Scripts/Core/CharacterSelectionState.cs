using System;
using System.Collections.Generic;

namespace FF
{
    public static class CharacterSelectionState
    {
        public static event Action<CharacterLoadout> OnSelectedChanged;

        public static CharacterLoadout Selection { get; private set; } = CharacterLoadout.Empty;

        public static CharacterDefinition SelectedCharacter => Selection.Character;
        public static HatDefinition SelectedHat => Selection.Hat;
        public static Weapon SelectedWeapon => Selection.Weapon;
        public static IReadOnlyList<SpecialItemDefinition> SelectedSpecialItems => Selection.SpecialItems;

        public static bool HasSelection => Selection.Character != null;

        public static void SetSelection(
            CharacterDefinition character,
            HatDefinition hat = null,
            Weapon weapon = null,
            IReadOnlyList<SpecialItemDefinition> specialItems = null)
        {
            HatDefinition resolvedHat = hat ?? character?.GetDefaultHat();
            Weapon resolvedWeapon = weapon ?? character?.StartingWeapon;
            IReadOnlyList<SpecialItemDefinition> resolvedItems = specialItems ?? character?.GetStartingSpecialItems();

            CharacterLoadout newSelection = new(character, resolvedHat, resolvedWeapon, resolvedItems);

            if (newSelection.Equals(Selection))
            {
                return;
            }

            Selection = newSelection;
            OnSelectedChanged?.Invoke(Selection);
        }
    }

    public readonly struct CharacterLoadout : IEquatable<CharacterLoadout>
    {
        public static readonly CharacterLoadout Empty = new(null, null, null, Array.Empty<SpecialItemDefinition>());

        public readonly CharacterDefinition Character;
        public readonly HatDefinition Hat;
        public readonly Weapon Weapon;
        public readonly IReadOnlyList<SpecialItemDefinition> SpecialItems;

        public CharacterLoadout(
            CharacterDefinition character,
            HatDefinition hat,
            Weapon weapon,
            IReadOnlyList<SpecialItemDefinition> specialItems)
        {
            Character = character;
            Hat = hat;
            Weapon = weapon;
            SpecialItems = specialItems ?? Array.Empty<SpecialItemDefinition>();
        }

        public bool Equals(CharacterLoadout other)
        {
            return Character == other.Character
                && Hat == other.Hat
                && Weapon == other.Weapon
                && AreSpecialItemsEqual(SpecialItems, other.SpecialItems);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterLoadout other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Character ? Character.GetHashCode() : 0;
                hash = (hash * 397) ^ (Hat ? Hat.GetHashCode() : 0);
                hash = (hash * 397) ^ (Weapon ? Weapon.GetHashCode() : 0);

                if (SpecialItems != null)
                {
                    for (int i = 0; i < SpecialItems.Count; i++)
                    {
                        hash = (hash * 397) ^ (SpecialItems[i] ? SpecialItems[i].GetHashCode() : 0);
                    }
                }

                return hash;
            }
        }

        static bool AreSpecialItemsEqual(
            IReadOnlyList<SpecialItemDefinition> a,
            IReadOnlyList<SpecialItemDefinition> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;

            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i]) return false;
            }

            return true;
        }
    }
}
