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
        [Tooltip("Flat health restored when the pickup is collected.")]
        public int HealAmount = 10;
        [Tooltip("Multiplier applied to the stat while the boost is active.")]
        public float Multiplier = 1.2f;
        [Tooltip("Duration of the temporary boost in seconds. Ignored for healing.")]
        public float Duration = 10f;

        [Header("Presentation")]
        public Color Tint = Color.white;
        public AudioClip PickupSound;
    }
}
