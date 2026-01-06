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

        /// <summary>
        /// Forces the system to discard cached data and re-read from PlayerPrefs.
        /// Call this after applying a Steam Cloud save.
        /// </summary>
        public static void Reload()
        {
            _loaded = false;
            StateByCharacter.Clear();
            EnsureLoaded();
        }

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
            XPWallet wallet,
            CharacterAbilityController abilityController,
            PlayerCombatEffectController combatEffects)
        {
            _ = wallet;
            if (!TryResolveProgression(character, out CharacterProgressionSettings progression, out CharacterProgressionState state))
            {
                XPOrb.SetGlobalAttractionMultipliers(1f, 1f);
                UpgradeManager.I?.SetCharacterWeaponClassBonuses(0, 0f, 0f);
                abilityController?.ConfigureDashChargeBonus(0);
                abilityController?.ConfigureDashFireRateBonus(0f, 0f);
                abilityController?.ConfigureDashImpactBlast(0f, 0f, 0f, 0f);
                combatEffects?.ConfigureProgressionShortRangeDamage(0f, 0f);
                combatEffects?.ConfigureProgressionKillMoveSpeed(0f, 0f);
                combatEffects?.ConfigureProgressionSustainedFireDamage(0f, 0f);
                combatEffects?.ConfigureProgressionRifleBonuses(0f, 0f, 0f);
                combatEffects?.ConfigureProgressionRifleMoveBonuses(0f, 0f);
                combatEffects?.ConfigureProgressionRevive(0f);
                return;
            }

            float moveBonus = 0f;
            float damageBonus = 0f;
            float maxHpBonus = 0f;
            float fireRateBonus = 0f;
            float xpSpeedBonus = 0f;
            float xpRadiusBonus = 0f;
            int dashChargeBonus = 0;
            float dashFireRateBonus = 0f;
            float dashFireRateDuration = 0f;
            float shortRangeDamageBonus = 0f;
            float shortRangeRadius = 0f;
            int maxHpFlatBonus = 0;
            int maxHpRestore = 0;
            int shotgunPelletBonus = 0;
            float smgFireRateBonus = 0f;
            float smgCooldownBonus = 0f;
            float dashImpactDamageBonus = 0f;
            float dashImpactRadius = 0f;
            float dashImpactForce = 0f;
            float dashImpactKnockbackDuration = 0f;
            float startRunMoveBonus = 0f;
            float rifleDamageBonus = 0f;
            float killMoveSpeedBonus = 0f;
            float killMoveSpeedDuration = 0f;
            float rifleFireRateBonus = 0f;
            float rifleProjectileSpeedBonus = 0f;
            float sustainedFireDamageBonus = 0f;
            float sustainedFireDelay = 0f;
            float revivePercent = 0f;
            float rifleMoveDamageBonus = 0f;
            float rifleMoveSpeedBonus = 0f;

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
                    case CharacterUpgradeType.FireRate:
                        fireRateBonus += delta;
                        break;
                    case CharacterUpgradeType.XPPickupSpeed:
                        xpSpeedBonus += delta;
                        break;
                    case CharacterUpgradeType.XPPickupRadius:
                        xpRadiusBonus += delta;
                        break;
                    case CharacterUpgradeType.DashCharge:
                        dashChargeBonus += Mathf.Max(0, reward.FlatAmount);
                        break;
                    case CharacterUpgradeType.ShortRangeDamage:
                        shortRangeDamageBonus += delta;
                        shortRangeRadius = Mathf.Max(shortRangeRadius, reward.Radius);
                        break;
                    case CharacterUpgradeType.DashFireRateBoost:
                        dashFireRateBonus += delta;
                        dashFireRateDuration = Mathf.Max(dashFireRateDuration, reward.DurationSeconds);
                        break;
                    case CharacterUpgradeType.MaxHPOnLevelUp:
                        maxHpFlatBonus += Mathf.Max(0, reward.FlatAmount);
                        maxHpRestore += Mathf.Max(0, reward.SecondaryFlatAmount);
                        break;
                    case CharacterUpgradeType.ShotgunPellets:
                        shotgunPelletBonus += Mathf.Max(0, reward.FlatAmount);
                        break;
                    case CharacterUpgradeType.SmgFireRateAndCooldown:
                        smgFireRateBonus += delta;
                        smgCooldownBonus += reward.GetSecondaryMultiplierDelta();
                        break;
                    case CharacterUpgradeType.DashImpactBlast:
                        dashImpactDamageBonus += delta;
                        dashImpactRadius = Mathf.Max(dashImpactRadius, reward.Radius);
                        dashImpactForce = Mathf.Max(dashImpactForce, reward.KnockbackForce);
                        dashImpactKnockbackDuration = Mathf.Max(dashImpactKnockbackDuration, reward.KnockbackDuration);
                        break;
                    case CharacterUpgradeType.StartRunMoveSpeed:
                        startRunMoveBonus += delta;
                        break;
                    case CharacterUpgradeType.RifleDamage:
                        rifleDamageBonus += delta;
                        break;
                    case CharacterUpgradeType.KillMoveSpeedBoost:
                        killMoveSpeedBonus += delta;
                        killMoveSpeedDuration = Mathf.Max(killMoveSpeedDuration, reward.DurationSeconds);
                        break;
                    case CharacterUpgradeType.RifleFireRateAndProjectileSpeed:
                        rifleFireRateBonus += delta;
                        rifleProjectileSpeedBonus += reward.GetSecondaryMultiplierDelta();
                        break;
                    case CharacterUpgradeType.SustainedFireDamage:
                        sustainedFireDamageBonus += delta;
                        sustainedFireDelay = Mathf.Max(sustainedFireDelay, reward.DurationSeconds);
                        break;
                    case CharacterUpgradeType.ReviveOnDeath:
                        revivePercent += delta;
                        break;
                    case CharacterUpgradeType.RifleMoveDamageAndSpeed:
                        rifleMoveDamageBonus += delta;
                        rifleMoveSpeedBonus += reward.GetSecondaryMultiplierDelta();
                        break;
                }
            }

            if (stats)
            {
                if (moveBonus > 0f)
                {
                    stats.MoveMult += moveBonus;
                }

                if (startRunMoveBonus > 0f)
                {
                    stats.MoveMult += startRunMoveBonus;
                }

                if (damageBonus > 0f)
                {
                    stats.DamageMult += damageBonus;
                }

                if (fireRateBonus > 0f)
                {
                    stats.FireRateMult += fireRateBonus;
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

            Health health = stats ? stats.GetHealth() : null;
            if (health)
            {
                if (maxHpFlatBonus > 0)
                {
                    // Use the new API so the flat bonus survives later ScaleMaxHP calls.
                    health.AddPermanentMaxHP(maxHpFlatBonus, false);
                }

                if (maxHpRestore > 0)
                {
                    health.Heal(maxHpRestore);
                }
            }

            float radiusMultiplier = 1f + xpRadiusBonus;
            float speedMultiplier = 1f + xpSpeedBonus;
            XPOrb.SetGlobalAttractionMultipliers(radiusMultiplier, speedMultiplier);

            UpgradeManager.I?.SetCharacterWeaponClassBonuses(shotgunPelletBonus, smgFireRateBonus, smgCooldownBonus);
            abilityController?.ConfigureDashChargeBonus(dashChargeBonus);
            abilityController?.ConfigureDashFireRateBonus(dashFireRateBonus, dashFireRateDuration);
            abilityController?.ConfigureDashImpactBlast(dashImpactDamageBonus, dashImpactRadius, dashImpactForce, dashImpactKnockbackDuration);
            combatEffects?.ConfigureProgressionShortRangeDamage(shortRangeDamageBonus, shortRangeRadius);
            combatEffects?.ConfigureProgressionKillMoveSpeed(killMoveSpeedBonus, killMoveSpeedDuration);
            combatEffects?.ConfigureProgressionSustainedFireDamage(sustainedFireDamageBonus, sustainedFireDelay);
            combatEffects?.ConfigureProgressionRifleBonuses(rifleDamageBonus, rifleFireRateBonus, rifleProjectileSpeedBonus);
            combatEffects?.ConfigureProgressionRifleMoveBonuses(rifleMoveDamageBonus, rifleMoveSpeedBonus);
            combatEffects?.ConfigureProgressionRevive(Mathf.Clamp01(revivePercent));
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
            catch (Exception)
            {
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
            SteamCloudSave.SaveToCloud();
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
