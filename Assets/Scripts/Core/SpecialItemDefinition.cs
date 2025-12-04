using UnityEngine;

namespace FF
{
    [CreateAssetMenu(menuName = "FF/Special Item", fileName = "SpecialItem_")]
    public class SpecialItemDefinition : ScriptableObject
    {
        [Header("Display")]
        public string DisplayName = "New Special Item";
        [TextArea]
        public string Description;
        public Sprite Icon;

        [Header("Prefab")]
        public GameObject ItemPrefab;
    }
}
