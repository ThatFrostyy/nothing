using UnityEngine;

namespace FF
{
    public class EnemyStats : MonoBehaviour, ICombatStats
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 3.2f;
        [SerializeField, Range(0f, 1f)] private float acceleration = 0.18f;
        [SerializeField, Range(0f, 1f)] private float retreatSpeedMultiplier = 0.6f;
        [SerializeField] private float bodyTiltDegrees = 12f;

        [Header("Combat")]
        [SerializeField] private float shootingDistance = 10f;
        [SerializeField] private float distanceBuffer = 1.5f;
        [SerializeField] private float fireRateMultiplier = 1f;
        [SerializeField] private float damageMultiplier = 1f;
        [SerializeField] private float movementAccuracyPenalty = 1.3f;

        [Header("Avoidance")]
        [SerializeField] private float avoidanceRadius = 0.85f;
        [SerializeField] private float avoidancePush = 6f;
        [SerializeField, Range(0f, 1f)] private float avoidanceResponsiveness = 0.35f;
        [SerializeField, Range(0f, 2f)] private float avoidanceWeight = 0.75f;

        public float MoveSpeed => moveSpeed;
        public float Acceleration => Mathf.Clamp01(acceleration);
        public float RetreatSpeedMultiplier => Mathf.Clamp01(retreatSpeedMultiplier);
        public float BodyTiltDegrees => bodyTiltDegrees;
        public float ShootingDistance => Mathf.Max(0f, shootingDistance);
        public float DistanceBuffer => Mathf.Max(0f, distanceBuffer);
        public float AvoidanceRadius => Mathf.Max(0f, avoidanceRadius);
        public float AvoidancePush => Mathf.Max(0f, avoidancePush);
        public float AvoidanceResponsiveness => Mathf.Clamp01(avoidanceResponsiveness);
        public float AvoidanceWeight => Mathf.Max(0f, avoidanceWeight);

        public float GetDamageMultiplier() => damageMultiplier;
        public float GetFireRateMultiplier() => fireRateMultiplier;
        public float GetMovementAccuracyPenalty() => movementAccuracyPenalty;
    }
}
