using UnityEngine;
using UnityEngine.AI;

namespace FF
{
    [AddComponentMenu("FF/Enemies/Movement/Flanker Movement (NavMesh)")]
    public class FlankerMovement : MonoBehaviour, IEnemyMovement
    {
        [Header("Flanker Settings")]
        [SerializeField, Min(0.1f)] private float flankRadius = 5f;
        [SerializeField] private float flankAngle = 90f;
        [SerializeField] private float repositionSpeed = 1.1f;
        [SerializeField] private float attackSpeed = 1.5f;

        private float chosenAngle;
        private bool angleChosen = false;

        public Vector2 GetDesiredVelocity(
            Enemy enemy,
            Transform player,
            EnemyStats stats,
            NavMeshAgent agent,
            float deltaTime)
        {
            if (!player)
                return Vector2.zero;

            float baseSpeed = stats ? stats.MoveSpeed : 3f;

            Vector2 enemyPos = enemy.transform.position;
            Vector2 toPlayer = (Vector2)(player.position) - enemyPos;
            float distance = toPlayer.magnitude;

            if (distance < 0.001f)
                return Vector2.zero;

            Vector2 forward = toPlayer.normalized;

            // 1. Choose flank angle once
            if (!angleChosen)
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                chosenAngle = flankAngle * side;
                angleChosen = true;
            }

            // 2. Rotate forward vector
            float rad = chosenAngle * Mathf.Deg2Rad;
            Vector2 flankDir =
                new Vector2(
                    forward.x * Mathf.Cos(rad) - forward.y * Mathf.Sin(rad),
                    forward.x * Mathf.Sin(rad) + forward.y * Mathf.Cos(rad)
                );

            // 3. Compute flank target
            Vector2 flankTarget = (Vector2)player.position + flankDir * flankRadius;
            Vector2 towardFlank = flankTarget - enemyPos;

            // 4. Move toward flank
            if (towardFlank.magnitude > 0.4f)
                return towardFlank.normalized * baseSpeed * repositionSpeed;

            // 5. Then attack directly
            if (distance > 1.5f)
                return forward * baseSpeed * attackSpeed;

            // 6. Reset flank cycle when close
            angleChosen = false;

            return Vector2.zero;
        }
    }
}
