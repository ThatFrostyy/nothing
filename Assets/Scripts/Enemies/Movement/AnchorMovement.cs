using UnityEngine;

namespace FF
{
    [AddComponentMenu("FF/Enemies/Movement/Anchor Movement")]
    public class AnchorMovement : MonoBehaviour, IEnemyMovement
    {
        [Header("Timings")]
        [SerializeField, Min(0.1f)] private float anchorDuration = 3f;
        [SerializeField, Min(0.05f)] private float relocateDelay = 0.6f;

        [Header("Positioning")]
        [SerializeField, Min(0.1f)] private float anchorRadius = 4.5f;

        private State _state = State.MovingToAnchor;
        private float _stateTimer;
        private Vector2 _anchorPosition;

        private enum State
        {
            MovingToAnchor,
            Anchored,
            WaitingToRelocate
        }

        public Vector2 GetDesiredVelocity(Enemy enemy, Transform player, EnemyStats stats, Rigidbody2D body, float deltaTime)
        {
            _stateTimer -= deltaTime;

            switch (_state)
            {
                case State.MovingToAnchor:
                    return HandleMoving(enemy.transform, player, stats);
                case State.Anchored:
                    if (_stateTimer <= 0f)
                    {
                        _state = State.WaitingToRelocate;
                        _stateTimer = relocateDelay;
                    }
                    return Vector2.zero;
                case State.WaitingToRelocate:
                    if (_stateTimer <= 0f)
                    {
                        ChooseNewAnchor(player, enemy.transform);
                        _state = State.MovingToAnchor;
                    }
                    return Vector2.zero;
                default:
                    return Vector2.zero;
            }
        }

        private Vector2 HandleMoving(Transform enemyTransform, Transform player, EnemyStats stats)
        {
            float baseSpeed = stats ? stats.MoveSpeed : 3f;
            Vector2 toAnchor = _anchorPosition - (Vector2)enemyTransform.position;
            if (toAnchor.sqrMagnitude <= 0.04f)
            {
                _state = State.Anchored;
                _stateTimer = anchorDuration;
                return Vector2.zero;
            }

            return toAnchor.normalized * baseSpeed;
        }

        private void ChooseNewAnchor(Transform player, Transform enemyTransform)
        {
            Vector2 origin = player ? (Vector2)player.position : (Vector2)enemyTransform.position;
            Vector2 offset = Random.insideUnitCircle.normalized * anchorRadius;
            _anchorPosition = origin + offset;
        }

        private void Reset()
        {
            _state = State.MovingToAnchor;
            _stateTimer = 0f;
            _anchorPosition = transform.position;
        }
    }
}
