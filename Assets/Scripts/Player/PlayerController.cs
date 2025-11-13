using UnityEngine;
using UnityEngine.InputSystem;

namespace FF
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private AutoShooter _autoShooter;
        [SerializeField] private Transform _gunPivot;
        [SerializeField] private Transform _playerVisual;

        [SerializeField] private float _acceleration = 0.18f;
        [SerializeField] private float _bodyTiltDegrees = 15f;

        private Rigidbody2D _rigidbody;
        private PlayerStats _stats;
        private Vector2 _moveInput;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _stats = GetComponent<PlayerStats>();
            _camera = _camera ? _camera : Camera.main;
        }

        private void Update()
        {
            AimGunAtPointer();
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

            UpdateBodyTilt();
        }

        private void AimGunAtPointer()
        {
            if (!_gunPivot || _camera == null) return;

            Vector2 mousePosition = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            Vector3 worldMouse = _camera.ScreenToWorldPoint(mousePosition);
            Vector2 direction = worldMouse - _gunPivot.position;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            _gunPivot.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

            bool isAimingLeft = direction.x < 0f;
            _gunPivot.localScale = isAimingLeft ? new Vector3(1f, -1f, 1f) : Vector3.one;
            if (_playerVisual)
            {
                _playerVisual.localScale = isAimingLeft ? new Vector3(-1f, 1f, 1f) : Vector3.one;
            }
        }

        #region Animations
        private void UpdateBodyTilt()
        {
            if (!_playerVisual) return;

            float speed = _rigidbody.linearVelocity.magnitude;
            float maxSpeed = Mathf.Max(_stats.GetMoveSpeed(), Mathf.Epsilon);
            float normalizedSpeed = speed / maxSpeed;

            float targetTilt = speed > 0.1f ? -_bodyTiltDegrees * normalizedSpeed : _bodyTiltDegrees * 0.3f;
            float sideTilt = _moveInput.x * (_bodyTiltDegrees * 0.5f);
            targetTilt += sideTilt;

            float currentZ = _playerVisual.localEulerAngles.z;
            if (currentZ > 180f)
            {
                currentZ -= 360f;
            }

            float newZ = Mathf.Lerp(currentZ, targetTilt, 0.15f);
            _playerVisual.localRotation = Quaternion.Euler(0f, 0f, newZ);
        }
        #endregion Animations

        #region Input System Callbacks
        public void OnMove(InputValue value)
        {
            _moveInput = value.Get<Vector2>();
        }

        public void OnAttack(InputValue value)
        {
            _autoShooter.OnFire(value);
        }
        #endregion Input System Callbacks
    }
}
