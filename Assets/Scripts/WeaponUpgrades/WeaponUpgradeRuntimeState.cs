using System.Collections.Generic;
using UnityEngine;

namespace WeaponUpgrades
{
    public class WeaponUpgradeRuntimeState
    {
        public Weapon Weapon { get; }
        public int CurrentExperience { get; private set; }
        public int UpgradePoints { get; private set; }
        public IReadOnlyCollection<string> UnlockedNodes => unlockedNodes;

        private readonly HashSet<string> unlockedNodes = new HashSet<string>();
        private int nextThresholdIndex;

        public WeaponUpgradeRuntimeState(Weapon weapon)
        {
            Weapon = weapon;
            CurrentExperience = 0;
            UpgradePoints = 0;
            nextThresholdIndex = 0;
        }

        public bool AddExperience(int amount, IReadOnlyList<int> thresholds)
        {
            CurrentExperience += amount;
            var gainedPoint = false;

            while (thresholds != null && nextThresholdIndex < thresholds.Count && CurrentExperience >= thresholds[nextThresholdIndex])
            {
                UpgradePoints++;
                nextThresholdIndex++;
                gainedPoint = true;
            }

            return gainedPoint;
        }

        public bool CanUnlockNode(WeaponUpgradeNode node)
        {
            if (node == null || unlockedNodes.Contains(node.Id))
            {
                return false;
            }

            foreach (var prerequisite in node.PrerequisiteIds)
            {
                if (!unlockedNodes.Contains(prerequisite))
                {
                    return false;
                }
            }

            return true;
        }

        public bool UnlockNode(WeaponUpgradeNode node)
        {
            if (!CanUnlockNode(node) || UpgradePoints <= 0)
            {
                return false;
            }

            UpgradePoints--;
            unlockedNodes.Add(node.Id);
            return true;
        }
    }
}
