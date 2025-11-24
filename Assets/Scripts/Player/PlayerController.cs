using UnityEngine;
using UnityEngine.InputSystem;

namespace FF
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(InputRouter))]
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera _camera;
        [SerializeField] private Transform _gunPivot;
        [SerializeField] private Transform _playerVisual;
        [SerializeField] private PlayerCosmetics _cosmetics;
        [SerializeField] private WeaponManager _weaponManager;
        [SerializeField] private Weapon _startingWeapon;
        [SerializeField] private InputRouter _inputRouter;

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
        private Collider2D _collider;
        private float _tiltVelocity;
        private Vector2 _moveInput;

        private void Awake()
        {
            if (!ValidateDependencies())
            {
                Debug.LogError($"{nameof(PlayerController)} on {name} disabled due to missing dependencies.", this);
                enabled = false;
                return;
            }

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Confined;
        }

        private void OnValidate()
        {
            if (!_rigidbody) _rigidbody = GetComponent<Rigidbody2D>();
            if (!_stats) _stats = GetComponent<PlayerStats>();
            if (!_collider) _collider = GetComponent<Collider2D>();
            if (!_weaponManager) _weaponManager = GetComponentInChildren<WeaponManager>();
            if (!_inputRouter) _inputRouter = GetComponent<InputRouter>();
            if (!_cosmetics) _cosmetics = GetComponent<PlayerCosmetics>();
            if (!_cosmetics && _playerVisual)
            {
                _cosmetics = _playerVisual.GetComponent<PlayerCosmetics>();
            }
            if (!_camera)
            {
                Transform cameraTransform = transform.root.Find("GameplayRoot/Camera") ?? transform.Find("Camera") ?? transform.GetComponentInChildren<Camera>()?.transform;
                if (cameraTransform && cameraTransform.TryGetComponent(out Camera foundCamera))
                {
                    _camera = foundCamera;
                }
            }
            if (!_playerVisual)
            {
                Transform foundVisual = transform.Find("Visual");
                if (foundVisual)
                {
                    _playerVisual = foundVisual;
                }
            }
            if (!_gunPivot)
            {
                Transform foundGunPivot = transform.Find("GunPivot");
                if (foundGunPivot)
                {
                    _gunPivot = foundGunPivot;
                }
            }
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

            if (!_camera)
            {
                Debug.LogError("Missing Camera reference.", this);
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

            if (!_weaponManager)
            {
                Debug.LogError("Missing WeaponManager reference.", this);
                ok = false;
            }

            if (_playerVisual && !_cosmetics)
            {
                SpriteRenderer renderer = _playerVisual.GetComponent<SpriteRenderer>();
                if (renderer)
                {
                    _cosmetics = gameObject.AddComponent<PlayerCosmetics>();
                    _cosmetics.SetRenderTargets(renderer, _playerVisual);
                }
            }

            if (!_gunPivot)
            {
                Debug.LogError("Missing gun pivot reference.", this);
                ok = false;
            }

            return ok;
        }

        private void Start()
        {
            ApplyCharacterSelection();

            if (_startingWeapon && _weaponManager)
            {
                _weaponManager.Equip(_startingWeapon);
            }
        }

        private void Update()
        {
            _moveInput = _inputRouter != null ? _inputRouter.MoveInput : Vector2.zero;
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

        public void OverrideStartingWeapon(Weapon weapon)
        {
            _startingWeapon = weapon;
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
