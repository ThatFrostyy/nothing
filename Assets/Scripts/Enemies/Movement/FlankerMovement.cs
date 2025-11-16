using UnityEngine;

namespace FF
{
    [AddComponentMenu("FF/Enemies/Movement/Flanker Movement")]
    public class FlankerMovement : MonoBehaviour, IEnemyMovement
    {
        [Header("Flanker Settings")]
        [SerializeField, Min(0.1f)] private float flankRadius = 5f;
        [SerializeField, Range(-1f, 1f)] private float orbitDirection = 1f;
        [SerializeField, Min(0.1f)] private float strafeSpeedMultiplier = 0.9f;

        public Vector2 GetDesiredVelocity(Enemy enemy, Transform player, EnemyStats stats, Rigidbody2D body, float deltaTime)
        {
            if (!player)
            {
                return Vector2.zero;
            }

            float baseSpeed = stats ? stats.MoveSpeed : 3f;
            Vector2 toPlayer = (Vector2)(player.position - enemy.transform.position);
            float distance = toPlayer.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                return Vector2.zero;
            }

            Vector2 forward = toPlayer / distance;
            Vector2 tangent = new Vector2(-forward.y, forward.x) * Mathf.Sign(orbitDirection);
            Vector2 desired = baseSpeed * strafeSpeedMultiplier * tangent;

            float distanceError = distance - flankRadius;
            desired += 0.5f * baseSpeed * Mathf.Clamp(distanceError, -1f, 1f) * forward;
            return desired;
        }
    }
}
