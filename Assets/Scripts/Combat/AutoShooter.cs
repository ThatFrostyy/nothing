using System;
using UnityEngine;

namespace FF
{
    public class AutoShooter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Weapon _weapon;
        [SerializeField] private Transform _muzzle;
        [SerializeField] private Transform _ejectPos;
        [SerializeField] private MonoBehaviour _statsProvider;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private Rigidbody2D _playerBody;
        [SerializeField] private InputRouter _inputRouter;
        [SerializeField] private WeaponManager _weaponManager;
        [SerializeField] private FireController _fireController;
        [SerializeField] private AccuracyController _accuracyController;
        [SerializeField] private ProjectileSpawner _projectileSpawner;
        [SerializeField] private RecoilPresenter _recoilPresenter;
        [SerializeField] private AudioController _audioController;

        private ICombatStats _stats;
        private bool _isFireHeld;
        private bool _isGrenadeWeapon;

        public event Action<float> OnCooldownChanged;
        public event Action<float> OnGrenadeChargeChanged;
        public event Action<Weapon> OnShotFired;

        public float CooldownProgress => _fireController ? _fireController.CooldownProgress : 1f;
        public float GrenadeChargeProgress => _fireController ? _fireController.GrenadeChargeProgress : 0f;

        #region Initialization
        private void Awake()
        {
            CacheInterfaces();

            if (!ValidateDependencies())
            {
                Debug.LogError($"{nameof(AutoShooter)} on {name} disabled due to missing dependencies.", this);
                enabled = false;
                return;
            }
        }

        private void OnEnable()
        {
            if (_inputRouter != null)
            {
                _inputRouter.OnFireStart += HandleFireStarted;
                _inputRouter.OnFireStop += HandleFireStopped;
            }

            if (_weaponManager != null)
            {
                _weaponManager.OnWeaponChanged += HandleWeaponChanged;
                HandleWeaponChanged(_weaponManager.CurrentInstance);
            }

            if (_fireController != null)
            {
                _fireController.OnShot += HandleShot;
                _fireController.OnCooldownChanged += HandleCooldownChanged;
                _fireController.OnGrenadeChargeChanged += HandleGrenadeChargeChanged;
            }
        }

        private void OnDisable()
        {
            if (_inputRouter != null)
            {
                _inputRouter.OnFireStart -= HandleFireStarted;
                _inputRouter.OnFireStop -= HandleFireStopped;
            }

            if (_weaponManager != null)
            {
                _weaponManager.OnWeaponChanged -= HandleWeaponChanged;
            }

            if (_fireController != null)
            {
                _fireController.OnShot -= HandleShot;
                _fireController.OnCooldownChanged -= HandleCooldownChanged;
                _fireController.OnGrenadeChargeChanged -= HandleGrenadeChargeChanged;
                _fireController.SetFireHeld(false);
                _fireController.SetWeapon(null, false);
            }

            _isFireHeld = false;
        }

        public void SetStatsProvider(ICombatStats stats)
        {
            _stats = stats;
            _statsProvider = stats as MonoBehaviour;
        }

        public void SetWeapon(WeaponInstance weaponInstance)
        {
            if (weaponInstance == null || weaponInstance.Weapon == null)
            {
                ClearWeapon();
                return;
            }

            _weapon = weaponInstance.Weapon;
            _muzzle = weaponInstance.Muzzle;
            _ejectPos = weaponInstance.Eject;
            _isGrenadeWeapon = _weapon && _weapon.bulletPrefab && _weapon.bulletPrefab.TryGetComponent<GrenadeProjectile>(out _);

            if (_fireController)
            {
                _fireController.SetWeapon(_weapon, _isGrenadeWeapon);
            }

            if (_accuracyController)
            {
                _accuracyController.SetWeapon(_weapon);
            }

            if (_projectileSpawner)
            {
                _projectileSpawner.ConfigureWeapon(_weapon, _muzzle, _ejectPos, _isGrenadeWeapon);
            }

            if (_recoilPresenter)
            {
                _recoilPresenter.SetWeapon(_weapon);
            }

            HandleCooldownChanged(CooldownProgress);
            HandleGrenadeChargeChanged(GrenadeChargeProgress);
        }

        public void ClearWeapon()
        {
            _weapon = null;
            _muzzle = null;
            _ejectPos = null;
            _isGrenadeWeapon = false;

            if (_fireController)
            {
                _fireController.SetWeapon(null, false);
            }

            _accuracyController?.SetWeapon(null);
            _projectileSpawner?.ClearWeapon();
            _recoilPresenter?.SetWeapon(null);

            HandleCooldownChanged(CooldownProgress);
            HandleGrenadeChargeChanged(GrenadeChargeProgress);
        }

        public void SetFireHeld(bool isHeld)
        {
            _isFireHeld = isHeld;

            if (_fireController)
            {
                _fireController.SetFireHeld(isHeld);
            }
        }

