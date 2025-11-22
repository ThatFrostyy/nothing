using UnityEngine;
using UnityEngine.InputSystem;

namespace FF
{
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera _camera;
        [SerializeField] private AutoShooter _autoShooter;
        [SerializeField] private Transform _gunPivot;
        [SerializeField] private Transform _playerVisual;
        [SerializeField] private PlayerCosmetics _cosmetics;
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

        private PlayerStats _stats;
        private Vector2 _moveInput;
        private bool _upgradeMenuOpen;
        private float _tiltVelocity;
        private Vector2 _currentVelocity;
        private Vector2 _visualExtents;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _camera = _camera ? _camera : Camera.main;
            _upgradeManager = _upgradeManager ? _upgradeManager : GetComponent<UpgradeManager>();
            _weaponManager = _weaponManager ? _weaponManager : GetComponentInChildren<WeaponManager>();
            _cosmetics = _cosmetics ? _cosmetics : GetComponent<PlayerCosmetics>();

            if (!_cosmetics && _playerVisual)
            {
                _cosmetics = _playerVisual.GetComponent<PlayerCosmetics>();
            }

            if (!_cosmetics && _playerVisual)
            {
                SpriteRenderer renderer = _playerVisual.GetComponent<SpriteRenderer>();
                if (renderer)
                {
                    _cosmetics = gameObject.AddComponent<PlayerCosmetics>();
                    _cosmetics.SetRenderTargets(renderer, _playerVisual);
                }
            }

            CacheVisualExtents();

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Confined;
        }

        private void Start()
        {
            ApplyCharacterSelection();

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
            HandleMovement(Time.deltaTime);
            AimGunAtPointer();
        }

        private void HandleMovement(float deltaTime)
        {
            float targetSpeed = _stats ? _stats.GetMoveSpeed() : 0f;
            Vector2 targetVelocity = _moveInput.normalized * targetSpeed;

            _currentVelocity = Vector2.Lerp(
                _currentVelocity,
                targetVelocity,
                _acceleration
            );

            transform.position += (Vector3)(_currentVelocity * deltaTime);
            UpdateBodyTilt();
            ConstrainToGroundBounds();
        }

        private void ConstrainToGroundBounds()
        {
            if (!Ground.Instance)
            {
                return;
            }

            Vector2 padding = _visualExtents + Vector2.one * _boundsPadding;
            Vector2 currentPosition = transform.position;
            Vector2 clampedPosition = Ground.Instance.ClampPoint(currentPosition, padding);

            if (currentPosition != clampedPosition)
            {
                if (!Mathf.Approximately(currentPosition.x, clampedPosition.x))
                {
                    _currentVelocity.x = 0f;
                }

                if (!Mathf.Approximately(currentPosition.y, clampedPosition.y))
                {
                    _currentVelocity.y = 0f;
                }

                transform.position = clampedPosition;
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

            Vector2 velocity = _currentVelocity;
            float speed = velocity.magnitude;
            float maxSpeed = Mathf.Max(_stats != null ? _stats.GetMoveSpeed() : speed, Mathf.Epsilon);
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

        private void CacheVisualExtents()
        {
            _visualExtents = Vector2.zero;

            if (_playerVisual && _playerVisual.TryGetComponent<SpriteRenderer>(out var renderer))
            {
                _visualExtents = renderer.bounds.extents;
                return;
            }

            if (TryGetComponent<SpriteRenderer>(out var selfRenderer))
            {
                _visualExtents = selfRenderer.bounds.extents;
            }
        }

        public Vector2 CurrentVelocity => _currentVelocity;

        public void OverrideStartingWeapon(Weapon weapon)
        {
            _startingWeapon = weapon;
        }

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

        private void ApplyCharacterSelection()
        {
            if (!CharacterSelectionState.HasSelection)
            {
                return;
            }

            CharacterDefinition character = CharacterSelectionState.SelectedCharacter;
            HatDefinition hat = CharacterSelectionState.SelectedHat ?? character?.GetDefaultHat();
            Weapon weapon = CharacterSelectionState.SelectedWeapon ?? character?.StartingWeapon;

            if (_cosmetics && character != null)
            {
                _cosmetics.Apply(hat, character.PlayerSprite);
            }
            else if (_cosmetics)
            {
                _cosmetics.Apply(hat, null);
            }

            if (weapon)
            {
                OverrideStartingWeapon(weapon);
            }
        }
    }
}
