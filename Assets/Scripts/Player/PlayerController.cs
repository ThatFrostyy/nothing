using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

namespace FF
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        public static event Action<PlayerController> OnPlayerReady;

        [Header("References")]
        [SerializeField] private Camera _camera;
        [SerializeField] private AutoShooter _autoShooter;
        [SerializeField] private Transform _gunPivot;
        [SerializeField] private Transform _playerVisual;
        [SerializeField] private PlayerCosmetics _cosmetics;
        [SerializeField] private UpgradeManager _upgradeManager;
        [SerializeField] private WeaponManager _weaponManager;
        [SerializeField] private Weapon _startingWeapon;

        [Header("Aiming Line")]
        [SerializeField] private bool _showAimingLine = true;
        [SerializeField, Min(0.1f)] private float _aimLineLength = 2.5f;
        [SerializeField, Min(0.01f)] private float _aimLineDashLength = 0.2f;
        [SerializeField, Min(0.01f)] private float _aimLineGapLength = 0.12f;
        [SerializeField, Min(0.005f)] private float _aimLineWidth = 0.05f;
        [SerializeField] private float _aimLineOffsetX = 0f;
        [SerializeField] private float _aimLineOffsetY = 0f;

        [Header("Movement Settings")]
        [SerializeField] private float _acceleration = 0.18f;
        [SerializeField] private float _bodyTiltDegrees = 15f;
        [SerializeField] private float _tiltSmoothTime = 0.07f;
        [SerializeField] private float _idleSwayFrequency = 6f;
        [SerializeField] private float _idleSwayAmplitude = 1.2f;
        [SerializeField] private bool _enableTilt = true;

        [Header("Bounds Settings")]
        [SerializeField] private float _boundsPadding = 0.05f;

        private Rigidbody2D _rigidbody;
        private PlayerStats _stats;
        private Vector2 _moveInput;
        private Collider2D _collider;
        private bool _upgradeMenuOpen;
        private float _tiltVelocity;
        private LineRenderer _aimLine;
        private Vector2 _lastAimDirection = Vector2.right;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _stats = GetComponent<PlayerStats>();
            _camera = _camera ? _camera : Camera.main;
            _collider = GetComponent<Collider2D>();
            _upgradeManager = _upgradeManager ? _upgradeManager : GetComponent<UpgradeManager>();
            if (!_upgradeManager)
            {
                _upgradeManager = UpgradeManager.I;
            }
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

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Confined;

            InitializeAimLine();

            if (_upgradeManager && _weaponManager)
            {
                _upgradeManager.RegisterWeaponManager(_weaponManager);
            }
        }

        private void Start()
        {
            ApplyCharacterSelection();

            if (_startingWeapon && _weaponManager)
            {
                _weaponManager.Equip(_startingWeapon);
            }

            OnPlayerReady?.Invoke(this);
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
            float timeScale = Time.timeScale;
            if (timeScale <= Mathf.Epsilon)
            {
                _rigidbody.linearVelocity = Vector2.zero;
                return;
            }

            float targetSpeed = _stats.GetMoveSpeed();
            if (timeScale < 0.999f)
            {
                targetSpeed /= Mathf.Max(0.01f, timeScale);
            }

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

            if (_upgradeMenuOpen || PauseMenuController.IsMenuOpen)
            {
                return;
            }

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

            if (direction.sqrMagnitude > 0.0001f)
            {
                _lastAimDirection = direction.normalized;
            }

            UpdateAimLine();
        }

        private void InitializeAimLine()
        {
            if (!_showAimingLine || !_gunPivot)
            {
                return;
            }

            GameObject aimLineObject = new GameObject("AimLine");
            aimLineObject.transform.SetParent(_gunPivot, false);
            _aimLine = aimLineObject.AddComponent<LineRenderer>();
            _aimLine.useWorldSpace = true;
            _aimLine.textureMode = LineTextureMode.Tile;
            _aimLine.alignment = LineAlignment.TransformZ;
            _aimLine.numCapVertices = 0;
            _aimLine.numCornerVertices = 0;
            _aimLine.widthMultiplier = _aimLineWidth;
            _aimLine.sortingOrder = 20;
            _aimLine.enabled = false;

            Material material = new Material(Shader.Find("Sprites/Default"));
            material.color = new Color(1f, 1f, 1f, 0.85f);
            material.mainTexture = CreateDottedTexture();
            material.hideFlags = HideFlags.DontSave;
            if (material.mainTexture)
            {
                material.mainTexture.wrapMode = TextureWrapMode.Repeat;
                material.mainTexture.filterMode = FilterMode.Point;
            }

            _aimLine.material = material;
        }

        private Texture2D CreateDottedTexture()
        {
            Texture2D texture = new Texture2D(2, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.SetPixel(1, 0, new Color(1f, 1f, 1f, 0f));
            texture.Apply();
            texture.name = "AimLine_Dots";
            texture.hideFlags = HideFlags.DontSave;
            return texture;
        }

        private void UpdateAimLine()
        {
            if (_aimLine == null)
            {
                return;
            }

            bool hasWeaponEquipped = _weaponManager && _weaponManager.CurrentWeapon;
            _aimLine.enabled = hasWeaponEquipped;
            if (!hasWeaponEquipped)
            {
                return;
            }

            Transform origin = _weaponManager.CurrentMuzzle ? _weaponManager.CurrentMuzzle : _gunPivot;
            Vector3 offset = new Vector3(_aimLineOffsetX, _aimLineOffsetY, 0f);
            Vector3 start = (origin ? origin.position : _gunPivot.position) + _gunPivot.TransformVector(offset);
            Vector3 end = start + (Vector3)(_lastAimDirection.normalized * _aimLineLength);

            _aimLine.positionCount = 2;
            _aimLine.SetPosition(0, start);
            _aimLine.SetPosition(1, end);

            if (_aimLine.material && _aimLine.material.mainTexture)
            {
                float patternLength = Mathf.Max(0.01f, _aimLineDashLength + _aimLineGapLength);
                float repeatCount = _aimLineLength / patternLength;
                _aimLine.material.mainTextureScale = new Vector2(repeatCount, 1f);
            }
        }

        #region Animations
        private void UpdateBodyTilt()
        {
            if (!_playerVisual) return;

            // NEW: allow tilt to be completely disabled
            if (!_enableTilt)
            {
                _playerVisual.localRotation = Quaternion.Euler(0f, 0f, 0f);
                return;
            }

            Vector2 velocity = _rigidbody.linearVelocity;
            float speed = velocity.magnitude;
            float maxSpeed = Mathf.Max(_stats.GetMoveSpeed(), Mathf.Epsilon);
            float normalizedSpeed = speed / maxSpeed;

            float targetTilt = speed > 0.1f ? -_bodyTiltDegrees * normalizedSpeed : _bodyTiltDegrees * 0.3f;
            if (velocity.sqrMagnitude > 0.01f)
            {
                float moveX = Mathf.Clamp(velocity.normalized.x, -1f, 1f);

                float sideTilt = moveX * (_bodyTiltDegrees * 0.5f);
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

            if (_upgradeMenuOpen || PauseMenuController.IsMenuOpen)
            {
                _autoShooter.SetFireHeld(false);
                return;
            }

            _autoShooter.OnFire(value);
        }

        public void OnUpgrade(InputValue value)
        {
            if (!value.isPressed)
            {
                return;
            }

            if (_upgradeManager == null)
            {
                _upgradeManager = UpgradeManager.I;
            }

            _upgradeManager?.TryOpenUpgradeMenu();
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

        public void OnSelectSpecial(InputValue value)
        {
            if (!value.isPressed || _weaponManager == null)
            {
                return;
            }

            _weaponManager.SelectSpecialSlot();
        }

        public void OnPause(InputValue value)
        {
            if (!value.isPressed)
            {
                return;
            }

            PauseMenuController.TogglePause();
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
