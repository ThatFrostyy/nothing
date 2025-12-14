using UnityEngine;

namespace FF
{
    [AddComponentMenu("FF/Enemies/Movement/Stop And Shoot Movement")]
    public class StopAndShootMovement : MonoBehaviour, IEnemyMovement
    {
        [Header("Timings")]
        [SerializeField, Min(0.1f)] private float advanceDuration = 1.5f;
        [SerializeField, Min(0.1f)] private float pauseDuration = 1f;
        [SerializeField, Min(0.1f)] private float repositionDuration = 1.25f;

        [Header("Positioning")]
        [SerializeField, Min(0f)] private float stopDistance = 1.75f;
        [SerializeField, Range(0f, 1f)] private float lateralBias = 0.65f;

        private State _state = State.Advancing;
        private float _stateTimer;
        private Vector2 _repositionDirection;

        private enum State
        {
            Advancing,
            Paused,
            Repositioning
        }

        public Vector2 GetDesiredVelocity(Enemy enemy, Transform player, EnemyStats stats, Rigidbody2D body, float deltaTime)
        {
            _stateTimer -= deltaTime;

            if (_state == State.Advancing && player)
            {
                float distance = Vector2.Distance(enemy.transform.position, player.position);
                if (distance <= stopDistance)
                {
                    BeginPause();
                }
            }

            if (_stateTimer <= 0f)
            {
                AdvanceState(player, enemy.transform);
            }

            float baseSpeed = stats ? stats.MoveSpeed : 3f;
            switch (_state)
            {
                case State.Advancing:
                    return GetAdvanceVelocity(enemy.transform, player, baseSpeed);
                case State.Repositioning:
                    return baseSpeed * _repositionDirection;
                default:
                    return Vector2.zero;
            }
        }

        private void AdvanceState(Transform player, Transform enemyTransform)
        {
            switch (_state)
            {
                case State.Advancing:
                    BeginPause();
                    break;
                case State.Paused:
                    BeginReposition(player, enemyTransform);
                    break;
                case State.Repositioning:
                    BeginAdvance();
                    break;
            }
        }

        private void BeginAdvance()
        {
            _state = State.Advancing;
            _stateTimer = advanceDuration;
        }

        private void BeginPause()
        {
            _state = State.Paused;
            _stateTimer = pauseDuration;
        }

        private void BeginReposition(Transform player, Transform enemyTransform)
        {
            _state = State.Repositioning;
            _stateTimer = repositionDuration;

            Vector2 forward = enemyTransform.right;
            Vector2 awayFromPlayer = Vector2.right;

            if (player)
            {
                awayFromPlayer = (Vector2)(enemyTransform.position - player.position);
                if (awayFromPlayer.sqrMagnitude > Mathf.Epsilon)
                {
                    awayFromPlayer = awayFromPlayer.normalized;
                }
            }

            if (forward.sqrMagnitude < Mathf.Epsilon)
            {
                forward = Vector2.right;
            }

            Vector2 lateral = Vector2.Perpendicular(awayFromPlayer);
            _repositionDirection = (awayFromPlayer * (1f - lateralBias) + lateral * lateralBias).normalized;
        }

        private Vector2 GetAdvanceVelocity(Transform enemyTransform, Transform player, float speed)
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

        private void Reset()
        {
            _state = State.Advancing;
            _stateTimer = advanceDuration;
        }
    }
}
