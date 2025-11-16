using UnityEngine;

namespace FF
{
    [CreateAssetMenu(menuName = "FF/Upgrade Pickup Effect", fileName = "UpgradePickupEffect_")]
    public class UpgradePickupEffect : ScriptableObject
    {
        public enum EffectType
        {
            Heal,
            DamageBoost,
            MoveSpeedBoost,
            FireRateBoost
        }

        [Header("Effect")]
        public EffectType Type = EffectType.Heal;
        public int HealAmount = 10;
        public float Multiplier = 1.2f;
        public float Duration = 10f;

        [Header("Presentation")]
        public AudioClip PickupSound;
        public Sprite Icon;
    }
}
