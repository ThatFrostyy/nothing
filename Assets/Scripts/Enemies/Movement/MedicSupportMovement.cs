using UnityEngine;

namespace FF
{
    [AddComponentMenu("FF/Enemies/Movement/Medic Support Movement")]
    public class MedicSupportMovement : MonoBehaviour, IEnemyMovement
    {
        [Header("Positioning")]
        [SerializeField, Min(0.5f)] private float allyFollowDistance = 1.75f;
        [SerializeField, Min(0.5f)] private float allySearchRadius = 8f;
        [SerializeField, Min(0.5f)] private float retreatDistance = 5f;

        [Header("Behaviour")]
        [SerializeField, Range(0f, 1f)] private float woundedPriorityBonus = 0.35f;
        [SerializeField, Range(0.1f, 2f)] private float retreatSpeedMultiplier = 1.15f;

        private readonly Collider2D[] _searchBuffer = new Collider2D[32];

        public Vector2 GetDesiredVelocity(Enemy enemy, Transform player, EnemyStats stats, Rigidbody2D body, float deltaTime)
        {
            if (!enemy)
            {
                return Vector2.zero;
            }

            Enemy targetAlly = FindPreferredAlly(enemy);
            float moveSpeed = stats ? stats.MoveSpeed : 3f;
            float retreatMultiplier = stats ? stats.RetreatSpeedMultiplier : 0.6f;
            Vector2 desired = Vector2.zero;

            if (targetAlly)
            {
                desired = MoveTowardAlly(enemy.transform, targetAlly.transform, moveSpeed);
            }

            bool hasHealTarget = false;
            if (enemy.TryGetComponent(out MedicHealAttack healAttack))
            {
                hasHealTarget = healAttack.IsHealing || healAttack.HasNearbyAllies;
            }

            if (!hasHealTarget && player)
            {
                Vector2 fromPlayer = (Vector2)(enemy.transform.position - player.position);
                float distance = fromPlayer.magnitude;
                if (distance < retreatDistance)
                {
                    Vector2 retreat = fromPlayer.sqrMagnitude > 0.0001f
                        ? fromPlayer.normalized
                        : Random.insideUnitCircle.normalized;
                    float retreatSpeed = moveSpeed * Mathf.Max(retreatMultiplier, 1f) * retreatSpeedMultiplier;
                    desired = retreat * retreatSpeed;
                }
                else if (!targetAlly)
                {
                    // Drift towards the player direction to locate comrades without engaging directly.
                    desired = fromPlayer.sqrMagnitude > 0.0001f ? -fromPlayer.normalized * moveSpeed : Vector2.zero;
                }
            }

            return desired;
        }

        private Vector2 MoveTowardAlly(Transform self, Transform ally, float moveSpeed)
        {
            Vector2 toAlly = ally.position - self.position;
            float distance = toAlly.magnitude;
            if (distance <= Mathf.Max(0.1f, allyFollowDistance))
            {
                return Vector2.zero;
            }

            return toAlly.normalized * moveSpeed;
        }

        private Enemy FindPreferredAlly(Enemy self)
        {
            Vector2 origin = self.transform.position;
            int hits = Physics2D.OverlapCircle(origin, allySearchRadius, _searchBuffer);

            Enemy best = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < hits; i++)
            {
                Collider2D col = _searchBuffer[i];
                if (!col)
                {
                    continue;
                }

                Enemy candidate = col.GetComponentInParent<Enemy>();
                if (!candidate || candidate == self)
                {
                    continue;
                }

                float distance = Vector2.Distance(origin, candidate.transform.position);
                float proximityScore = Mathf.InverseLerp(allySearchRadius, 0f, distance);
                float healthScore = 0f;
                if (candidate.TryGetComponent(out Health health))
                {
                    float missing = Mathf.Max(0, health.MaxHP - health.CurrentHP);
                    float missingRatio = health.MaxHP > 0 ? missing / health.MaxHP : 0f;
                    healthScore = missingRatio * woundedPriorityBonus;
                }

                float totalScore = proximityScore + healthScore;
                if (totalScore > bestScore)
                {
                    bestScore = totalScore;
                    best = candidate;
                }
            }

            return best;
        }
    }
}

