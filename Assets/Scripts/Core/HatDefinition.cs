using UnityEngine;

namespace FF
{
    [CreateAssetMenu(menuName = "FF/Hat", fileName = "Hat_")]
    public class HatDefinition : ScriptableObject
    {
        [Header("Display")]
        public string DisplayName = "New Hat";
        public Sprite Icon;
        [Tooltip("Shown next to the hat name when browsing the hat selector.")]
        public string RarityText = "Common";
        [Tooltip("Color applied to the rarity text.")]
        public Color RarityColor = Color.white;

        [Header("Prefab")]
        public GameObject HatPrefab;

        [Header("Steam Inventory")]
        [Tooltip("Steam item definition ID for this hat (if it can be owned through Steam inventory). Leave as 0 if not applicable.")]
        public int SteamItemDefinitionId;
    }
}
