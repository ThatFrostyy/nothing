using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FF
{
    public class CharacterLevelBar : MonoBehaviour
    {
        [SerializeField] private Image progressFill;
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text xpLabel;
        [SerializeField] private Transform levelIconContainer;
        [SerializeField] private CharacterLevelIcon levelIconPrefab;

        private readonly List<CharacterLevelIcon> _spawnedIcons = new();

        public void Show(CharacterDefinition character)
        {
            if (!character || character.Progression == null || character.Progression.LevelCount == 0)
            {
                ClearIcons();
                UpdateLabels(0, 0, 1, 0, false);
                SetProgressFill(0f);
                return;
            }

            CharacterProgressionSnapshot snapshot = CharacterProgressionService.GetSnapshot(character);
            int maxLevel = character.Progression.LevelCount;
            bool isMaxed = snapshot.Level >= maxLevel && maxLevel > 0;
            UpdateLabels(snapshot.Level, snapshot.XPInLevel, snapshot.XPToNext, maxLevel, isMaxed);

            float fill = isMaxed
                ? 1f
                : snapshot.XPToNext > 0 ? (float)snapshot.XPInLevel / snapshot.XPToNext : 0f;
            SetProgressFill(fill);

            BuildLevelIcons(snapshot.Levels, snapshot.Level);
        }

        private void UpdateLabels(int level, int xp, int xpToNext, int maxLevel, bool isMaxed)
        {
            if (levelLabel)
            {
                levelLabel.text = maxLevel > 0
                    ? $"Level {Mathf.Clamp(level, 0, maxLevel)}/{maxLevel}"
                    : "No Levels";
            }

            if (xpLabel)
            {
                if (maxLevel <= 0)
                {
                    xpLabel.text = string.Empty;
                }
                else if (isMaxed)
                {
                    xpLabel.text = "Max Level";
                }
                else
                {
                    xpLabel.text = $"{xp}/{xpToNext} XP";
                }
            }
        }

        private void SetProgressFill(float normalized)
        {
            if (progressFill)
            {
                progressFill.fillAmount = Mathf.Clamp01(normalized);
            }
        }

        private void BuildLevelIcons(IReadOnlyList<CharacterProgressionLevel> levels, int unlockedLevels)
        {
            ClearIcons();

            if (levels == null || levelIconPrefab == null || levelIconContainer == null)
            {
                return;
            }

            for (int i = 0; i < levels.Count; i++)
            {
                CharacterProgressionLevel level = levels[i];
                CharacterLevelIcon icon = Instantiate(levelIconPrefab, levelIconContainer);
                _spawnedIcons.Add(icon);

                bool unlocked = i < unlockedLevels;
                Sprite sprite = level != null ? level.Icon : null;
                icon.Configure(sprite, i + 1, unlocked);
            }
        }

        private void ClearIcons()
        {
            for (int i = 0; i < _spawnedIcons.Count; i++)
            {
                if (_spawnedIcons[i])
                {
                    Destroy(_spawnedIcons[i].gameObject);
                }
            }

            _spawnedIcons.Clear();
        }
    }
}
