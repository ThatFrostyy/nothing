using UnityEngine;

namespace FF
{
    [AddComponentMenu("FF/Enemies/Movement/Charger Movement")]
    public class ChargerMovement : MonoBehaviour, IEnemyMovement
    {
        [SerializeField, Min(0.1f)] private float windupDuration = 1.5f;
        [SerializeField, Min(0.1f)] private float chargeDuration = 0.8f;
        [SerializeField, Min(0.1f)] private float chargeSpeedMultiplier = 2.5f;
        [SerializeField, Min(0.1f)] private float cooldownDuration = 1f;

        private float _stateTimer;
        private State _state = State.Windup;
        private Vector2 _chargeDirection;

        enum State
        {
            Windup,
            Charging,
            Cooldown
        }

        public Vector2 GetDesiredVelocity(Enemy enemy, Transform player, EnemyStats stats, Rigidbody2D body, float deltaTime)
        {
            _stateTimer -= deltaTime;
            if (_stateTimer <= 0f)
            {
                AdvanceState(player, enemy.transform);
            }

            float baseSpeed = stats ? stats.MoveSpeed : 3f;
            switch (_state)
            {
                case State.Charging:
                    return _chargeDirection * baseSpeed * chargeSpeedMultiplier;
                case State.Cooldown:
                    return Vector2.zero;
                default:
                    return Vector2.zero;
            }
        }

        private void AdvanceState(Transform player, Transform enemyTransform)
        {
            switch (_state)
            {
                case State.Windup:
                    BeginCharge(player, enemyTransform);
                    break;
                case State.Charging:
                    _state = State.Cooldown;
                    _stateTimer = cooldownDuration;
                    break;
                case State.Cooldown:
                    _state = State.Windup;
                    _stateTimer = windupDuration;
                    break;
            }
        }

        private void BeginCharge(Transform player, Transform enemyTransform)
        {
            _state = State.Charging;
            _stateTimer = chargeDuration;
            if (player)
            {
                Vector2 toPlayer = (Vector2)(player.position - enemyTransform.position);
                if (toPlayer.sqrMagnitude > Mathf.Epsilon)
                {
                    _chargeDirection = toPlayer.normalized;
                    return;
                }
            }

            Vector2 forward = enemyTransform.right;
            if (forward.sqrMagnitude < Mathf.Epsilon)
            {
                forward = Vector2.right;
            }

            _chargeDirection = forward.normalized;
        }

        void Reset()
        {
            _state = State.Windup;
            _stateTimer = windupDuration;
        }
    }
}
