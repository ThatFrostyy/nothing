using System;
using System.Collections.Generic;
using UnityEngine;

namespace FF
{
    public static class CharacterProgressionService
    {
        private const string PlayerPrefsKey = "FF_CharacterProgression_v1";

        private static readonly Dictionary<string, CharacterProgressionState> StateByCharacter = new();
        private static bool _loaded;

        public static CharacterProgressionSnapshot GetSnapshot(CharacterDefinition character)
        {
            if (!TryResolveProgression(character, out CharacterProgressionSettings progression, out CharacterProgressionState state))
            {
                return new CharacterProgressionSnapshot(0, 0, 1, Array.Empty<CharacterProgressionLevel>());
            }

            return progression.CreateSnapshot(state);
        }

        public static CharacterProgressionState AddRunExperience(CharacterDefinition character, int highestWave, int totalKills)
        {
            if (!TryResolveProgression(character, out CharacterProgressionSettings progression, out CharacterProgressionState state))
            {
                return null;
            }

            int awardedXP = progression.CalculateRunXP(highestWave, totalKills);
            if (awardedXP <= 0)
            {
                return state;
            }

            return AddExperience(character, progression, state, awardedXP);
        }

        public static CharacterProgressionState AddExperience(CharacterDefinition character, int amount)
        {
            if (!TryResolveProgression(character, out CharacterProgressionSettings progression, out CharacterProgressionState state))
            {
                return null;
            }

            return AddExperience(character, progression, state, amount);
        }

        public static void ApplyPermanentUpgrades(
            CharacterDefinition character,
            PlayerStats stats,
            XPWallet wallet)
        {
            _ = wallet;
            if (!TryResolveProgression(character, out CharacterProgressionSettings progression, out CharacterProgressionState state))
            {
                XPOrb.SetGlobalAttractionMultipliers(1f, 1f);
                return;
            }

            float moveBonus = 0f;
            float damageBonus = 0f;
            float maxHpBonus = 0f;
            float xpSpeedBonus = 0f;
            float xpRadiusBonus = 0f;

            foreach (CharacterUpgradeReward reward in progression.GetUnlockedRewards(state.Level))
            {
                if (reward == null)
                {
                    continue;
                }

                float delta = reward.GetMultiplierDelta();
                switch (reward.Type)
                {
                    case CharacterUpgradeType.MoveSpeed:
                        moveBonus += delta;
                        break;
                    case CharacterUpgradeType.AttackDamage:
                        damageBonus += delta;
                        break;
                    case CharacterUpgradeType.MaxHP:
                        maxHpBonus += delta;
                        break;
                    case CharacterUpgradeType.XPPickupSpeed:
                        xpSpeedBonus += delta;
                        break;
                    case CharacterUpgradeType.XPPickupRadius:
                        xpRadiusBonus += delta;
                        break;
                }
            }

            if (stats)
            {
                if (moveBonus > 0f)
                {
                    stats.MoveMult += moveBonus;
                }

                if (damageBonus > 0f)
                {
                    stats.DamageMult += damageBonus;
                }

                if (maxHpBonus > 0f)
                {
                    stats.MaxHealthMult += maxHpBonus;
                    if (stats.GetHealth())
                    {
                        stats.GetHealth().ScaleMaxHP(stats.MaxHealthMult, false);
                    }
                }
            }

            float radiusMultiplier = 1f + xpRadiusBonus;
            float speedMultiplier = 1f + xpSpeedBonus;
            XPOrb.SetGlobalAttractionMultipliers(radiusMultiplier, speedMultiplier);
        }

        public static CharacterProgressionState GetState(CharacterDefinition character)
        {
            if (character == null)
            {
                return null;
            }

            EnsureLoaded();

            string key = character.GetProgressionKey();
            if (!StateByCharacter.TryGetValue(key, out CharacterProgressionState state))
            {
                state = new CharacterProgressionState(key);
                StateByCharacter[key] = state;
                Save();
            }

            return state;
        }

        private static CharacterProgressionState AddExperience(
            CharacterDefinition character,
            CharacterProgressionSettings progression,
            CharacterProgressionState state,
            int amount)
        {
            if (amount <= 0 || progression == null || state == null)
            {
                return state;
            }

            int maxLevel = progression.LevelCount;
            if (maxLevel <= 0)
            {
                return state;
            }

            state.XP += amount;

            while (state.Level < maxLevel)
            {
                int targetLevel = state.Level + 1;
                int requirement = progression.GetXPRequirementForLevel(targetLevel);
                if (state.XP < requirement)
                {
                    break;
                }

                state.XP -= requirement;
                state.Level++;
            }

            if (state.Level >= maxLevel)
            {
                state.Level = maxLevel;
                state.XP = 0;
            }

            Save();
            return state;
        }

        private static bool TryResolveProgression(
            CharacterDefinition character,
            out CharacterProgressionSettings progression,
            out CharacterProgressionState state)
        {
            progression = null;
            state = null;

            if (character == null || character.Progression == null || character.Progression.LevelCount == 0)
            {
                return false;
            }

            progression = character.Progression;
            state = GetState(character);
            return state != null;
        }

        #region Persistence
        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;

            string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            try
            {
                var wrapper = JsonUtility.FromJson<CharacterProgressionSaveData>(json);
                if (wrapper?.Entries == null)
                {
                    return;
                }

                StateByCharacter.Clear();
                for (int i = 0; i < wrapper.Entries.Count; i++)
                {
                    CharacterProgressionState entry = wrapper.Entries[i];
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.CharacterId))
                    {
                        StateByCharacter[entry.CharacterId] = entry;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to load character progression: {e.Message}");
                StateByCharacter.Clear();
            }
        }

        private static void Save()
        {
            var wrapper = new CharacterProgressionSaveData();
            wrapper.Entries.AddRange(StateByCharacter.Values);

            string json = JsonUtility.ToJson(wrapper);
            PlayerPrefs.SetString(PlayerPrefsKey, json);
            PlayerPrefs.Save();
        }
        #endregion
    }

    [Serializable]
    public class CharacterProgressionState
    {
        public string CharacterId;
        public int Level;
        public int XP;

        public CharacterProgressionState()
        {
        }

        public CharacterProgressionState(string characterId)
        {
            CharacterId = characterId;
            Level = 0;
            XP = 0;
        }
    }

    [Serializable]
    internal class CharacterProgressionSaveData
    {
        public List<CharacterProgressionState> Entries = new();
    }
}
