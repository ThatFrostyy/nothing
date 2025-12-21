using System;
using UnityEngine;

namespace FF
{
    public enum CharacterUnlockRequirementType
    {
        StartRun,
        ReachWave,
        ReachWaveWithCharacter,
        TotalKills,
        WeaponKills,
        NoDamageDuration,
        BulletTimeMoments
    }

    [Serializable]
    public class CharacterUnlockRequirement
    {
        public CharacterUnlockRequirementType Type;
        [Min(1)] public int Target = 1;
        public CharacterDefinition RequiredCharacter;
        public Weapon RequiredWeapon;
        [TextArea] public string DescriptionOverride;
    }

    public readonly struct CharacterUnlockRequirementStatus
    {
        public CharacterUnlockRequirementStatus(CharacterUnlockRequirement requirement, int currentValue, bool isCompleted)
        {
            Requirement = requirement;
            CurrentValue = currentValue;
            IsCompleted = isCompleted;
        }

        public CharacterUnlockRequirement Requirement { get; }
        public int CurrentValue { get; }
        public bool IsCompleted { get; }
    }
}
