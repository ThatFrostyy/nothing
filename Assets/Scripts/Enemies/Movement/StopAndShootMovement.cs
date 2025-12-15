using UnityEngine;

namespace FF
{
    [AddComponentMenu("FF/Enemies/Movement/Stop And Shoot Movement")]
    public class StopAndShootMovement : MonoBehaviour, IEnemyMovement
    {
        [Header("Timings")]
        [Tooltip("How long the enemy walks towards the player.")]
        [SerializeField, Min(0.1f)] private float moveDuration = 2.0f;

        [Tooltip("How long the enemy stands still to shoot.")]
        [SerializeField, Min(0.1f)] private float stopDuration = 1.0f;

        [Header("Settings")]
        [Tooltip("If true, the enemy will stop moving if they get too close, even if the Move Timer is active.")]
        [SerializeField] private bool respectMinDistance = true;
        [SerializeField, Min(0f)] private float minDistance = 1.5f;

        private float _timer;
        private bool _isMoving;

        private void Start()
        {
            // Start by moving
            _isMoving = true;
            _timer = moveDuration;
        }

        public Vector2 GetDesiredVelocity(Enemy enemy, Transform player, EnemyStats stats, Rigidbody2D body, float deltaTime)
        {
            if (!player) return Vector2.zero;

            // 1. Handle the Timer Cycle
            _timer -= deltaTime;
            if (_timer <= 0f)
            {
                // Toggle state
                _isMoving = !_isMoving;

                // Reset timer based on new state
                _timer = _isMoving ? moveDuration : stopDuration;
            }

            // 2. If we are in the "Stop/Shoot" phase, return zero velocity
            if (!_isMoving)
            {
                return Vector2.zero;
            }

            // 3. We are in "Move" phase - Calculate direction to player
            Vector2 toPlayer = player.position - enemy.transform.position;
            float distance = toPlayer.magnitude;

            // Optional: If we are moving, but we are ALREADY too close, stop anyway
            if (respectMinDistance && distance <= minDistance)
            {
                return Vector2.zero;
            }

            // 4. Return movement velocity
            float moveSpeed = stats ? stats.MoveSpeed : 3f;
            return toPlayer.normalized * moveSpeed;
        }

        private void Reset()
        {
            _isMoving = true;
            _timer = moveDuration;
        }
    }
}