using System;
using System.Collections.Generic;
using UnityEngine;

namespace FF
{
    public static class CharacterUnlockProgress
    {
        private const string PlayerPrefsKey = "FF_CharacterUnlockProgress_v1";
        private static CharacterUnlockProgressData _data;
        public static event Action OnProgressUpdated;

        public static CharacterUnlockProgressData Data
        {
            get
            {
                EnsureLoaded();
                return _data;
            }
        }

        public static bool IsUnlocked(CharacterDefinition character)
        {
            if (!character)
            {
                return true;
            }

            if (character.UnlockRequirements == null || character.UnlockRequirements.Count == 0)
            {
                return true;
            }

            foreach (CharacterUnlockRequirement requirement in character.UnlockRequirements)
            {
                if (!IsRequirementCompleted(requirement))
                {
                    return false;
                }
            }

            return true;
        }

        public static IReadOnlyList<CharacterUnlockRequirementStatus> GetRequirementStatuses(CharacterDefinition character)
        {
            List<CharacterUnlockRequirementStatus> statuses = new();
            if (!character || character.UnlockRequirements == null)
            {
                return statuses;
            }

            for (int i = 0; i < character.UnlockRequirements.Count; i++)
            {
                CharacterUnlockRequirement requirement = character.UnlockRequirements[i];
                int currentValue = GetRequirementValue(requirement);
                bool completed = IsRequirementCompleted(requirement, currentValue);
                statuses.Add(new CharacterUnlockRequirementStatus(requirement, currentValue, completed));
            }

            return statuses;
        }

        public static string GetRequirementDescription(CharacterUnlockRequirement requirement)
        {
            if (requirement == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(requirement.DescriptionOverride))
            {
                return requirement.DescriptionOverride;
            }

            switch (requirement.Type)
            {
                case CharacterUnlockRequirementType.StartRun:
                    return "Start a run";
                case CharacterUnlockRequirementType.ReachWave:
                    return $"Reach wave {requirement.Target}";
                case CharacterUnlockRequirementType.ReachWaveWithCharacter:
                    return $"Reach wave {requirement.Target} with {GetCharacterName(requirement.RequiredCharacter)}";
                case CharacterUnlockRequirementType.TotalKills:
                    return $"Kill {requirement.Target} enemies";
                case CharacterUnlockRequirementType.WeaponKills:
                    return $"Kill {requirement.Target} enemies with {GetWeaponName(requirement.RequiredWeapon)}";
                case CharacterUnlockRequirementType.NoDamageDuration:
                    return $"Survive without taking damage for {FormatMinutes(requirement.Target)}";
                case CharacterUnlockRequirementType.BulletTimeMoments:
                    return $"Enter {requirement.Target} bullet time moments";
                default:
                    return "Complete requirement";
            }
        }

        public static void RecordRunStarted()
        {
            EnsureLoaded();
            _data.TotalRunsStarted++;
            MarkDirty();
        }

        public static void RecordWaveReached(int wave, CharacterDefinition character)
        {
            EnsureLoaded();
            if (wave > _data.HighestWave)
            {
                _data.HighestWave = wave;
            }

            if (character)
            {
                string key = character.GetProgressionKey();
                CharacterWaveProgress record = GetOrCreateCharacterWaveRecord(key);
                if (wave > record.HighestWave)
                {
                    record.HighestWave = wave;
                }
            }

            MarkDirty();
        }

        public static void RecordKill()
        {
            EnsureLoaded();
            _data.TotalKills++;
            MarkDirty();
        }

        public static void RecordWeaponKill(Weapon weapon)
        {
            if (!weapon)
            {
                return;
            }

            EnsureLoaded();
            string key = GetWeaponKey(weapon);
            WeaponKillProgress record = GetOrCreateWeaponKillRecord(key);
            record.Kills++;
            MarkDirty();
        }

        public static void RecordNoDamageDuration(float seconds)
        {
            EnsureLoaded();
            if (seconds > _data.LongestNoDamageSeconds)
            {
                _data.LongestNoDamageSeconds = seconds;
                MarkDirty();
            }
        }

        public static void RecordBulletTimeMoment()
        {
            EnsureLoaded();
            _data.BulletTimeMoments++;
            MarkDirty();
        }

        public static void SaveIfDirty()
        {
            if (!EnsureLoaded())
            {
                return;
            }

            if (!_data.IsDirty)
            {
                return;
            }

            _data.IsDirty = false;
            string json = JsonUtility.ToJson(_data);
            PlayerPrefs.SetString(PlayerPrefsKey, json);
            PlayerPrefs.Save();
            OnProgressUpdated?.Invoke();
        }

        public static void ForceSave()
        {
            if (!EnsureLoaded())
            {
                return;
            }

            _data.IsDirty = false;
            string json = JsonUtility.ToJson(_data);
            PlayerPrefs.SetString(PlayerPrefsKey, json);
            PlayerPrefs.Save();
            OnProgressUpdated?.Invoke();
        }

        private static bool EnsureLoaded()
        {
            if (_data != null)
            {
                return true;
            }

            if (PlayerPrefs.HasKey(PlayerPrefsKey))
            {
                string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    _data = JsonUtility.FromJson<CharacterUnlockProgressData>(json);
                }
            }

