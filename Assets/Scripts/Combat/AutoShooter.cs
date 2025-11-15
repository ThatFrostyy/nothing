using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FF
{
    public class AutoShooter : MonoBehaviour
    {
        private Weapon _weapon;
        private Transform _muzzle;
        private Transform _ejectPos;
        private ICombatStats _stats;
        private AudioSource _audioSource;
        private Rigidbody2D _playerBody;

        private float _fireTimer;
        private bool _isFireHeld;
        private bool _isFirePressed;

        [SerializeField] private bool _cameraShakeEnabled = true;

        private float sustainedFireTime;
        [SerializeField] float maxSustainedShake = 0.35f;

        private float _recoilTimer;
        private float _currentSpread;
        private float _currentRecoil;
        private Vector3 _baseLocalPosition;
        private Transform _gunPivot;

        private float _currentCooldownProgress = 1f;

        public event Action<float> OnCooldownChanged;

        public float CooldownProgress => _currentCooldownProgress;

        #region Initialization
        private void Awake()
        {
            _stats = GetComponentInParent<ICombatStats>();
            _audioSource = GetComponent<AudioSource>();
            _playerBody = GetComponentInParent<Rigidbody2D>();
        }

        public void SetStatsProvider(ICombatStats stats)
        {
            _stats = stats;
        }

        public void InitializeRecoil(Transform gunPivotTransform)
        {
            _gunPivot = gunPivotTransform;
        }

        public void SetWeapon(Weapon weapon, Transform muzzleTransform, Transform eject)
        {
            _weapon = weapon;
            _muzzle = muzzleTransform;
            _ejectPos = eject;
            _currentSpread = _weapon.baseSpread;

            SetCooldownProgress(1f);

            if (_gunPivot)
            {
                _baseLocalPosition = _gunPivot.localPosition;
                _currentRecoil = 0f;
                _recoilTimer = 0f;
                _gunPivot.localPosition = _baseLocalPosition;
            }

            NotifyCooldownChanged();
        }

        public void SetFireHeld(bool isHeld)
        {
            if (isHeld)
            {
                if (!_isFireHeld)
                    _isFirePressed = true;
            }
            else
            {
                _isFirePressed = false;
            }

            _isFireHeld = isHeld;
        }

        public void SetCameraShakeEnabled(bool enabled)
        {
            _cameraShakeEnabled = enabled;
        }
        #endregion Initialization

        public void OnFire(InputValue value)
        {
            SetFireHeld(value.Get<float>() > 0.5f);
        }

        private void Update()
        {
            if (_weapon == null || _muzzle == null)
            {
                SetCooldownProgress(1f);
                return;
            }

            if (_isFireHeld && _weapon.isAuto)
            {
                sustainedFireTime += Time.deltaTime;
                sustainedFireTime = Mathf.Clamp(sustainedFireTime, 0f, 1f);
            }
            else
            {
                sustainedFireTime -= Time.deltaTime * 1.5f;
                sustainedFireTime = Mathf.Clamp(sustainedFireTime, 0f, 1f);
            }

            _fireTimer += Time.deltaTime;

            float rpmMultiplier = 1f;
            if (_stats != null)
            {
                rpmMultiplier = _stats.GetFireRateMultiplier();
            }
            float rpm = Mathf.Max(_weapon.rpm * rpmMultiplier, 0.01f);
            float interval = 60f / rpm;

            if (_fireTimer >= interval)
            {
                if (!_weapon.isAuto && _isFirePressed)
                {
                    _fireTimer = 0f;
                    _isFirePressed = false;
                    Shoot();
                }

                if (_weapon.isAuto && _isFireHeld)
                {
                    _fireTimer = 0f;
                    Shoot();
                }
            }

            float cooldownFraction = Mathf.Clamp01(_fireTimer / interval);
            SetCooldownProgress(cooldownFraction);

            float movementSpeed = _playerBody ? _playerBody.linearVelocity.magnitude : 0f;
            bool isMoving = movementSpeed > 0.1f;

            float movementPenalty = 1f;
            if (_stats != null)
            {
                movementPenalty = _stats.GetMovementAccuracyPenalty();
            }
            float targetSpread = _weapon.baseSpread * (isMoving ? movementPenalty : 1f);
            _currentSpread = Mathf.Lerp(_currentSpread, targetSpread, Time.deltaTime * _weapon.spreadRecoverySpeed);

            UpdateRecoil();
        }

        #region Recoil & Shooting
        private void Shoot()
        {
            _currentSpread += _weapon.spreadIncreasePerShot;

            bool isMoving = _playerBody && _playerBody.linearVelocity.magnitude > 0.1f;
            float movementPenalty = 1f;
            if (_stats != null)
            {
                movementPenalty = _stats.GetMovementAccuracyPenalty();
            }
            float maxSpread = _weapon.maxSpread * (isMoving ? movementPenalty : 1f);
            _currentSpread = Mathf.Clamp(_currentSpread, _weapon.baseSpread, maxSpread);

            float angleOffset = UnityEngine.Random.Range(-_currentSpread, _currentSpread);
            Quaternion spreadRotation = _muzzle.rotation * Quaternion.AngleAxis(angleOffset, Vector3.forward);

            if (_weapon.ejectParticles)
            {
                GameObject ejectInstance = PoolManager.Get(_weapon.ejectParticles, _ejectPos.position, _ejectPos.rotation);
                if (ejectInstance && !ejectInstance.TryGetComponent<PooledParticleSystem>(out var ejectPooled))
                {
                    ejectPooled = ejectInstance.AddComponent<PooledParticleSystem>();
                    ejectPooled.OnTakenFromPool();
                }
            }

            GameObject bulletInstance = PoolManager.Get(_weapon.bulletPrefab, _muzzle.position, spreadRotation);

            if (bulletInstance.TryGetComponent<Bullet>(out var bullet))
            {
                float damageMultiplier = 1f;
                if (_stats != null)
                {
                    damageMultiplier = _stats.GetDamageMultiplier();
                }
                bullet.SetDamage(Mathf.RoundToInt(_weapon.damage * damageMultiplier));
                bullet.SetOwner(transform.root.tag);
            }

            if (_weapon.fireSFX && _audioSource)
            {
                _audioSource.PlayOneShot(_weapon.fireSFX);
            }

            if (_weapon.muzzleFlash)
            {
                GameObject flashInstance = PoolManager.Get(_weapon.muzzleFlash, _muzzle.position, _muzzle.rotation);
                if (flashInstance && !flashInstance.TryGetComponent<PooledParticleSystem>(out var flashPooled))
                {
                    flashPooled = flashInstance.AddComponent<PooledParticleSystem>();
                    flashPooled.OnTakenFromPool();
                }
            }

            if (_cameraShakeEnabled)
            {
                float shakeStrength = Mathf.Lerp(0.05f, maxSustainedShake, sustainedFireTime);
                CameraShake.Shake(shakeStrength, shakeStrength);
            }

            _currentRecoil = _weapon.recoilAmount;
            _recoilTimer = 0f;
            SetCooldownProgress(0f);
        }

        private void UpdateRecoil()
        {
            if (!_gunPivot)
            {
                return;
            }

            _gunPivot.localPosition = _baseLocalPosition;

            _recoilTimer += Time.deltaTime * 10f;
            float kick = Mathf.Lerp(_currentRecoil, 0f, _recoilTimer);

            Vector3 recoilDirection = -_gunPivot.right * (kick * 0.1f);
            Transform pivotParent = _gunPivot.parent;
            Vector3 localRecoil = pivotParent ? pivotParent.InverseTransformDirection(recoilDirection) : recoilDirection;

            _gunPivot.localPosition = _baseLocalPosition + localRecoil;

            _currentRecoil = Mathf.Lerp(_currentRecoil, 0f, Time.deltaTime * _weapon.recoilRecoverySpeed);
        }
#endregion Recoil & Shooting

        private void SetCooldownProgress(float value)
        {
            value = Mathf.Clamp01(value);

            if (Mathf.Approximately(_currentCooldownProgress, value))
            {
                return;
            }

            _currentCooldownProgress = value;
            NotifyCooldownChanged();
        }

        void NotifyCooldownChanged()
        {
            var handler = OnCooldownChanged;
            if (handler != null)
            {
                handler(_currentCooldownProgress);
            }
        }
    }
}
