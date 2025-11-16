using UnityEngine;

namespace FF
{
    [AddComponentMenu("FF/Enemies/Movement/Tank Movement")]
    public class TankMovement : MonoBehaviour, IEnemyMovement
    {
        [SerializeField, Min(0.1f)] private float approachSpeed = 0.55f;
        [SerializeField, Min(0.1f)] private float preferredDistance = 4f;
        [SerializeField, Min(0f)] private float distanceBuffer = 0.5f;

        public Vector2 GetDesiredVelocity(Enemy enemy, Transform player, EnemyStats stats, Rigidbody2D body, float deltaTime)
        {
            if (!player)
            {
                return Vector2.zero;
            }

            float speed = (stats ? stats.MoveSpeed : 3f) * approachSpeed;
            Vector2 toPlayer = (Vector2)(player.position - enemy.transform.position);
            float distance = toPlayer.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                return Vector2.zero;
            }

            Vector2 direction = toPlayer / distance;
            if (distance > preferredDistance + distanceBuffer)
            {
                return direction * speed;
            }

            if (distance < preferredDistance - distanceBuffer)
            {
                return -direction * speed * 0.5f;
            }

            return Vector2.zero;
        }
    }
}
