using System;
using System.Collections.Generic;
using UnityEngine;

namespace WeaponUpgrades
{
    [CreateAssetMenu(menuName = "Upgrades/Weapon Upgrade Tree", fileName = "WeaponUpgradeTree")]
    public class WeaponUpgradeTree : ScriptableObject
    {
        [SerializeField] private List<WeaponUpgradeNode> nodes = new List<WeaponUpgradeNode>();

        public IReadOnlyList<WeaponUpgradeNode> Nodes => nodes;

        public WeaponUpgradeNode GetNodeById(string nodeId)
        {
            return nodes.Find(n => string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
        }
    }
}
