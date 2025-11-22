using UnityEngine;

namespace FF
{
    [AddComponentMenu("FF/Enemies/Movement/Flanker Movement")]
    public class FlankerMovement : MonoBehaviour, IEnemyMovement
    {
        [Header("Flanker Settings")]
        [SerializeField, Min(0.1f)] private float flankRadius = 5f;
        [SerializeField] private float flankAngle = 90f; // degrees left/right
        [SerializeField] private float repositionSpeed = 1.1f;
        [SerializeField] private float attackSpeed = 1.5f;

        private float chosenAngle;
        private bool angleChosen = false;

        public Vector2 GetDesiredVelocity(Enemy enemy, Transform player, EnemyStats stats, float deltaTime)
        {
            if (!player)
                return Vector2.zero;

            float baseSpeed = stats ? stats.MoveSpeed : 3f;

            Vector2 enemyPos = enemy.transform.position;
            Vector2 toPlayer = (Vector2)(player.position) - enemyPos;
            float distance = toPlayer.magnitude;

            if (distance <= Mathf.Epsilon)
                return Vector2.zero;

            Vector2 forward = toPlayer.normalized;

            // ---- 1. Choose a flank angle ONCE per flank cycle ----
            if (!angleChosen)
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                chosenAngle = flankAngle * side;
                angleChosen = true;
            }

            // ---- 2. Compute flank direction (rotate forward vector) ----
            float rad = chosenAngle * Mathf.Deg2Rad;

            Vector2 flankDir = new Vector2(
                forward.x * Mathf.Cos(rad) - forward.y * Mathf.Sin(rad),
                forward.x * Mathf.Sin(rad) + forward.y * Mathf.Cos(rad)
            );

            // Target flank position
            Vector2 flankTarget = (Vector2)player.position + flankDir * flankRadius;

            Vector2 towardFlank = flankTarget - enemyPos;
            float flankDist = towardFlank.magnitude;

            // ---- 3. Move to flank position ----
            if (flankDist > 0.4f)
            {
                return baseSpeed * repositionSpeed * towardFlank.normalized;
            }

            // ---- 4. Attack run toward the player ----
            // Only when positioned at flank
            if (distance > 1.5f)
            {
                return attackSpeed * baseSpeed * forward;
            }

            // ---- 5. Reset flanking cycle when close to player ----
            angleChosen = false;

            return Vector2.zero;
        }
    }
}
