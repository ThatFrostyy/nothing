using UnityEngine;

namespace FF
{
    [CreateAssetMenu(menuName = "FF/Hat", fileName = "Hat_")]
    public class HatDefinition : ScriptableObject
    {
        [Header("Display")]
        public string DisplayName = "New Hat";
        public Sprite Icon;

        [Header("Prefab")]
        public GameObject HatPrefab;
    }
}
