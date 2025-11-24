using System;
using UnityEngine;
using UnityEngine.Audio;

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

        private ICombatStats _stats;

        private float _fireTimer;
        private bool _isFireHeld;
        private bool _isFirePressed;

        [Header("Grenade Charging")]
        [SerializeField, Min(0.1f)] private float grenadeMinThrowSpeed = 8f;
        [SerializeField, Min(0.1f)] private float grenadeMaxThrowSpeed = 22f;
        [SerializeField, Min(0.05f)] private float grenadeChargeTime = 1.1f;
        [SerializeField] private AnimationCurve grenadeChargeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Camera Shake")]
        [SerializeField] private bool _cameraShakeEnabled = true;
        private float sustainedFireTime;
        [SerializeField] float maxSustainedShake = 0.35f;

        private float _currentSpread;

        private float _currentCooldownProgress = 1f;
        private float _currentChargeProgress;
        private bool _isGrenadeWeapon;
        private bool _isChargingGrenade;

        public event Action<float> OnCooldownChanged;
        public event Action<float> OnGrenadeChargeChanged;
        public event Action<Weapon> OnShotFired;

        public float CooldownProgress => _currentCooldownProgress;
        public float GrenadeChargeProgress => _currentChargeProgress;

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
        }

        private void OnDisable()
        {
            if (_inputRouter != null)
            {
                _inputRouter.OnFireStart -= HandleFireStarted;
                _inputRouter.OnFireStop -= HandleFireStopped;
            }

            SetFireHeld(false);
        }

        public void SetStatsProvider(ICombatStats stats)
        {
            _stats = stats;
            _statsProvider = stats as MonoBehaviour;
        }

        public void SetWeapon(Weapon weapon, Transform muzzleTransform, Transform eject)
        {
            _weapon = weapon;
            _muzzle = muzzleTransform;
            _ejectPos = eject;
            _currentSpread = _weapon.baseSpread;
            _isGrenadeWeapon = _weapon && _weapon.bulletPrefab && _weapon.bulletPrefab.TryGetComponent<GrenadeProjectile>(out _);
            SetGrenadeChargeProgress(0f);

            SetCooldownProgress(1f);

            NotifyCooldownChanged();
        }

        public void ClearWeapon()
        {
            _weapon = null;
            _muzzle = null;
            _ejectPos = null;
            _isGrenadeWeapon = false;
            SetCooldownProgress(1f);
            SetGrenadeChargeProgress(0f);
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

            return ok;
        }

        private void HandleFireStarted()
        {
            SetFireHeld(true);
        }

        private void HandleFireStopped()
        {
            SetFireHeld(false);
        }

        private void Update()
        {
            if (_weapon == null || _muzzle == null)
            {
                SetCooldownProgress(1f);
                SetGrenadeChargeProgress(0f);
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

            float cooldownMultiplier = _stats != null ? _stats.GetFireCooldownMultiplier() : 1f;
            interval *= Mathf.Max(0.1f, cooldownMultiplier);

            if (!_weapon.isAuto && _weapon.fireCooldown > 0f)
            {
                interval = _weapon.fireCooldown / rpmMultiplier;
                interval *= Mathf.Max(0.1f, cooldownMultiplier);
            }

            if (_isGrenadeWeapon)
            {
                HandleGrenadeCharging(interval);
            }
            else
            {
                HandleStandardFiring(interval);
            }

            float movementSpeed = _playerBody ? _playerBody.linearVelocity.magnitude : 0f;
            bool isMoving = movementSpeed > 0.1f;

            float movementPenalty = 1f;
            if (_stats != null)
            {
                movementPenalty = _stats.GetMovementAccuracyPenalty();
            }
            float targetSpread = _weapon.baseSpread * (isMoving ? movementPenalty : 1f);
            _currentSpread = Mathf.Lerp(_currentSpread, targetSpread, Time.deltaTime * _weapon.spreadRecoverySpeed);
        }

        #region Recoil & Shooting
        private void Shoot(float? grenadeSpeedOverride = null)
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

            SpawnEjectParticles();

            bool launchedGrenade = TryLaunchGrenade(spreadRotation, grenadeSpeedOverride);
            if (!launchedGrenade)
            {
                SpawnStandardBullet(spreadRotation);
            }

            PlayFireAudio();
            SpawnMuzzleFlash();

            if (_cameraShakeEnabled)
            {
                float shakeStrength = Mathf.Lerp(0.05f, maxSustainedShake, sustainedFireTime);
                CameraShake.Shake(shakeStrength, shakeStrength);
            }

            SetCooldownProgress(0f);
            OnShotFired?.Invoke(_weapon);
        }

        private void SpawnEjectParticles()
        {
            if (!_weapon.ejectParticles || !_ejectPos)
            {
                return;
            }

            GameObject ejectInstance = PoolManager.Get(_weapon.ejectParticles, _ejectPos.position, _ejectPos.rotation);
            if (ejectInstance && !ejectInstance.TryGetComponent<PooledParticleSystem>(out var ejectPooled))
            {
                ejectPooled = ejectInstance.AddComponent<PooledParticleSystem>();
                ejectPooled.OnTakenFromPool();
            }
        }

        private void SpawnStandardBullet(Quaternion spreadRotation)
        {
            if (!_weapon.bulletPrefab)
            {
                return;
            }

            GameObject bulletInstance = PoolManager.Get(_weapon.bulletPrefab, _muzzle.position, spreadRotation);

            if (bulletInstance.TryGetComponent<Bullet>(out var bullet))
            {
                float damageMultiplier = GetFinalDamageMultiplier(out _);
                float projectileSpeedMultiplier = _stats != null ? _stats.GetProjectileSpeedMultiplier() : 1f;
                bullet.SetDamage(Mathf.RoundToInt(_weapon.damage * damageMultiplier));
                string ownerTag = transform.root ? transform.root.tag : gameObject.tag;
                bullet.SetOwner(ownerTag);
                bullet.SetSpeed(bullet.BaseSpeed * Mathf.Max(0.01f, projectileSpeedMultiplier));
            }
        }

        private bool TryLaunchGrenade(Quaternion spreadRotation, float? speedOverride = null)
        {
            if (!_weapon.bulletPrefab)
            {
                return false;
            }

            if (!_weapon.bulletPrefab.TryGetComponent<GrenadeProjectile>(out _))
            {
                return false;
            }

            GameObject grenadeInstance = PoolManager.Get(_weapon.bulletPrefab, _muzzle.position, spreadRotation);
            if (!grenadeInstance.TryGetComponent<GrenadeProjectile>(out var grenade))
            {
                return false;
            }

            float damageMultiplier = GetFinalDamageMultiplier(out _);
            Vector2 direction = spreadRotation * Vector3.right;
            string ownerTag = transform.root ? transform.root.tag : gameObject.tag;
            AudioMixerGroup mixer = _audioSource ? _audioSource.outputAudioMixerGroup : null;
            float spatialBlend = _audioSource ? _audioSource.spatialBlend : 0f;
            float volume = _audioSource ? _audioSource.volume : 1f;
            float pitch = _audioSource ? _audioSource.pitch : 1f;

            float projectileSpeedMultiplier = _stats != null ? _stats.GetProjectileSpeedMultiplier() : 1f;
            float baseLaunchSpeed = grenade.BaseLaunchSpeed;
            float finalLaunchSpeed = speedOverride.HasValue
                ? Mathf.Max(0.1f, speedOverride.Value * projectileSpeedMultiplier)
                : Mathf.Max(0.1f, baseLaunchSpeed * projectileSpeedMultiplier);

            grenade.Launch(direction, _weapon.damage, damageMultiplier, ownerTag, mixer, spatialBlend, volume, pitch, null, finalLaunchSpeed);
            return true;
        }

        private void PlayFireAudio()
        {
            if (!_weapon.fireSFX)
            {
                return;
            }

            AudioMixerGroup mixer = _audioSource ? _audioSource.outputAudioMixerGroup : null;
            float spatialBlend = _audioSource ? _audioSource.spatialBlend : 0f;
            float volume = _audioSource ? _audioSource.volume : 1f;
            float pitch = _audioSource ? _audioSource.pitch : 1f;
            AudioPlaybackPool.PlayOneShot(_weapon.fireSFX, _muzzle.position, mixer, spatialBlend, volume, pitch);
        }

        private void SpawnMuzzleFlash()
        {
            if (!_weapon.muzzleFlash)
            {
                return;
            }

            GameObject flashInstance = PoolManager.Get(_weapon.muzzleFlash, _muzzle.position, _muzzle.rotation);
            if (flashInstance && !flashInstance.TryGetComponent<PooledParticleSystem>(out var flashPooled))
            {
                flashPooled = flashInstance.AddComponent<PooledParticleSystem>();
                flashPooled.OnTakenFromPool();
            }
        }

        private float GetFinalDamageMultiplier(out bool isCrit)
        {
            float damageMultiplier = _stats != null ? _stats.GetDamageMultiplier() : 1f;
            float critChance = _stats != null ? _stats.GetCritChance() : 0f;
            float critDamageMultiplier = _stats != null ? _stats.GetCritDamageMultiplier() : 1f;

            bool didCrit = critChance > 0f && UnityEngine.Random.value < critChance;
            isCrit = didCrit;

            if (!didCrit)
            {
                return damageMultiplier;
            }

            return damageMultiplier * Mathf.Max(1f, critDamageMultiplier);
        }
        #endregion Recoil & Shooting

        #region Grenade Charging
        private void HandleStandardFiring(float interval)
        {
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
        }

        private void HandleGrenadeCharging(float interval)
        {
            if (_fireTimer < interval)
            {
                _isChargingGrenade = false;
                SetGrenadeChargeProgress(0f);
                SetCooldownProgress(Mathf.Clamp01(_fireTimer / interval));
                return;
            }

            SetCooldownProgress(1f);

            if (_isFireHeld)
            {
                if (!_isChargingGrenade)
                {
                    _isChargingGrenade = true;
                    SetGrenadeChargeProgress(0f);
                }

                float chargeDelta = grenadeChargeTime > 0.001f
                    ? Time.deltaTime / grenadeChargeTime
                    : 1f;
                SetGrenadeChargeProgress(_currentChargeProgress + chargeDelta);
            }
            else if (_isChargingGrenade)
            {
                float curved = grenadeChargeCurve != null
                    ? grenadeChargeCurve.Evaluate(_currentChargeProgress)
                    : _currentChargeProgress;
                float throwSpeed = Mathf.Lerp(grenadeMinThrowSpeed, grenadeMaxThrowSpeed, Mathf.Clamp01(curved));
                _fireTimer = 0f;
                _isFirePressed = false;
                _isChargingGrenade = false;
                Shoot(throwSpeed);
                SetGrenadeChargeProgress(0f);
            }
            else
            {
                SetGrenadeChargeProgress(0f);
            }
        }

        private void SetGrenadeChargeProgress(float progress)
        {
            _currentChargeProgress = Mathf.Clamp01(progress);
            OnGrenadeChargeChanged?.Invoke(_currentChargeProgress);
        }
        #endregion Grenade Charging

        #region Cooldown
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
            OnCooldownChanged?.Invoke(_currentCooldownProgress);
        }
        #endregion Cooldown
    }
}
