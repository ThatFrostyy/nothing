using UnityEngine;

namespace FF
{
    [AddComponentMenu("FF/Enemies/Movement/Fast Runner Movement")]
    public class FastRunnerMovement : MonoBehaviour, IEnemyMovement
    {
        [SerializeField, Min(0.1f)] private float speedMultiplier = 1.4f;
        [SerializeField, Min(0f)] private float minimumChaseDistance = 0f;

        public Vector2 GetDesiredVelocity(Enemy enemy, Transform player, EnemyStats stats, Rigidbody2D body, float deltaTime)
        {
            if (!player)
            {
                return Vector2.zero;
            }

            float baseSpeed = stats ? stats.MoveSpeed : 3f;
            Vector2 toPlayer = (Vector2)(player.position - enemy.transform.position);
            if (toPlayer.sqrMagnitude <= minimumChaseDistance * minimumChaseDistance)
            {
                return Vector2.zero;
            }

            Vector2 direction = toPlayer.sqrMagnitude > Mathf.Epsilon ? toPlayer.normalized : Vector2.zero;
            return direction * baseSpeed * speedMultiplier;
        }
    }
}
