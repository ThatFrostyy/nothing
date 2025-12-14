using UnityEngine;

namespace FF
{
    [AddComponentMenu("FF/Enemies/Movement/Leap Vault Movement")]
    public class LeapVaultMovement : MonoBehaviour, IEnemyMovement
    {
        [Header("Timings")]
        [SerializeField, Min(0.05f)] private float pauseBeforeLeap = 0.65f;
        [SerializeField, Min(0.05f)] private float leapDuration = 0.35f;
        [SerializeField, Min(0.05f)] private float cooldownAfterLanding = 1.15f;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float leapSpeedMultiplier = 4.5f;

        private State _state = State.Pausing;
        private float _stateTimer;
        private Vector2 _leapDirection;

        private enum State
        {
            Pausing,
            Leaping,
            Cooldown
        }

        public Vector2 GetDesiredVelocity(Enemy enemy, Transform player, EnemyStats stats, Rigidbody2D body, float deltaTime)
        {
            _stateTimer -= deltaTime;
            if (_stateTimer <= 0f)
            {
                AdvanceState(enemy.transform, player);
            }

            float baseSpeed = stats ? stats.MoveSpeed : 3f;
            return _state switch
            {
                State.Leaping => _leapDirection * baseSpeed * leapSpeedMultiplier,
                State.Cooldown => Vector2.zero,
                _ => Vector2.zero
            };
        }

        private void AdvanceState(Transform enemyTransform, Transform player)
        {
            switch (_state)
            {
                case State.Pausing:
                    BeginLeap(enemyTransform, player);
                    break;
                case State.Leaping:
                    _state = State.Cooldown;
                    _stateTimer = cooldownAfterLanding;
                    break;
                case State.Cooldown:
                    _state = State.Pausing;
                    _stateTimer = pauseBeforeLeap;
                    break;
            }
        }

        private void BeginLeap(Transform enemyTransform, Transform player)
        {
            _state = State.Leaping;
            _stateTimer = leapDuration;
            _leapDirection = DetermineLeapDirection(enemyTransform, player);
        }

        private Vector2 DetermineLeapDirection(Transform enemyTransform, Transform player)
        {
            if (player)
            {
                Vector2 toPlayer = (Vector2)(player.position - enemyTransform.position);
                if (toPlayer.sqrMagnitude > Mathf.Epsilon)
                {
                    return toPlayer.normalized;
                }
            }

            Vector2 fallback = enemyTransform.right;
            if (fallback.sqrMagnitude < Mathf.Epsilon)
            {
                fallback = Vector2.right;
            }

            return fallback.normalized;
        }

        private void Reset()
        {
            _state = State.Pausing;
            _stateTimer = pauseBeforeLeap;
        }
    }
}
