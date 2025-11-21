using UnityEngine;

namespace FF
{
    [CreateAssetMenu(menuName = "FF/Character", fileName = "Character_")]
    public class CharacterDefinition : ScriptableObject
    {
        public string DisplayName = "New Character";
        [TextArea] public string Description;
        [Tooltip("Used to hook up unique abilities later.")]
        public string AbilityId = "Default";
        public Sprite Portrait;
    }
}