        public void QueueFirePress()
        {
            if (_fireController)
            {
                _fireController.QueueFirePress();
            }
        }

        public void SetCameraShakeEnabled(bool enabled)
        {
            if (_recoilPresenter)
            {
                _recoilPresenter.SetCameraShakeEnabled(enabled);
            }
        }
        #endregion Initialization

        private void OnValidate()
        {
            if (!_statsProvider)
            {
                MonoBehaviour[] parents = GetComponentsInParent<MonoBehaviour>(includeInactive: true);
                foreach (MonoBehaviour behaviour in parents)
                {
                    if (behaviour is ICombatStats)
                    {
                        _statsProvider = behaviour;
                        break;
                    }
                }
            }

            CacheInterfaces();

            if (!_audioSource) _audioSource = GetComponent<AudioSource>();
            if (!_playerBody) _playerBody = GetComponentInParent<Rigidbody2D>();
            if (!_inputRouter) _inputRouter = GetComponentInParent<InputRouter>();
            if (!_weaponManager) _weaponManager = GetComponentInParent<WeaponManager>();
            if (!_fireController) _fireController = GetComponent<FireController>();
            if (!_accuracyController) _accuracyController = GetComponent<AccuracyController>();
            if (!_projectileSpawner) _projectileSpawner = GetComponent<ProjectileSpawner>();
            if (!_recoilPresenter) _recoilPresenter = GetComponent<RecoilPresenter>();
            if (!_audioController) _audioController = GetComponent<AudioController>();
        }

        private void CacheInterfaces()
        {
            _stats = _statsProvider as ICombatStats;
        }

        private bool ValidateDependencies()
        {
            bool ok = true;

            if (!_audioSource)
            {
                Debug.LogError("Missing AudioSource reference.", this);
                ok = false;
            }

            if (!_playerBody)
            {
                Debug.LogError("Missing Rigidbody2D reference.", this);
                ok = false;
            }

            if (_statsProvider && _stats == null)
            {
                Debug.LogError("Stats provider does not implement ICombatStats.", this);
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

            if (!_fireController)
            {
                Debug.LogError("Missing FireController reference.", this);
                ok = false;
            }

            if (!_accuracyController)
            {
                Debug.LogError("Missing AccuracyController reference.", this);
                ok = false;
            }

            if (!_projectileSpawner)
            {
                Debug.LogError("Missing ProjectileSpawner reference.", this);
                ok = false;
            }

            if (!_recoilPresenter)
            {
                Debug.LogError("Missing RecoilPresenter reference.", this);
                ok = false;
            }

            if (!_audioController)
            {
                Debug.LogError("Missing AudioController reference.", this);
                ok = false;
            }

            return ok;
        }

        private void HandleWeaponChanged(WeaponInstance weaponInstance)
        {
            SetWeapon(weaponInstance);
        }

        private void HandleFireStarted()
        {
            SetFireHeld(true);
            QueueFirePress();
        }

        private void HandleFireStopped()
        {
            SetFireHeld(false);
        }

        private void Update()
        {
            if (_weapon == null || _muzzle == null)
            {
                HandleCooldownChanged(CooldownProgress);
                HandleGrenadeChargeChanged(GrenadeChargeProgress);
                return;
            }

            float rpmMultiplier = _stats != null ? _stats.GetFireRateMultiplier() : 1f;
            float cooldownMultiplier = _stats != null ? _stats.GetFireCooldownMultiplier() : 1f;

            _recoilPresenter?.UpdateHold(_isFireHeld, _weapon.isAuto, Time.deltaTime);
            _accuracyController?.TickSpread(_weapon, _playerBody, _stats, Time.deltaTime);

            _fireController?.Tick(_weapon, rpmMultiplier, cooldownMultiplier, Time.deltaTime);
        }

        #region Shot Handling
        private void HandleShot(float? grenadeSpeedOverride)
        {
            if (_weapon == null || _muzzle == null)
            {
                return;
            }

            _accuracyController?.RegisterShot(_weapon, _playerBody, _stats);
            Quaternion spreadRotation = _accuracyController != null
                ? _accuracyController.GetShotRotation(_weapon, _muzzle.rotation)
                : _muzzle.rotation;

            string ownerTag = transform.root ? transform.root.tag : gameObject.tag;

            _projectileSpawner?.SpawnShot(_weapon, spreadRotation, grenadeSpeedOverride, _stats, ownerTag);
            _audioController?.PlayFire(_weapon, _muzzle.position, _audioSource);
            _recoilPresenter?.HandleShot(_weapon);

            OnShotFired?.Invoke(_weapon);
        }

        private void HandleCooldownChanged(float value)
        {
            OnCooldownChanged?.Invoke(value);
        }

        private void HandleGrenadeChargeChanged(float value)
        {
            OnGrenadeChargeChanged?.Invoke(value);
        }
        #endregion
    }
}
