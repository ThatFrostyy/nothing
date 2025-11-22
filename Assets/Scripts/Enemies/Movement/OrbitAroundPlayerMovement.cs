using UnityEngine;

namespace FF
{
    [AddComponentMenu("FF/Enemies/Movement/Orbit Around Player Movement")]
    public class OrbitAroundPlayerMovement : MonoBehaviour, IEnemyMovement
    {
        [Header("Orbit Around Player Settings")]
        [SerializeField, Min(0.1f)] private float orbitRadius = 6f;
        [SerializeField, Min(0.1f)] private float orbitSpeedMultiplier = 0.75f;
        [SerializeField, Range(-1f, 1f)] private float orbitDirection = 1f;

        public Vector2 GetDesiredVelocity(Enemy enemy, Transform player, EnemyStats stats, float deltaTime)
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
            Vector2 desired = baseSpeed * orbitSpeedMultiplier * tangent;

            float correction = Mathf.Clamp(distance - orbitRadius, -1f, 1f);
            desired += baseSpeed * correction * forward;
            return desired;
        }
    }
}
