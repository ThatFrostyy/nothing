using System;

namespace FF
{
    public static class CharacterSelectionState
    {
        public static event Action<CharacterDefinition> OnSelectedChanged;

        public static CharacterDefinition SelectedCharacter { get; private set; }

        public static bool HasSelection => SelectedCharacter != null;

        public static void SetSelection(CharacterDefinition character)
        {
            if (character == SelectedCharacter)
            {
                return;
            }

            SelectedCharacter = character;
            OnSelectedChanged?.Invoke(SelectedCharacter);
        }
    }
}
