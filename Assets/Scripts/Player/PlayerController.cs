using UnityEngine;

namespace FF
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(InputRouter))]
    [RequireComponent(typeof(PlayerState))]
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Rigidbody2D _rigidbody;
        [SerializeField] private PlayerStats _stats;
        [SerializeField] private Collider2D _collider;
        [SerializeField] private InputRouter _inputRouter;
        [SerializeField] private PlayerState _playerState;

        [Header("Movement Settings")]
        [SerializeField] private float _acceleration = 0.18f;

        [Header("Bounds Settings")]
        [SerializeField] private float _boundsPadding = 0.05f;

        private Vector2 _moveInput;

        private void Awake()
        {
            if (!_rigidbody) _rigidbody = GetComponent<Rigidbody2D>();
            if (!_stats) _stats = GetComponent<PlayerStats>();
            if (!_collider) _collider = GetComponent<Collider2D>();
            if (!_inputRouter) _inputRouter = GetComponent<InputRouter>();
            if (!_playerState) _playerState = GetComponent<PlayerState>();

            if (!ValidateDependencies())
            {
                Debug.LogError($"{nameof(PlayerController)} on {name} disabled due to missing dependencies.", this);
                enabled = false;
                return;
            }
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            // Editor only convenience: fill references when editing.
            if (!_rigidbody) _rigidbody = GetComponent<Rigidbody2D>();
            if (!_stats) _stats = GetComponent<PlayerStats>();
            if (!_collider) _collider = GetComponent<Collider2D>();
            if (!_inputRouter) _inputRouter = GetComponent<InputRouter>();
            if (!_playerState) _playerState = GetComponent<PlayerState>();
#endif
        }

        private bool ValidateDependencies()
        {
            bool ok = true;

            if (!_rigidbody)
            {
                Debug.LogError("Missing Rigidbody2D reference.", this);
                ok = false;
            }

            if (!_stats)
            {
                Debug.LogError("Missing PlayerStats reference.", this);
                ok = false;
            }

            if (!_collider)
            {
                Debug.LogError("Missing Collider2D reference.", this);
                ok = false;
            }

            if (!_inputRouter)
            {
                Debug.LogError("Missing InputRouter reference.", this);
                ok = false;
            }

            if (!_playerState)
            {
                Debug.LogError("Missing PlayerState reference.", this);
                ok = false;
            }

            return ok;
        }

        private void Update()
        {
            _moveInput = CanMove ? _inputRouter.MoveInput : Vector2.zero;
        }

        private void FixedUpdate()
        {
            float targetSpeed = _stats.GetMoveSpeed();
            Vector2 targetVelocity = _moveInput.normalized * targetSpeed;

            _rigidbody.linearVelocity = Vector2.Lerp(
                _rigidbody.linearVelocity,
                targetVelocity,
                _acceleration
            );

            ConstrainToGroundBounds();
        }

        private void ConstrainToGroundBounds()
        {
            if (!Ground.Instance)
            {
                return;
            }

            Vector2 padding = Vector2.one * _boundsPadding;
            if (_collider)
            {
                Vector2 extents = _collider.bounds.extents;
                padding = extents + padding;
            }

            Vector2 currentPosition = _rigidbody.position;
            Vector2 clampedPosition = Ground.Instance.ClampPoint(currentPosition, padding);

            if (currentPosition != clampedPosition)
            {
                Vector2 velocity = _rigidbody.linearVelocity;

                if (!Mathf.Approximately(currentPosition.x, clampedPosition.x))
                {
                    velocity.x = 0f;
                }

                if (!Mathf.Approximately(currentPosition.y, clampedPosition.y))
                {
                    velocity.y = 0f;
                }

                _rigidbody.linearVelocity = velocity;
                _rigidbody.position = clampedPosition;
            }
        }

        private bool CanMove => _playerState == null || _playerState.CanMove;
    }
}
