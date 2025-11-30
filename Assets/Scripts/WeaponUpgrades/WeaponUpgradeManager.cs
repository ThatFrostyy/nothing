using System;
using System.Collections.Generic;
using UnityEngine;

namespace WeaponUpgrades
{
    public class WeaponUpgradeManager : MonoBehaviour
    {
        public static WeaponUpgradeManager Instance { get; private set; }

        private readonly Dictionary<Weapon, WeaponUpgradeRuntimeState> runtimeStates = new Dictionary<Weapon, WeaponUpgradeRuntimeState>();

        public event Action<Weapon, WeaponUpgradeRuntimeState> OnWeaponStateCreated;
        public event Action<Weapon, WeaponUpgradeRuntimeStateChange> OnRuntimeStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public WeaponUpgradeRuntimeState GetOrCreateState(Weapon weapon)
        {
            if (weapon == null)
            {
                Debug.LogWarning("Cannot get runtime state for a null weapon.");
                return null;
            }

            if (!runtimeStates.TryGetValue(weapon, out var state))
            {
                state = new WeaponUpgradeRuntimeState(weapon);
                runtimeStates.Add(weapon, state);
                OnWeaponStateCreated?.Invoke(weapon, state);
            }

            return state;
        }

        public void AddExperience(Weapon weapon, int amount)
        {
            var state = GetOrCreateState(weapon);
            if (state == null)
            {
                return;
            }

            var gainedPoint = state.AddExperience(amount, weapon.ExperienceThresholds);
            if (gainedPoint)
            {
                OnRuntimeStateChanged?.Invoke(weapon, WeaponUpgradeRuntimeStateChange.PointGained);
            }
        }

        public bool TryUnlockNode(Weapon weapon, WeaponUpgradeNode node)
        {
            var state = GetOrCreateState(weapon);
            if (state == null)
            {
                return false;
            }

            if (!state.CanUnlockNode(node) || state.UpgradePoints <= 0)
            {
                return false;
            }

            if (state.UnlockNode(node))
            {
                node.Effect?.Apply(weapon);
                OnRuntimeStateChanged?.Invoke(weapon, WeaponUpgradeRuntimeStateChange.NodeUnlocked);
                return true;
            }

            return false;
        }

        public bool IsNodeUnlocked(Weapon weapon, string nodeId)
        {
            var state = GetOrCreateState(weapon);
            return state != null && state.UnlockedNodes.Contains(nodeId);
        }

        public int GetUpgradePoints(Weapon weapon)
        {
            var state = GetOrCreateState(weapon);
            return state?.UpgradePoints ?? 0;
        }

        public void ClearRuntimeStates()
        {
            runtimeStates.Clear();
        }
    }

    public enum WeaponUpgradeRuntimeStateChange
    {
        PointGained,
        NodeUnlocked
    }
}