            if (_data == null)
            {
                _data = new CharacterUnlockProgressData();
            }

            _data.IsDirty = false;
            return true;
        }

        private static void MarkDirty()
        {
            if (_data == null)
            {
                return;
            }

            _data.IsDirty = true;
            OnProgressUpdated?.Invoke();
        }

        private static bool IsRequirementCompleted(CharacterUnlockRequirement requirement)
        {
            int currentValue = GetRequirementValue(requirement);
            return IsRequirementCompleted(requirement, currentValue);
        }

        private static bool IsRequirementCompleted(CharacterUnlockRequirement requirement, int currentValue)
        {
            if (requirement == null)
            {
                return true;
            }

            int target = Mathf.Max(1, requirement.Target);
            return currentValue >= target;
        }

        private static int GetRequirementValue(CharacterUnlockRequirement requirement)
        {
            if (requirement == null)
            {
                return 0;
            }

            EnsureLoaded();

            switch (requirement.Type)
            {
                case CharacterUnlockRequirementType.StartRun:
                    return _data.TotalRunsStarted;
                case CharacterUnlockRequirementType.ReachWave:
                    return _data.HighestWave;
                case CharacterUnlockRequirementType.ReachWaveWithCharacter:
                    return GetCharacterWaveBest(requirement.RequiredCharacter);
                case CharacterUnlockRequirementType.TotalKills:
                    return _data.TotalKills;
                case CharacterUnlockRequirementType.WeaponKills:
                    return GetWeaponKillCount(requirement.RequiredWeapon);
                case CharacterUnlockRequirementType.NoDamageDuration:
                    return Mathf.FloorToInt(_data.LongestNoDamageSeconds);
                case CharacterUnlockRequirementType.BulletTimeMoments:
                    return _data.BulletTimeMoments;
                default:
                    return 0;
            }
        }

        private static int GetCharacterWaveBest(CharacterDefinition character)
        {
            if (!character)
            {
                return 0;
            }

            string key = character.GetProgressionKey();
            for (int i = 0; i < _data.CharacterWaves.Count; i++)
            {
                CharacterWaveProgress entry = _data.CharacterWaves[i];
                if (entry.CharacterId == key)
                {
                    return entry.HighestWave;
                }
            }

            return 0;
        }

        private static int GetWeaponKillCount(Weapon weapon)
        {
            if (!weapon)
            {
                return 0;
            }

            string key = GetWeaponKey(weapon);
            for (int i = 0; i < _data.WeaponKills.Count; i++)
            {
                WeaponKillProgress entry = _data.WeaponKills[i];
                if (entry.WeaponId == key)
                {
                    return entry.Kills;
                }
            }

            return 0;
        }

        private static CharacterWaveProgress GetOrCreateCharacterWaveRecord(string key)
        {
            for (int i = 0; i < _data.CharacterWaves.Count; i++)
            {
                if (_data.CharacterWaves[i].CharacterId == key)
                {
                    return _data.CharacterWaves[i];
                }
            }

            CharacterWaveProgress record = new CharacterWaveProgress { CharacterId = key, HighestWave = 0 };
            _data.CharacterWaves.Add(record);
            return record;
        }

        private static WeaponKillProgress GetOrCreateWeaponKillRecord(string key)
        {
            for (int i = 0; i < _data.WeaponKills.Count; i++)
            {
                if (_data.WeaponKills[i].WeaponId == key)
                {
                    return _data.WeaponKills[i];
                }
            }

            WeaponKillProgress record = new WeaponKillProgress { WeaponId = key, Kills = 0 };
            _data.WeaponKills.Add(record);
            return record;
        }

        private static string GetCharacterName(CharacterDefinition character)
        {
            return character ? character.DisplayName : "Unknown";
        }

        private static string GetWeaponName(Weapon weapon)
        {
            if (weapon == null)
            {
                return "Unknown";
            }

            return !string.IsNullOrWhiteSpace(weapon.weaponName) ? weapon.weaponName : weapon.name;
        }

        private static string GetWeaponKey(Weapon weapon)
        {
            return !string.IsNullOrWhiteSpace(weapon.weaponName) ? weapon.weaponName : weapon.name;
        }

        private static string FormatMinutes(int seconds)
        {
            if (seconds < 60)
            {
                return $"{seconds} seconds";
            }

            float minutes = seconds / 60f;
            if (Mathf.Approximately(minutes, Mathf.Round(minutes)))
            {
                return $"{minutes:0} minutes";
            }

            return $"{minutes:0.##} minutes";
        }
    }

    [Serializable]
    public class CharacterUnlockProgressData
    {
        public int TotalRunsStarted;
        public int HighestWave;
        public int TotalKills;
        public int BulletTimeMoments;
        public float LongestNoDamageSeconds;
        public List<CharacterWaveProgress> CharacterWaves = new();
        public List<WeaponKillProgress> WeaponKills = new();
        [NonSerialized] public bool IsDirty;
    }

    [Serializable]
    public class CharacterWaveProgress
    {
        public string CharacterId;
        public int HighestWave;
    }

    [Serializable]
    public class WeaponKillProgress
    {
        public string WeaponId;
        public int Kills;
    }
}
