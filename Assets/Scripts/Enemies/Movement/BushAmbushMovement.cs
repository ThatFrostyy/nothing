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

        private float _waitTimer;
        private bool _isSneaking;

        public Vector2 GetDesiredVelocity(Enemy enemy, Transform player, EnemyStats stats, Rigidbody2D body, float deltaTime)
        {
            if (!player)
            {
                return Vector2.zero;
            }

            Vector2 toPlayer = (Vector2)player.position - (Vector2)enemy.transform.position;
            float distance = toPlayer.magnitude;

            float triggerDistance = Mathf.Max(0f, ambushDistance);
            if (stats != null)
            {
                triggerDistance = Mathf.Max(triggerDistance, stats.ShootingDistance * 0.75f);
            }

            if (distance <= triggerDistance)
            {
                _waitTimer = 0f;
                _isSneaking = false;
                return Vector2.zero;
            }

            if (!_isSneaking)
            {
                _waitTimer += deltaTime;
                if (_waitTimer >= waitBeforeSneak)
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

            return direction * sneakSpeed;
        }
    }
}
