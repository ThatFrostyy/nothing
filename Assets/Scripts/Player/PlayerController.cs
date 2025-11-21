using UnityEngine;
using UnityEngine.InputSystem;

namespace FF
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera _camera;
        [SerializeField] private AutoShooter _autoShooter;
        [SerializeField] private Transform _gunPivot;
        [SerializeField] private Transform _playerVisual;
        [SerializeField] private UpgradeManager _upgradeManager;
        [SerializeField] private WeaponManager _weaponManager;
        [SerializeField] private Weapon _startingWeapon;

        [Header("Movement Settings")]
        [SerializeField] private float _acceleration = 0.18f;
        [SerializeField] private float _bodyTiltDegrees = 15f;
        [SerializeField] private float _tiltSmoothTime = 0.07f;
        [SerializeField] private float _idleSwayFrequency = 6f;
        [SerializeField] private float _idleSwayAmplitude = 1.2f;

        [Header("Bounds Settings")]
        [SerializeField] private float _boundsPadding = 0.05f;

        private Rigidbody2D _rigidbody;
        private PlayerStats _stats;
        private Vector2 _moveInput;
        private Collider2D _collider;
        private bool _upgradeMenuOpen;
        private float _tiltVelocity;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _stats = GetComponent<PlayerStats>();
            _camera = _camera ? _camera : Camera.main;
            _collider = GetComponent<Collider2D>();
            _upgradeManager = _upgradeManager ? _upgradeManager : GetComponent<UpgradeManager>();
            _weaponManager = _weaponManager ? _weaponManager : GetComponentInChildren<WeaponManager>();

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Confined;
        }

        private void Start()
        {
            if (_startingWeapon && _weaponManager)
            {
                _weaponManager.Equip(_startingWeapon);
            }
        }

        private void OnEnable()
        {
            UpgradeUI.OnVisibilityChanged += HandleUpgradeVisibilityChanged;
        }

        private void OnDisable()
        {
            UpgradeUI.OnVisibilityChanged -= HandleUpgradeVisibilityChanged;
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

            Vector2 velocity = _rigidbody.linearVelocity;
            float speed = velocity.magnitude;
            float maxSpeed = Mathf.Max(_stats.GetMoveSpeed(), Mathf.Epsilon);
            float normalizedSpeed = speed / maxSpeed;

            float targetTilt = speed > 0.1f ? -_bodyTiltDegrees * normalizedSpeed : _bodyTiltDegrees * 0.3f;
            if (velocity.sqrMagnitude > 0.01f)
            {
                float moveX = velocity.normalized.x;

                float facing = _playerVisual.localScale.x < 0f ? -1f : 1f;

                float sideTilt = moveX * facing * (_bodyTiltDegrees * 0.5f);
                targetTilt += sideTilt;
            }

            float idleBlend = 1f - Mathf.Clamp01(normalizedSpeed * 3f);
            if (idleBlend > 0f)
            {
                targetTilt += Mathf.Sin(Time.time * _idleSwayFrequency) * _idleSwayAmplitude * idleBlend;
            }

            float newZ = Mathf.SmoothDampAngle(
                _playerVisual.localEulerAngles.z,
                targetTilt,
                ref _tiltVelocity,
                _tiltSmoothTime
            );
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
            if (_autoShooter == null)
            {
                return;
            }

            if (_upgradeMenuOpen)
            {
                _autoShooter.SetFireHeld(false);
                return;
            }

            _autoShooter.OnFire(value);
        }

        public void OnUpgrade(InputValue value)
        {
            Debug.Log("press");
            if (!value.isPressed || _upgradeManager == null)
            {
                return;
            }

            _upgradeManager.TryOpenUpgradeMenu();
        }

        public void OnPrevious(InputValue value)
        {
            if (!value.isPressed || _weaponManager == null)
            {
                return;
            }

            _weaponManager.SelectPreviousSlot();
        }

        public void OnNext(InputValue value)
        {
            if (!value.isPressed || _weaponManager == null)
            {
                return;
            }

            _weaponManager.SelectNextSlot();
        }
        #endregion Input System Callbacks

        private void HandleUpgradeVisibilityChanged(bool isVisible)
        {
            _upgradeMenuOpen = isVisible;
            if (isVisible && _autoShooter != null)
            {
                _autoShooter.SetFireHeld(false);
            }
        }
    }
}
