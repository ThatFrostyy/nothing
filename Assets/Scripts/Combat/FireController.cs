using System;
using UnityEngine;

namespace FF
{
    public class FireController : MonoBehaviour
    {
        [Header("Grenade Charging")]
        [SerializeField, Min(0.1f)] private float grenadeMinThrowSpeed = 8f;
        [SerializeField, Min(0.1f)] private float grenadeMaxThrowSpeed = 22f;
        [SerializeField, Min(0.05f)] private float grenadeChargeTime = 1.1f;
        [SerializeField] private AnimationCurve grenadeChargeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private Weapon _weapon;
        private bool _isGrenadeWeapon;
        private float _fireTimer;
        private bool _isFireHeld;
        private bool _pendingFirePress;
        private bool _isChargingGrenade;
        private float _currentCooldownProgress = 1f;
        private float _currentChargeProgress;

        public event Action<float?> OnShot;
        public event Action<float> OnCooldownChanged;
        public event Action<float> OnGrenadeChargeChanged;

        public float CooldownProgress => _currentCooldownProgress;
        public float GrenadeChargeProgress => _currentChargeProgress;

        public void SetWeapon(Weapon weapon, bool isGrenadeWeapon)
        {
            _weapon = weapon;
            _isGrenadeWeapon = isGrenadeWeapon;
            _fireTimer = 0f;
            _pendingFirePress = false;
            _isChargingGrenade = false;
            SetCooldownProgress(weapon ? 1f : 0f);
            SetGrenadeChargeProgress(0f);
        }

        public void SetFireHeld(bool isHeld)
        {
            _isFireHeld = isHeld;

            if (!isHeld)
            {
                _pendingFirePress = false;
            }
        }

        public void QueueFirePress()
        {
            _pendingFirePress = true;
        }

        public void Tick(Weapon weapon, float rpmMultiplier, float cooldownMultiplier, float deltaTime)
        {
            if (weapon == null)
            {
                SetCooldownProgress(1f);
                SetGrenadeChargeProgress(0f);
                return;
            }

            float rpm = Mathf.Max(weapon.rpm * rpmMultiplier, 0.01f);
            float interval = weapon.isAuto
                ? 60f / rpm
                : (weapon.fireCooldown > 0f ? weapon.fireCooldown / rpmMultiplier : 60f / rpm);

            interval *= Mathf.Max(0.1f, cooldownMultiplier);

            _fireTimer += deltaTime;

            if (_isGrenadeWeapon)
            {
                HandleGrenadeCharging(interval, deltaTime);
            }
            else
            {
                HandleStandardFiring(interval);
            }
        }

        private void HandleStandardFiring(float interval)
        {
            if (_fireTimer >= interval)
            {
                if (!_weapon.isAuto && _pendingFirePress)
                {
                    _fireTimer = 0f;
                    _pendingFirePress = false;
                    OnShot?.Invoke(null);
                }

                if (_weapon.isAuto && _isFireHeld)
                {
                    _fireTimer = 0f;
                    OnShot?.Invoke(null);
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
                _isChargingGrenade = false;
                OnShot?.Invoke(throwSpeed);
                SetGrenadeChargeProgress(0f);
            }
            else
            {
                SetGrenadeChargeProgress(0f);
            }
        }

        private void SetGrenadeChargeProgress(float progress)
        {
            float clamped = Mathf.Clamp01(progress);
            _currentChargeProgress = clamped;
            OnGrenadeChargeChanged?.Invoke(clamped);
        }

        private void SetCooldownProgress(float value)
        {
            value = Mathf.Clamp01(value);

            if (Mathf.Approximately(_currentCooldownProgress, value))
            {
                return;
            }

            _currentCooldownProgress = value;
            OnCooldownChanged?.Invoke(_currentCooldownProgress);
        }
    }
}
