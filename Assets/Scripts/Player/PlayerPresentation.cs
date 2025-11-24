using UnityEngine;

namespace FF
{
    public class PlayerPresentation : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera _camera;
        [SerializeField] private Transform _gunPivot;
        [SerializeField] private Transform _playerVisual;
        [SerializeField] private PlayerCosmetics _cosmetics;
        [SerializeField] private WeaponManager _weaponManager;
        [SerializeField] private AutoShooter _autoShooter;
        [SerializeField] private InputRouter _inputRouter;
        [SerializeField] private PlayerState _playerState;
        [SerializeField] private PlayerStats _stats;
        [SerializeField] private Rigidbody2D _rigidbody;

        [Header("Movement Visuals")]
        [SerializeField] private float _bodyTiltDegrees = 15f;
        [SerializeField] private float _tiltSmoothTime = 0.07f;
        [SerializeField] private float _idleSwayFrequency = 6f;
        [SerializeField] private float _idleSwayAmplitude = 1.2f;

        [Header("Cursor Settings")]
        [SerializeField] private bool _lockCursor = true;

        private Vector2 _screenLookPosition;
        private float _tiltVelocity;
        private Vector3 _baseGunLocalPosition;
        private float _currentRecoil;
        private float _recoilTimer;
        private Weapon _currentWeapon;

        private void Awake()
        {
            if (!ValidateDependencies())
            {
                Debug.LogError($"{nameof(PlayerPresentation)} on {name} disabled due to missing dependencies.", this);
                enabled = false;
                return;
            }
        }

        private void OnEnable()
        {
            if (_inputRouter != null)
            {
                _inputRouter.OnLook += HandleLook;
            }

            if (_autoShooter != null)
            {
                _autoShooter.OnShotFired += HandleShotFired;
            }

            if (_weaponManager != null)
            {
                _weaponManager.OnWeaponChanged += HandleWeaponChanged;
            }

            if (_lockCursor)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Confined;
            }
        }

        private void Start()
        {
            _screenLookPosition = _inputRouter != null ? _inputRouter.LookInput : Vector2.zero;
            HandleWeaponChanged(_weaponManager != null ? _weaponManager.CurrentInstance : null);
            ApplyCharacterSelection();
        }

        private void OnDisable()
        {
            if (_inputRouter != null)
            {
                _inputRouter.OnLook -= HandleLook;
            }

            if (_autoShooter != null)
            {
                _autoShooter.OnShotFired -= HandleShotFired;
            }

            if (_weaponManager != null)
            {
                _weaponManager.OnWeaponChanged -= HandleWeaponChanged;
            }
        }

        private void Update()
        {
            RotateTowardsPointer();
            UpdateBodyTilt();
            UpdateRecoil();
        }

        private void OnValidate()
        {
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

            if (!_rigidbody) _rigidbody = GetComponent<Rigidbody2D>();
            if (!_stats) _stats = GetComponent<PlayerStats>();
            if (!_weaponManager) _weaponManager = GetComponentInChildren<WeaponManager>();
            if (!_autoShooter && _weaponManager) _autoShooter = _weaponManager.Shooter;
            if (!_inputRouter) _inputRouter = GetComponent<InputRouter>();
            if (!_playerState) _playerState = GetComponent<PlayerState>();
            if (!_cosmetics) _cosmetics = GetComponent<PlayerCosmetics>();
            if (!_cosmetics && _playerVisual)
            {
                _cosmetics = _playerVisual.GetComponent<PlayerCosmetics>();
            }
        }

        private void HandleLook(Vector2 screenPosition)
        {
            _screenLookPosition = screenPosition;
        }

        private void HandleShotFired(Weapon weapon)
        {
            if (weapon == null || _gunPivot == null)
            {
                return;
            }

            _currentRecoil = weapon.recoilAmount;
            _recoilTimer = 0f;
        }

        private void HandleWeaponChanged(WeaponInstance weaponInstance)
        {
            _currentWeapon = weaponInstance != null ? weaponInstance.Weapon : null;
            _currentRecoil = 0f;
            _recoilTimer = 0f;

            if (_gunPivot)
            {
                _baseGunLocalPosition = _gunPivot.localPosition;
            }
        }

        private void RotateTowardsPointer()
        {
            if (_camera == null || _gunPivot == null || !_playerState || !_playerState.CanAct)
            {
                return;
            }

            Vector3 worldMouse = _camera.ScreenToWorldPoint(_screenLookPosition);
            Vector2 direction = worldMouse - _gunPivot.position;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            _gunPivot.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

            bool isAimingLeft = direction.x < 0f;
            _gunPivot.localScale = isAimingLeft ? new Vector3(1f, -1f, 1f) : Vector3.one;
            if (_playerVisual)
            {
                _playerVisual.localScale = isAimingLeft ? new Vector3(-1f, 1f, 1f) : Vector3.one;
            }
        }

        private void UpdateBodyTilt()
        {
            if (!_playerVisual || _rigidbody == null || _stats == null)
            {
                return;
            }

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

        private void UpdateRecoil()
        {
            if (_gunPivot == null || _currentWeapon == null)
            {
                return;
            }

            _gunPivot.localPosition = _baseGunLocalPosition;

            _recoilTimer += Time.deltaTime * 10f;
            float kick = Mathf.Lerp(_currentRecoil, 0f, _recoilTimer);

            Vector3 recoilDirection = -_gunPivot.right * (kick * 0.1f);
            Transform pivotParent = _gunPivot.parent;
            Vector3 localRecoil = pivotParent ? pivotParent.InverseTransformDirection(recoilDirection) : recoilDirection;

            _gunPivot.localPosition = _baseGunLocalPosition + localRecoil;

            _currentRecoil = Mathf.Lerp(_currentRecoil, 0f, Time.deltaTime * _currentWeapon.recoilRecoverySpeed);
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
                _playerState?.OverrideStartingWeapon(weapon);
            }
        }

        private bool ValidateDependencies()
        {
            bool ok = true;

            if (!_camera)
            {
                Debug.LogError("Missing Camera reference.", this);
                ok = false;
            }

            if (!_gunPivot)
            {
                Debug.LogError("Missing gun pivot reference.", this);
                ok = false;
            }

            if (!_playerVisual)
            {
                Debug.LogError("Missing player visual reference.", this);
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

            if (!_weaponManager)
            {
                Debug.LogError("Missing WeaponManager reference.", this);
                ok = false;
            }

            if (!_autoShooter)
            {
                Debug.LogError("Missing AutoShooter reference.", this);
                ok = false;
            }

            return ok;
        }
    }
}
