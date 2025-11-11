using UnityEngine;


namespace FF
{
    public class PlayerStats : MonoBehaviour
    {
        [Header("Base Stats")]
        public float MoveSpeed = 6f;
        public float FireRateRPM = 450f;
        public float Damage = 10f;
        public float MovementAccuracyPenalty = 1.5f;


        [Header("Multipliers (from Upgrades)")]
        public float MoveMult = 1f;
        public float FireRateMult = 1f;
        public float DamageMult = 1f;

        public float GetMoveSpeed() => MoveSpeed * MoveMult;
        public float GetRPM() => FireRateRPM * FireRateMult;
        public int GetDamageInt() => Mathf.RoundToInt(Damage * DamageMult);
    }
}