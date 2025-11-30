using System.Collections.Generic;
using UnityEngine;

namespace WeaponUpgrades
{
    [System.Serializable]
    public class WeaponUpgradeNode
    {
        [SerializeField] private string id = "node_id";
        [SerializeField] private Sprite icon;
        [SerializeField] private string title;
        [TextArea]
        [SerializeField] private string description;
        [SerializeField] private Vector2 uiPosition;
        [SerializeField] private List<string> prerequisiteIds = new List<string>();
        [SerializeField] private WeaponUpgradeEffectBase effect;

        public string Id => id;
        public Sprite Icon => icon;
        public string Title => title;
        public string Description => description;
        public Vector2 UiPosition => uiPosition;
        public IReadOnlyList<string> PrerequisiteIds => prerequisiteIds;
        public WeaponUpgradeEffectBase Effect => effect;
    }
}
