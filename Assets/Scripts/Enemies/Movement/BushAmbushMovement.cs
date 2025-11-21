using UnityEngine;

namespace FF
{
    [AddComponentMenu("FF/Enemies/Movement/Bush Ambush Movement")]
    public class BushAmbushMovement : MonoBehaviour, IEnemyMovement
    {
        [Header("Ambush Settings")]
        [SerializeField, Min(0f)] private float ambushDistance = 4.5f;
        [SerializeField, Min(0f)] private float waitBeforeSneak = 5f;
        [SerializeField, Range(0f, 1f)] private float sneakSpeedMultiplier = 0.35f;
        [SerializeField, Min(0f)] private float closeStopDistance = 3f;
        [SerializeField, Min(0f)] private float reengageDistance = 6f;
        [SerializeField, Min(0f)] private float fireReleaseDistance = 3.25f;

        private float _waitTimer;
        private bool _isSneaking;
        private float _lastDistance;

        public bool ShouldHoldFire => !_isSneaking || _lastDistance > Mathf.Max(closeStopDistance, fireReleaseDistance);

        public Vector2 GetDesiredVelocity(Enemy enemy, Transform player, EnemyStats stats, Rigidbody2D body, float deltaTime)
        {
            if (!player)
            {
                return Vector2.zero;
            }

            Vector2 toPlayer = (Vector2)player.position - (Vector2)enemy.transform.position;
            float distance = toPlayer.magnitude;
            _lastDistance = distance;

            float triggerDistance = Mathf.Max(0f, ambushDistance);
            if (stats != null)
            {
                triggerDistance = Mathf.Max(triggerDistance, stats.ShootingDistance * 0.75f);
            }

            float stopDistance = Mathf.Max(closeStopDistance, triggerDistance * 0.6f);
            float resumeDistance = Mathf.Max(stopDistance + 0.5f, reengageDistance);

            if (distance <= stopDistance)
            {
                _waitTimer = 0f;
                _isSneaking = false;
                return Vector2.zero;
            }

            if (!_isSneaking)
            {
                _waitTimer += deltaTime;
                if (_waitTimer >= waitBeforeSneak || distance >= resumeDistance)
                {
                    _isSneaking = true;
                }
            }

            if (!_isSneaking || distance <= Mathf.Epsilon)
            {
                return Vector2.zero;
            }

            float baseSpeed = stats ? stats.MoveSpeed : 3f;
            float sneakSpeed = baseSpeed * Mathf.Clamp01(sneakSpeedMultiplier);
            Vector2 direction = toPlayer / distance;

            if (distance <= resumeDistance)
            {
                return direction * (sneakSpeed * 0.65f);
            }

            return direction * sneakSpeed;
        }

        void OnValidate()
        {
            ambushDistance = Mathf.Max(0f, ambushDistance);
            waitBeforeSneak = Mathf.Max(0f, waitBeforeSneak);
            sneakSpeedMultiplier = Mathf.Clamp01(sneakSpeedMultiplier);
            closeStopDistance = Mathf.Max(0f, closeStopDistance);
            reengageDistance = Mathf.Max(closeStopDistance, reengageDistance);
            fireReleaseDistance = Mathf.Max(0f, fireReleaseDistance);
        }
    }
}
