using System;

namespace FF
{
    public static class CharacterSelectionState
    {
        public static event Action<CharacterLoadout> OnSelectedChanged;

        public static CharacterLoadout Selection { get; private set; } = CharacterLoadout.Empty;

        public static CharacterDefinition SelectedCharacter => Selection.Character;
        public static HatDefinition SelectedHat => Selection.Hat;
        public static Weapon SelectedWeapon => Selection.Weapon;

        public static bool HasSelection => Selection.Character != null;

        public static void SetSelection(CharacterDefinition character, HatDefinition hat = null, Weapon weapon = null)
        {
            HatDefinition resolvedHat = hat ?? character?.GetDefaultHat();
            Weapon resolvedWeapon = weapon ?? character?.StartingWeapon;

            CharacterLoadout newSelection = new(character, resolvedHat, resolvedWeapon);

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
        public static readonly CharacterLoadout Empty = new(null, null, null);

        public readonly CharacterDefinition Character;
        public readonly HatDefinition Hat;
        public readonly Weapon Weapon;

        public CharacterLoadout(CharacterDefinition character, HatDefinition hat, Weapon weapon)
        {
            Character = character;
            Hat = hat;
            Weapon = weapon;
        }

        public bool Equals(CharacterLoadout other)
        {
            return Character == other.Character && Hat == other.Hat && Weapon == other.Weapon;
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
                return hash;
            }
        }
    }
}
