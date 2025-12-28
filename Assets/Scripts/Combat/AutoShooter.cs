using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
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
        private CharacterAbilityController _abilityController;
        private AudioSource _attackLoopSource;
        private AudioSource _fireLoopSource;
        private GameObject _activeLoopingVfx;
        private GameObject _flamethrowerInstance;
        private FlamethrowerEmitter _flamethrowerEmitter;

        private float _fireTimer;
        private bool _isFireHeld;
        private bool _isFirePressed;

        [Header("Extra Projectiles")]
        [SerializeField, Min(0f)] private float extraProjectileDelay = 0.05f;

        [Header("Slowmo Tweaks")]
        [SerializeField, Min(0f)] private float _slowmoProjectileSpeedMultiplier = 1f;

        [Header("Grenade Charging")]
        [SerializeField, Min(0.1f)] private float grenadeMinThrowSpeed = 8f;
        [SerializeField, Min(0.1f)] private float grenadeMaxThrowSpeed = 22f;
        [SerializeField, Min(0.05f)] private float grenadeChargeTime = 1.1f;
        [SerializeField] private AnimationCurve grenadeChargeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Camera Shake")]
        [SerializeField] private bool _cameraShakeEnabled = true;
        private float sustainedFireTime;
        [SerializeField] float maxSustainedShake = 0.35f;

        private float _recoilTimer;
        private float _currentSpread;
        private float _currentRecoil;
        private Vector3 _baseLocalPosition;
        private bool _hasBaseLocalPosition;
        private Transform _gunPivot;
        private float _flamethrowerHeat;
        private bool _flamethrowerOverheated;

        private float _currentCooldownProgress = 1f;
        private float _currentChargeProgress;
        private bool _isGrenadeWeapon;
        private bool _isChargingGrenade;
        private bool _useGrenadeCharging;

        public static event Action<AutoShooter, int> OnRoundsFired;

        public event Action<float> OnCooldownChanged;
        public event Action<float> OnGrenadeChargeChanged;

        public float CooldownProgress => _currentCooldownProgress;
        public float GrenadeChargeProgress => _currentChargeProgress;
        public Weapon CurrentWeapon => _weapon;

        #region Initialization
        private void Awake()
        {
            _stats = GetComponentInParent<ICombatStats>();
            _audioSource = GetComponent<AudioSource>();
            _playerBody = GetComponentInParent<Rigidbody2D>();
            _abilityController = GetComponentInParent<CharacterAbilityController>();
        }

        public void SetStatsProvider(ICombatStats stats)
        {
            _stats = stats;
        }

        public void InitializeRecoil(Transform gunPivotTransform)
        {
            // Fix: Detect if we are re-initializing the same pivot.
            // If we already have a base position for this pivot, we must NOT 
            // overwrite it with the current localPosition, because the gun might 
            // be currently recoiled (moved back) due to firing.
            if (_hasBaseLocalPosition && _gunPivot == gunPivotTransform)
            {
                // Just reset the gun to the known correct base position
                _gunPivot.localPosition = _baseLocalPosition;
                _currentRecoil = 0f;
                _recoilTimer = 0f;
                return;
            }

            _gunPivot = gunPivotTransform;
            if (_gunPivot)
            {
                _baseLocalPosition = _gunPivot.localPosition;
                _hasBaseLocalPosition = true;
                _currentRecoil = 0f;
                _recoilTimer = 0f;
                _gunPivot.localPosition = _baseLocalPosition;
            }
        }

        public void SetWeapon(Weapon weapon, Transform muzzleTransform, Transform eject)
        {
            StopLoopingFeedback();
            _weapon = weapon;
            _muzzle = muzzleTransform;
            _ejectPos = eject;
            _currentSpread = _weapon.baseSpread;
            _isGrenadeWeapon = _weapon && _weapon.bulletPrefab && _weapon.bulletPrefab.TryGetComponent<GrenadeProjectile>(out _);
            _useGrenadeCharging = _weapon && _weapon.useGrenadeCharging;
            SetGrenadeChargeProgress(0f);
            _flamethrowerHeat = 0f;
            _flamethrowerOverheated = false;
            _isChargingGrenade = false;

            SetCooldownProgress(1f);

            SetupFlamethrowerEmitter();

            if (_gunPivot)
            {
                if (!_hasBaseLocalPosition)
                {
                    _baseLocalPosition = _gunPivot.localPosition;
                    _hasBaseLocalPosition = true;
                }

                _currentRecoil = 0f;
                _recoilTimer = 0f;
                _gunPivot.localPosition = _baseLocalPosition;
            }

            NotifyCooldownChanged();
        }

        public void ClearWeapon()
        {
            StopLoopingFeedback();
            _weapon = null;
            _muzzle = null;
            _ejectPos = null;
            _isGrenadeWeapon = false;
            _useGrenadeCharging = false;
            _isChargingGrenade = false;
            _flamethrowerHeat = 0f;
            _flamethrowerOverheated = false;
            SetCooldownProgress(1f);
            SetGrenadeChargeProgress(0f);
            CleanupFlamethrowerEmitter();
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

        private void OnDisable()
        {
            StopLoopingFeedback();

            if (_flamethrowerEmitter)
            {
                _flamethrowerEmitter.Tick(false, 0, 1f, 0f, 1f, ResolveOwnerTag());
            }
        }

        private void OnDestroy()
        {
            CleanupFlamethrowerEmitter();
        }

        private void SetupFlamethrowerEmitter()
        {
            CleanupFlamethrowerEmitter();

            if (_weapon == null || !_weapon.isFlamethrower || _muzzle == null)
            {
                return;
            }

            if (_weapon.flamethrowerEmitterPrefab)
            {
                _flamethrowerInstance = Instantiate(_weapon.flamethrowerEmitterPrefab);
            }
            else
            {
                _flamethrowerInstance = new GameObject("FlamethrowerEmitter");
            }

            _flamethrowerInstance.transform.SetParent(null);
            _flamethrowerInstance.transform.SetPositionAndRotation(_muzzle.position, _muzzle.rotation);
            _flamethrowerInstance.transform.localScale = Vector3.one;

            _flamethrowerEmitter = _flamethrowerInstance.GetComponent<FlamethrowerEmitter>();
            if (_flamethrowerEmitter == null)
            {
                _flamethrowerEmitter = _flamethrowerInstance.AddComponent<FlamethrowerEmitter>();
            }

            _flamethrowerEmitter.Initialize(_weapon, _muzzle, ResolveOwnerTag());
        }

        private void CleanupFlamethrowerEmitter()
        {
            if (_flamethrowerEmitter)
            {
                _flamethrowerEmitter.gameObject.SetActive(false);
            }

            if (_flamethrowerInstance)
            {
                Destroy(_flamethrowerInstance);
            }

            _flamethrowerEmitter = null;
            _flamethrowerInstance = null;
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
                SetGrenadeChargeProgress(0f);
                StopLoopingFeedback();
                return;
            }

            if (PauseMenuController.IsMenuOpen || Time.timeScale <= Mathf.Epsilon)
            {
                SetFireHeld(false);
                sustainedFireTime = 0f;
                StopLoopingFeedback();
                if (_flamethrowerEmitter)
                {
                    _flamethrowerEmitter.Tick(false, 0, 1f, 0f, 1f, ResolveOwnerTag());
                }
                return;
            }

            float deltaTime = Time.timeScale < 0.999f ? Time.unscaledDeltaTime : Time.deltaTime;

            if (_weapon.isFlamethrower)
            {
                HandleFlamethrower(deltaTime);
                return;
            }

            if (_isFireHeld && _weapon.isAuto)
            {
                sustainedFireTime += deltaTime;
                sustainedFireTime = Mathf.Clamp(sustainedFireTime, 0f, 1f);
            }
            else
            {
                sustainedFireTime -= deltaTime * 1.5f;
                sustainedFireTime = Mathf.Clamp(sustainedFireTime, 0f, 1f);
            }

            _fireTimer += deltaTime;

            float rpmMultiplier = _stats != null ? _stats.GetFireRateMultiplier() : 1f;
            if (UpgradeManager.I != null)
            {
                rpmMultiplier *= UpgradeManager.I.GetWeaponFireRateMultiplier(_weapon);
                rpmMultiplier = UpgradeManager.I.ClampFireRateMultiplier(rpmMultiplier);
            }

            float rpm = Mathf.Max(_weapon.rpm * rpmMultiplier, 0.01f);
            if (UpgradeManager.I != null)
            {
                rpm = UpgradeManager.I.ClampFireRateRpm(rpm);
            }

            float effectiveRpmMultiplier = rpm / Mathf.Max(0.01f, _weapon.rpm);
            float interval = 60f / rpm;

            float cooldownMultiplier = _stats != null ? _stats.GetFireCooldownMultiplier() : 1f;
            if (UpgradeManager.I != null)
            {
                cooldownMultiplier *= UpgradeManager.I.GetWeaponFireCooldownMultiplier(_weapon);
                cooldownMultiplier = UpgradeManager.I.ClampCooldownMultiplier(cooldownMultiplier);
            }
            interval *= cooldownMultiplier;

            if (!_weapon.isAuto && _weapon.fireCooldown > 0f)
            {
                interval = _weapon.fireCooldown / effectiveRpmMultiplier;
                interval *= cooldownMultiplier;
            }

            if (UpgradeManager.I != null)
            {
                interval = UpgradeManager.I.ClampCooldownSeconds(interval);
            }
            interval = Mathf.Max(0.01f, interval);

            if (_isGrenadeWeapon && _useGrenadeCharging)
            {
                HandleGrenadeCharging(interval, deltaTime);
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
            float accuracyMultiplier = GetAccuracyMultiplier();
            float adjustedBaseSpread = _weapon.baseSpread * accuracyMultiplier;
            float targetSpread = adjustedBaseSpread * (isMoving ? movementPenalty : 1f);
            _currentSpread = Mathf.Lerp(_currentSpread, targetSpread, deltaTime * _weapon.spreadRecoverySpeed);

            UpdateRecoil(deltaTime);
            UpdateLoopingFeedback();
        }

        private void HandleFlamethrower(float deltaTime)
        {
            if (_flamethrowerEmitter == null)
            {
                SetupFlamethrowerEmitter();
                if (_flamethrowerEmitter == null)
                {
                    return;
                }
            }

            GetDamageAndCritStats(out float damageMultiplier, out float critChance, out float critDamageMultiplier);
            int baseDamagePerSecond = Mathf.Max(1, _weapon.flamethrowerDamagePerSecond);

            if (_weapon.useFlamethrowerBurst)
            {
                UpdateFlamethrowerHeat(deltaTime);
            }
            else if (_flamethrowerHeat > 0.001f)
            {
                _flamethrowerHeat = 0f;
                _flamethrowerOverheated = false;
                SetGrenadeChargeProgress(0f);
            }

            float rangeMultiplier = UpgradeManager.I != null ? UpgradeManager.I.GetFlamethrowerRangeMultiplier(_weapon) : 1f;
            _flamethrowerEmitter.SetRangeMultiplier(rangeMultiplier);

            bool canFire = _isFireHeld && !_flamethrowerOverheated;

            _flamethrowerEmitter.Tick(
                canFire,
                baseDamagePerSecond,
                damageMultiplier,
                critChance,
                critDamageMultiplier,
                ResolveOwnerTag());

            _currentSpread = _weapon.baseSpread;
            _fireTimer = 0f;
            SetCooldownProgress(1f);
            UpdateRecoil(deltaTime);
        }

        private void UpdateFlamethrowerHeat(float deltaTime)
        {
            if (!_weapon || !_weapon.useFlamethrowerBurst)
            {
                return;
            }

            if (!_flamethrowerOverheated && _isFireHeld)
            {
                float duration = Mathf.Max(0.1f, _weapon.flamethrowerBurstDuration);
                _flamethrowerHeat += deltaTime / duration;
                if (_flamethrowerHeat >= 1f)
                {
                    _flamethrowerHeat = 1f;
                    _flamethrowerOverheated = true;
                }
            }
            else if (_flamethrowerHeat > 0f)
            {
                float cooldown = Mathf.Max(0.1f, _weapon.flamethrowerOverheatCooldown);
                if (UpgradeManager.I != null)
                {
                    cooldown *= UpgradeManager.I.GetFlamethrowerCooldownMultiplier(_weapon);
                }
                _flamethrowerHeat = Mathf.Max(0f, _flamethrowerHeat - deltaTime / cooldown);
                if (_flamethrowerOverheated && _flamethrowerHeat <= Mathf.Epsilon)
                {
                    _flamethrowerOverheated = false;
                }
            }

            SetGrenadeChargeProgress(_flamethrowerHeat);
        }

        private string ResolveOwnerTag()
        {
            return transform.root ? transform.root.tag : gameObject.tag;
        }

        #region Recoil & Shooting
        private void Shoot(float? grenadeSpeedOverride = null)
        {
            float accuracyMultiplier = GetAccuracyMultiplier();
            float spreadIncrease = _weapon.spreadIncreasePerShot * accuracyMultiplier;
            _currentSpread += spreadIncrease;

            bool isMoving = _playerBody && _playerBody.linearVelocity.magnitude > 0.1f;
            float movementPenalty = 1f;
            if (_stats != null)
            {
                movementPenalty = _stats.GetMovementAccuracyPenalty();
            }
            float adjustedBaseSpread = _weapon.baseSpread * accuracyMultiplier;
            float adjustedMaxSpread = _weapon.maxSpread * accuracyMultiplier;
            float maxSpread = adjustedMaxSpread * (isMoving ? movementPenalty : 1f);
            _currentSpread = Mathf.Clamp(_currentSpread, adjustedBaseSpread, maxSpread);

            float angleOffset = UnityEngine.Random.Range(-_currentSpread, _currentSpread);
            Quaternion spreadRotation = _muzzle.rotation * Quaternion.AngleAxis(angleOffset, Vector3.forward);

            SpawnEjectParticles();

            int extraProjectiles = UpgradeManager.I != null ? UpgradeManager.I.GetWeaponExtraProjectiles(_weapon) : 0;
            bool firesLikeShotgun = _weapon.isShotgun || _weapon.weaponClass == Weapon.WeaponClass.Shotgun;
            int pelletCount = firesLikeShotgun
                ? Mathf.Max(1, _weapon.pelletsPerShot)
                : 1;
            int totalProjectiles = Mathf.Max(1, pelletCount + extraProjectiles);
            int pierceCount = UpgradeManager.I != null ? UpgradeManager.I.GetWeaponPierceCount(_weapon) : 0;

            StartCoroutine(FireProjectilesRoutine(totalProjectiles, spreadRotation, pierceCount, grenadeSpeedOverride));

            PlayFireAudio();
            SpawnMuzzleFlash();

            if (_cameraShakeEnabled)
            {
                float shakeStrength = Mathf.Lerp(0.05f, maxSustainedShake, sustainedFireTime);
                CameraShake.Shake(shakeStrength, shakeStrength);
            }

            _currentRecoil = _weapon.recoilAmount;
            _recoilTimer = 0f;
            SetCooldownProgress(0f);
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

        private void SpawnStandardBullet(Quaternion spreadRotation, int pierceCount)
        {
            if (!_weapon.bulletPrefab)
            {
                return;
            }

            GameObject bulletInstance = PoolManager.Get(_weapon.bulletPrefab, _muzzle.position, spreadRotation);

            if (bulletInstance.TryGetComponent<Bullet>(out var bullet))
            {
                float damageMultiplier = GetFinalDamageMultiplier(out bool isCrit);
                bullet.SetDamage(Mathf.RoundToInt(_weapon.damage * damageMultiplier), isCrit);
                string ownerTag = transform.root ? transform.root.tag : gameObject.tag;
                bullet.SetOwner(ownerTag);
                float projectileSpeedMultiplier = GetProjectileSpeedMultiplier();
                bullet.SetSpeed(bullet.BaseSpeed * Mathf.Max(0.01f, projectileSpeedMultiplier));
                bullet.SetSourceWeapon(_weapon);
                bullet.SetKnockback(_weapon.knockbackStrength, _weapon.knockbackDuration);
                bullet.SetPierceCount(pierceCount);
            }
        }

        private IEnumerator FireProjectilesRoutine(int totalProjectiles, Quaternion baseRotation, int pierceCount, float? grenadeSpeedOverride)
        {
            if (_weapon == null || _muzzle == null)
            {
                yield break;
            }

            bool firesLikeShotgun = _weapon != null && (_weapon.isShotgun || _weapon.weaponClass == Weapon.WeaponClass.Shotgun);
            bool staggerShots = _isGrenadeWeapon || (_weapon != null && (_weapon.isSpecial || _weapon.weaponClass == Weapon.WeaponClass.Special) && !firesLikeShotgun);

            for (int i = 0; i < totalProjectiles; i++)
            {
                Quaternion shotRotation = i == 0
                    ? baseRotation
                    : baseRotation * Quaternion.AngleAxis(UnityEngine.Random.Range(-_currentSpread, _currentSpread), Vector3.forward);

                bool launchedGrenade = TryLaunchGrenade(shotRotation, grenadeSpeedOverride);
                if (!launchedGrenade)
                {
                    SpawnStandardBullet(shotRotation, pierceCount);
                }

                if (launchedGrenade || (_weapon != null && _weapon.bulletPrefab))
                {
                    NotifyRoundsFired(1);
                }

                if (staggerShots && i < totalProjectiles - 1 && extraProjectileDelay > 0f)
                {
                    yield return new WaitForSeconds(extraProjectileDelay);
                }
            }
        }

        private void NotifyRoundsFired(int count)
        {
            if (count <= 0)
            {
                return;
            }

            OnRoundsFired?.Invoke(this, count);
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

            _abilityController?.TryApplyShockwave(grenade);

            float damageMultiplier = GetFinalDamageMultiplier(out bool isCrit);
            Vector2 direction = spreadRotation * Vector3.right;
            string ownerTag = transform.root ? transform.root.tag : gameObject.tag;
            AudioMixerGroup mixer = _audioSource ? _audioSource.outputAudioMixerGroup : null;
            float spatialBlend = _audioSource ? _audioSource.spatialBlend : 0f;
            float volume = _audioSource ? _audioSource.volume : 1f;
            float pitch = _audioSource ? _audioSource.pitch : 1f;

            float projectileSpeedMultiplier = GetProjectileSpeedMultiplier();
            float baseLaunchSpeed = grenade.BaseLaunchSpeed;
            float finalLaunchSpeed = speedOverride.HasValue
                ? Mathf.Max(0.1f, speedOverride.Value * projectileSpeedMultiplier)
                : Mathf.Max(0.1f, baseLaunchSpeed * projectileSpeedMultiplier);

            grenade.Launch(direction, _weapon.damage, damageMultiplier, ownerTag, mixer, spatialBlend, volume, pitch, null, finalLaunchSpeed, null, _weapon, isCrit);
            return true;
        }

        private void PlayFireAudio()
        {
            if (ShouldUseLoopingAudio())
            {
                StartLoopingAudio();
                return;
            }

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

        private void UpdateRecoil(float deltaTime)
        {
            if (!_gunPivot)
            {
                return;
            }

            _gunPivot.localPosition = _baseLocalPosition;

            _recoilTimer += deltaTime * 10f;
            float kick = Mathf.Lerp(_currentRecoil, 0f, _recoilTimer);

            Vector3 recoilDirection = -_gunPivot.right * (kick * 0.1f);
            Transform pivotParent = _gunPivot.parent;
            Vector3 localRecoil = pivotParent ? pivotParent.InverseTransformDirection(recoilDirection) : recoilDirection;

            _gunPivot.localPosition = _baseLocalPosition + localRecoil;

            _currentRecoil = Mathf.Lerp(_currentRecoil, 0f, deltaTime * _weapon.recoilRecoverySpeed);
        }

        private void GetDamageAndCritStats(out float damageMultiplier, out float critChance, out float critDamageMultiplier)
        {
            damageMultiplier = _stats != null ? _stats.GetDamageMultiplier() : 1f;
            critChance = _stats != null ? _stats.GetCritChance() : 0f;
            critDamageMultiplier = _stats != null ? _stats.GetCritDamageMultiplier() : 1f;

            if (UpgradeManager.I != null)
            {
                damageMultiplier *= UpgradeManager.I.GetWeaponDamageMultiplier(_weapon);
                critChance += UpgradeManager.I.GetWeaponCritChance(_weapon);
                critDamageMultiplier *= UpgradeManager.I.GetWeaponCritDamageMultiplier(_weapon);
            }

            critChance = Mathf.Clamp01(critChance);
        }

        private float GetFinalDamageMultiplier(out bool isCrit)
        {
            GetDamageAndCritStats(out float damageMultiplier, out float critChance, out float critDamageMultiplier);

            bool didCrit = critChance > 0f && UnityEngine.Random.value < critChance;
            isCrit = didCrit;

            if (!didCrit)
            {
                return damageMultiplier;
            }

            return damageMultiplier * Mathf.Max(1f, critDamageMultiplier);
        }

        private float GetProjectileSpeedMultiplier()
        {
            float projectileSpeedMultiplier = _stats != null ? _stats.GetProjectileSpeedMultiplier() : 1f;
            if (UpgradeManager.I != null)
            {
                projectileSpeedMultiplier *= UpgradeManager.I.GetWeaponProjectileSpeedMultiplier(_weapon);
            }

            if (IsInSlowmo())
            {
                projectileSpeedMultiplier *= _slowmoProjectileSpeedMultiplier;
            }

            return projectileSpeedMultiplier;
        }

        private float GetAccuracyMultiplier()
        {
            return UpgradeManager.I != null ? UpgradeManager.I.GetWeaponAccuracyMultiplier(_weapon) : 1f;
        }

        private static bool IsInSlowmo()
        {
            return Time.timeScale > Mathf.Epsilon && Time.timeScale < 0.999f;
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

        private void HandleGrenadeCharging(float interval, float deltaTime)
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
                    ? deltaTime / grenadeChargeTime
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

        private bool ShouldUseLoopingAudio()
        {
            return _weapon != null && (_weapon.attackLoopSFX || _weapon.fireLoopSFX);
        }

        private bool ShouldUseLoopingVfx()
        {
            return _weapon != null && _weapon.loopingFireVfx;
        }

        private void UpdateLoopingFeedback()
        {
            bool shouldPlay = _isFireHeld && _weapon != null && _muzzle != null;

            if (shouldPlay)
            {
                if (ShouldUseLoopingAudio())
                {
                    StartLoopingAudio();
                }

                if (ShouldUseLoopingVfx())
                {
                    StartLoopingVfx();
                }
            }
            else
            {
                StopLoopingFeedback();
            }
        }

        private void StartLoopingAudio()
        {
            AudioMixerGroup mixer = _audioSource ? _audioSource.outputAudioMixerGroup : null;
            float spatialBlend = _audioSource ? _audioSource.spatialBlend : 0f;
            float volume = _audioSource ? _audioSource.volume : 1f;
            float pitch = _audioSource ? _audioSource.pitch : 1f;

            if (_weapon.attackLoopSFX)
            {
                AudioSource attackSource = GetOrCreateLoopSource(ref _attackLoopSource, "AttackLoopSource");
                ConfigureLoopSource(attackSource, mixer, spatialBlend, volume, pitch);
                PlayLoopIfNeeded(attackSource, _weapon.attackLoopSFX);
            }

            if (_weapon.fireLoopSFX)
            {
                AudioSource fireSource = GetOrCreateLoopSource(ref _fireLoopSource, "FireLoopSource");
                ConfigureLoopSource(fireSource, mixer, spatialBlend, volume, pitch);
                PlayLoopIfNeeded(fireSource, _weapon.fireLoopSFX);
            }
        }

        private void StopLoopingFeedback()
        {
            StopLoopingAudio();
            StopLoopingVfx();
        }

        private void StopLoopingAudio()
        {
            StopLoopSource(_attackLoopSource);
            StopLoopSource(_fireLoopSource);
        }

        private void StartLoopingVfx()
        {
            if (_activeLoopingVfx || _muzzle == null || _weapon.loopingFireVfx == null)
            {
                return;
            }

            Transform parent = _muzzle;
            Vector3 position = parent.position + parent.TransformVector(_weapon.loopingVfxOffset);
            Quaternion rotation = parent.rotation;

            GameObject instance = PoolManager.Get(_weapon.loopingFireVfx, position, rotation);
            if (instance)
            {
                instance.transform.SetParent(parent);
                instance.transform.localPosition = _weapon.loopingVfxOffset;
                instance.transform.localRotation = Quaternion.identity;

                if (instance.TryGetComponent<PooledParticleSystem>(out var pooled))
                {
                    pooled.OnTakenFromPool();
                }
                else
                {
                    pooled = instance.AddComponent<PooledParticleSystem>();
                    pooled.OnTakenFromPool();
                }
            }

            _activeLoopingVfx = instance;
        }

        private void StopLoopingVfx()
        {
            if (_activeLoopingVfx == null)
            {
                return;
            }

            if (_activeLoopingVfx.TryGetComponent<PoolToken>(out var token))
            {
                token.Release();
            }
            else
            {
                Destroy(_activeLoopingVfx);
            }

            _activeLoopingVfx = null;
        }

        private AudioSource GetOrCreateLoopSource(ref AudioSource source, string name)
        {
            if (source)
            {
                return source;
            }

            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            source.name = name;
            return source;
        }

        private static void ConfigureLoopSource(AudioSource source, AudioMixerGroup mixer, float spatialBlend, float volume, float pitch)
        {
            if (!source)
            {
                return;
            }

            source.outputAudioMixerGroup = mixer;
            source.spatialBlend = spatialBlend;
            source.volume = volume;
            source.pitch = pitch;
        }

        private static void StopLoopSource(AudioSource source)
        {
            if (!source)
            {
                return;
            }

            if (source.isPlaying)
            {
                source.Stop();
            }

            source.clip = null;
        }

        private static void PlayLoopIfNeeded(AudioSource source, AudioClip clip)
        {
            if (!source || !clip)
            {
                return;
            }

            if (source.clip == clip && source.isPlaying)
            {
                return;
            }

            source.clip = clip;
            source.Play();
        }
        #endregion Cooldown
    }
}
