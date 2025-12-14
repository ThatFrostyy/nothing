using UnityEngine;

namespace FF
{
    [AddComponentMenu("FF/Enemies/Movement/Fake Retreat Movement")]
    [RequireComponent(typeof(Health))]
    public class FakeRetreatMovement : MonoBehaviour, IEnemyMovement
    {
        [Header("Retreat")]
        [SerializeField, Range(0.05f, 0.95f)] private float lowHealthThreshold = 0.35f;
        [SerializeField, Min(0.05f)] private float retreatDuration = 1.25f;
        [SerializeField, Min(0.1f)] private float retreatSpeedMultiplier = 1.2f;

        [Header("Charge")]
        [SerializeField, Min(0.05f)] private float chargeDuration = 0.85f;
        [SerializeField, Min(0.1f)] private float chargeSpeedMultiplier = 2.6f;

        private State _state = State.Advancing;
        private float _stateTimer;
        private Vector2 _chargeDirection;
        private Health _health;

        private enum State
        {
            Advancing,
            Retreating,
            Charging
        }

        public Vector2 GetDesiredVelocity(Enemy enemy, Transform player, EnemyStats stats, Rigidbody2D body, float deltaTime)
        {
            CacheHealth();
            _stateTimer -= deltaTime;

            float healthRatio = GetHealthRatio();
            if (_state == State.Advancing && healthRatio <= lowHealthThreshold)
            {
                BeginRetreat(enemy.transform, player);
            }

            if (_stateTimer <= 0f)
            {
                AdvanceState(enemy.transform, player, healthRatio);
            }

            float baseSpeed = stats ? stats.MoveSpeed : 3f;
            switch (_state)
            {
                case State.Retreating:
                    return GetRetreatVelocity(enemy.transform, player, baseSpeed);
                case State.Charging:
                    return _chargeDirection * baseSpeed * chargeSpeedMultiplier;
                default:
                    return MoveTowardPlayer(enemy.transform, player, baseSpeed);
            }
        }

        private void AdvanceState(Transform enemyTransform, Transform player, float healthRatio)
        {
            switch (_state)
            {
                case State.Advancing:
                    BeginRetreat(enemyTransform, player);
                    break;
                case State.Retreating:
                    BeginCharge(enemyTransform, player);
                    break;
                case State.Charging:
                    _state = healthRatio <= lowHealthThreshold ? State.Retreating : State.Advancing;
                    _stateTimer = Mathf.Max(0.2f, retreatDuration * 0.5f);
                    break;
            }
        }

        private void BeginRetreat(Transform enemyTransform, Transform player)
        {
            _state = State.Retreating;
            _stateTimer = retreatDuration;
            _chargeDirection = DetermineChargeDirection(enemyTransform, player);
        }

        private void BeginCharge(Transform enemyTransform, Transform player)
        {
            _state = State.Charging;
            _stateTimer = chargeDuration;
            _chargeDirection = DetermineChargeDirection(enemyTransform, player);
        }

        private Vector2 DetermineChargeDirection(Transform enemyTransform, Transform player)
        {
            if (player)
            {
                Vector2 toPlayer = (Vector2)(player.position - enemyTransform.position);
                if (toPlayer.sqrMagnitude > Mathf.Epsilon)
                {
                    return toPlayer.normalized;
                }
            }

            Vector2 forward = enemyTransform.right;
            if (forward.sqrMagnitude < Mathf.Epsilon)
            {
                forward = Vector2.right;
            }

            return forward.normalized;
        }

        private Vector2 MoveTowardPlayer(Transform enemyTransform, Transform player, float speed)
        {
            if (!player)
            {
                return Vector2.zero;
            }

            Vector2 toPlayer = (Vector2)(player.position - enemyTransform.position);
            if (toPlayer.sqrMagnitude < Mathf.Epsilon)
            {
                return Vector2.zero;
            }

            return toPlayer.normalized * speed;
        }

        private Vector2 GetRetreatVelocity(Transform enemyTransform, Transform player, float speed)
        {
            Vector2 awayFromPlayer = player
                ? (Vector2)(enemyTransform.position - player.position)
                : enemyTransform.right;

            if (awayFromPlayer.sqrMagnitude < Mathf.Epsilon)
            {
                awayFromPlayer = Vector2.right;
            }

            return awayFromPlayer.normalized * speed * retreatSpeedMultiplier;
        }

        private void CacheHealth()
        {
            if (_health)
            {
                return;
            }

            _health = GetComponent<Health>();
        }

        private float GetHealthRatio()
        {
            if (!_health)
            {
                return 1f;
            }

            return Mathf.Clamp01(_health.MaxHP > 0 ? (float)_health.CurrentHP / _health.MaxHP : 1f);
        }

        private void Reset()
        {
            _state = State.Advancing;
            _stateTimer = 0f;
            _chargeDirection = Vector2.zero;
            CacheHealth();
        }
    }
}
