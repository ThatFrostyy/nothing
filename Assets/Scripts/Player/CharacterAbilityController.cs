using System;
using System.Collections.Generic;
using UnityEngine;

namespace FF
{
    /// <summary>
    /// Handles character-specific active and passive abilities based on the
    /// current CharacterDefinition.AbilityId.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class CharacterAbilityController : MonoBehaviour
    {
        public enum AbilityType { None, Dash, Suppression, Sharpshooter, AntiTank }

        public event Action<AbilityType, float, bool> OnAbilityRechargeUpdated;

        [Header("General")]
        [SerializeField, Tooltip("If left empty we try to infer from the selected character.")]
        private string abilityIdOverride;

        [Header("Dash")]
        [SerializeField, Min(1)] private int baseDashCharges = 1;
        [SerializeField, Min(1f)] private float dashSpeedMultiplier = 2.25f;
        [SerializeField, Min(0.05f)] private float dashDuration = 0.35f;
        [SerializeField, Min(0.05f)] private float dashCooldown = 2.5f;
        [SerializeField] private AudioClip dashSfx;
        [SerializeField] private GameObject dashEffect;

        [Header("Suppression Aura")]
        [SerializeField, Min(0.5f)] private float suppressionRadius = 5.5f;
        [SerializeField, Range(0.05f, 1f)] private float suppressionSpeedMultiplier = 0.65f;
        [SerializeField, Min(0.25f)] private float suppressionDuration = 3.5f;
        [SerializeField, Min(0.05f)] private float suppressionPulseInterval = 0.75f;
        [SerializeField, Range(0.05f, 1f)] private float suppressionFireRateMultiplier = 0.8f;
        [SerializeField] private Color suppressionTextColor = new(0.85f, 0.58f, 1f);
        [SerializeField, Min(0.25f)] private float suppressionTextScale = 0.9f;

        [Header("Sharpshooter")]
        [SerializeField, Range(0f, 1f)] private float sharpshooterCritBonus = 0.35f;
        [SerializeField, Min(1f)] private float sharpshooterCritDamageMult = 1.5f;

        [Header("AT Shockwave")]
        [SerializeField, Min(0.1f)] private float shockwaveStunDuration = 1.5f;
        [SerializeField, Min(0f)] private float shockwaveForce = 9f;
        [SerializeField, Min(0.05f)] private float shockwaveKnockbackDuration = 0.35f;
        [SerializeField, Min(0.1f)] private float shockwaveRadius = 2.75f; // <--- new: explicit shockwave radius
        [SerializeField] private Color shockwaveTextColor = new(0.95f, 0.9f, 0.35f);
        [SerializeField, Min(0.25f)] private float shockwaveTextScale = 0.95f;

        private PlayerStats _stats;
        private Rigidbody2D _rigidbody;
        private WeaponManager _weaponManager;
        private AbilityType _activeAbility = AbilityType.None;
        private float _dashCooldownTimer;
        private float _dashTimer;
        private Vector2 _lastMoveInput;
        private bool _dashRequested;
        private int _dashCharges;
        private int _bonusDashCharges;
        private float _dashFireRateMultiplier = 1f;
        private float _dashFireRateDuration;
        private float _dashImpactDamageMultiplier;
        private float _dashImpactRadius;
        private float _dashImpactKnockbackStrength;
        private float _dashImpactKnockbackDuration;
        private float _suppressionTimer;
        private readonly Dictionary<EnemyStats, float> _suppressedEnemies = new();
        private readonly List<EnemyStats> _toRemove = new();
        private bool _sharpshooterApplied;
        private float _lastAbilityRechargeProgress = -1f;
        private AbilityType _lastAbilityType = AbilityType.None;
        private bool _lastAbilityRechargeVisible;

        public AbilityType ActiveAbility => _activeAbility;
        public float AbilityRechargeProgress => GetAbilityRechargeProgress(_activeAbility);
        public bool AbilityUsesRecharge => AbilityUsesCooldown(_activeAbility);

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _rigidbody = GetComponent<Rigidbody2D>();
            _weaponManager = GetComponentInChildren<WeaponManager>();
        }

        private void OnEnable()
        {
            CharacterSelectionState.OnSelectedChanged += HandleSelectionChanged;
            if (_weaponManager)
            {
                _weaponManager.OnWeaponEquipped += HandleWeaponEquipped;
            }
            InitializeAbility(CharacterSelectionState.SelectedCharacter);
        }

        private void OnDisable()
        {
            CharacterSelectionState.OnSelectedChanged -= HandleSelectionChanged;
            if (_weaponManager)
            {
                _weaponManager.OnWeaponEquipped -= HandleWeaponEquipped;
            }
        }

        private void Update()
        {
            switch (_activeAbility)
            {
                case AbilityType.Dash:
                    UpdateDash();
                    break;
                case AbilityType.Suppression:
                    UpdateSuppression();
                    break;
            }

            NotifyAbilityRechargeUpdated(false);
        }

        public void UpdateMoveInput(Vector2 moveInput)
        {
            _lastMoveInput = moveInput;
        }

        private void HandleSelectionChanged(CharacterLoadout selection)
        {
            InitializeAbility(selection.Character);
        }

        public void InitializeAbility(CharacterDefinition character)
        {
            AbilityType resolved = ResolveAbility(character);
            bool abilityChanged = resolved != _activeAbility;

            if (abilityChanged && _activeAbility == AbilityType.Sharpshooter)
            {
                RemoveSharpshooterBonuses();
            }

            _activeAbility = resolved;

            if (abilityChanged)
            {
                _dashCooldownTimer = 0f;
                _dashTimer = 0f;
                _dashRequested = false;
                _dashCharges = GetMaxDashCharges();
                _suppressionTimer = 0f;
                _suppressedEnemies.Clear();

                if (_activeAbility != AbilityType.Sharpshooter)
                {
                    _sharpshooterApplied = false;
                }
            }

            if (_activeAbility == AbilityType.Sharpshooter)
            {
                ApplySharpshooterBonuses();
            }

            NotifyAbilityRechargeUpdated(true);
        }

        private void HandleWeaponEquipped(Weapon weapon)
        {
            if (_activeAbility != AbilityType.Sharpshooter)
            {
                return;
            }

            if (!IsSharpshooterWeapon(weapon))
            {
                RemoveSharpshooterBonuses();
                return;
            }

            ApplySharpshooterBonuses();
        }

        private AbilityType ResolveAbility(CharacterDefinition character)
        {
            string id = !string.IsNullOrWhiteSpace(abilityIdOverride)
                ? abilityIdOverride
                : character?.AbilityId;

            if (string.IsNullOrWhiteSpace(id))
            {
                return AbilityType.None;
            }

            if (id.Equals("dash", StringComparison.OrdinalIgnoreCase)) return AbilityType.Dash;
            if (id.Equals("suppression", StringComparison.OrdinalIgnoreCase)
                || id.Equals("suppress", StringComparison.OrdinalIgnoreCase)
                || id.Equals("suppresion", StringComparison.OrdinalIgnoreCase))
                return AbilityType.Suppression;
            if (id.Equals("sharpshooter", StringComparison.OrdinalIgnoreCase)) return AbilityType.Sharpshooter;
            if (id.Equals("at", StringComparison.OrdinalIgnoreCase)) return AbilityType.AntiTank;

            return AbilityType.None;
        }

        private bool AbilityUsesCooldown(AbilityType abilityType)
        {
            return abilityType == AbilityType.Dash;
        }

        private float GetAbilityRechargeProgress(AbilityType abilityType)
        {
            if (!AbilityUsesCooldown(abilityType))
            {
                return 1f;
            }

            if (abilityType == AbilityType.Dash)
            {
                if (_dashCharges >= GetMaxDashCharges())
                {
                    return 1f;
                }

                if (dashCooldown <= 0.001f)
                {
                    return 1f;
                }

                return 1f - Mathf.Clamp01(_dashCooldownTimer / dashCooldown);
            }

            return 1f;
        }

        private void NotifyAbilityRechargeUpdated(bool forceNotify)
        {
            bool isVisible = AbilityUsesCooldown(_activeAbility);
            float progress = GetAbilityRechargeProgress(_activeAbility);

            if (!forceNotify
                && _lastAbilityType == _activeAbility
                && Mathf.Approximately(_lastAbilityRechargeProgress, progress)
                && _lastAbilityRechargeVisible == isVisible)
            {
                return;
            }

            _lastAbilityType = _activeAbility;
            _lastAbilityRechargeProgress = progress;
            _lastAbilityRechargeVisible = isVisible;
            OnAbilityRechargeUpdated?.Invoke(_activeAbility, progress, isVisible);
        }

        public bool TryApplyShockwave(GrenadeProjectile grenade)
        {
            if (_activeAbility != AbilityType.AntiTank || grenade == null)
            {
                return false;
            }

            grenade.ConfigureShockwave(
                transform,
                shockwaveStunDuration,
                shockwaveForce,
                shockwaveKnockbackDuration,
                shockwaveTextColor,
                shockwaveTextScale,
                shockwaveRadius); // pass explicit radius
            return true;
        }

        #region Dash
        private void UpdateDash()
        {
            float deltaTime = Time.deltaTime;
            if (_dashCooldownTimer > 0f)
            {
                _dashCooldownTimer = Mathf.Max(0f, _dashCooldownTimer - deltaTime);
                if (_dashCooldownTimer <= 0f && _dashCharges < GetMaxDashCharges())
                {
                    _dashCharges++;
                    if (_dashCharges < GetMaxDashCharges())
                    {
                        _dashCooldownTimer = dashCooldown;
                    }
                }
            }

            if (_dashTimer > 0f)
            {
                _dashTimer = Mathf.Max(0f, _dashTimer - deltaTime);
            }

            if (!CanDash())
            {
                _dashRequested = false;
                return;
            }

            if (_dashRequested)
            {
                TriggerDash();
                _dashRequested = false;
            }
        }

        private bool CanDash()
        {
            if (_dashCharges <= 0)
            {
                return false;
            }

            return _lastMoveInput.sqrMagnitude > 0.01f;
        }

        public void RequestDash()
        {
            if (_activeAbility != AbilityType.Dash)
            {
                return;
            }

            _dashRequested = true;
        }

        private int GetMaxDashCharges()
        {
            return Mathf.Max(1, baseDashCharges + _bonusDashCharges);
        }

        public void ConfigureDashRewards(
            int bonusCharges,
            float fireRateBonusMultiplier,
            float fireRateDuration,
            float impactDamageMultiplier,
            float impactRadius,
            float knockbackStrength,
            float knockbackDuration)
        {
            _bonusDashCharges = Mathf.Max(0, bonusCharges);
            _dashFireRateMultiplier = Mathf.Max(1f, fireRateBonusMultiplier);
            _dashFireRateDuration = Mathf.Max(0f, fireRateDuration);
            _dashImpactDamageMultiplier = Mathf.Max(0f, impactDamageMultiplier);
            _dashImpactRadius = Mathf.Max(0f, impactRadius);
            _dashImpactKnockbackStrength = Mathf.Max(0f, knockbackStrength);
            _dashImpactKnockbackDuration = Mathf.Max(0f, knockbackDuration);

            if (_activeAbility == AbilityType.Dash)
            {
                _dashCharges = GetMaxDashCharges();
                _dashCooldownTimer = 0f;
            }
        }

        private void TriggerDash()
        {
            _dashCharges = Mathf.Max(0, _dashCharges - 1);
            if (_dashCharges < GetMaxDashCharges() && _dashCooldownTimer <= 0f)
            {
                _dashCooldownTimer = dashCooldown;
            }
            _dashTimer = dashDuration;
            if (_stats != null)
            {
                _stats.ApplyTemporaryMultiplier(PlayerStats.StatType.MoveSpeed, dashSpeedMultiplier, dashDuration);
                if (_dashFireRateMultiplier > 1f && _dashFireRateDuration > 0f)
                {
                    _stats.ApplyTemporaryMultiplier(PlayerStats.StatType.FireRate, _dashFireRateMultiplier, _dashFireRateDuration);
                }
            }

            if (_rigidbody)
            {
                Vector2 direction = _lastMoveInput.sqrMagnitude > 0.0001f
                    ? _lastMoveInput.normalized
                    : _rigidbody.linearVelocity.normalized;

                if (direction.sqrMagnitude > 0.0001f)
                {
                    _rigidbody.linearVelocity += direction * (_stats.GetMoveSpeed() * (dashSpeedMultiplier - 1f));
                }
            }

            PlayDashFeedback();
            TriggerDashImpactBlast();
        }
        #endregion Dash

        private void PlayDashFeedback()
        {
            if (dashEffect)
            {
                PoolManager.Get(dashEffect, transform.position, Quaternion.identity);
            }

            if (dashSfx)
            {
                AudioPlaybackPool.PlayOneShot(dashSfx, transform.position);
            }
        }

        private void TriggerDashImpactBlast()
        {
            if (_dashImpactDamageMultiplier <= 0f || _dashImpactRadius <= 0f)
            {
                return;
            }

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, _dashImpactRadius);
            if (hits == null || hits.Length == 0)
            {
                return;
            }

            int baseDamage = _stats != null ? _stats.GetDamageInt() : 0;
            int impactDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * _dashImpactDamageMultiplier));

            foreach (Collider2D hit in hits)
            {
                if (!hit)
                {
                    continue;
                }

                if (hit.CompareTag("Player"))
                {
                    continue;
                }

                Health health = hit.GetComponentInParent<Health>();
                if (health == null || (health.transform.root && health.transform.root.CompareTag("Player")))
                {
                    continue;
                }

                health.Damage(impactDamage);

                Enemy enemy = hit.GetComponentInParent<Enemy>();
                if (enemy != null && _dashImpactKnockbackStrength > 0f)
                {
                    Vector2 direction = (enemy.transform.position - transform.position).normalized;
                    enemy.ApplyKnockback(direction * _dashImpactKnockbackStrength, _dashImpactKnockbackDuration);
                }
            }
        }

        #region Suppression
        private void UpdateSuppression()
        {
            float deltaTime = Time.deltaTime;
            _suppressionTimer = Mathf.Max(0f, _suppressionTimer - deltaTime);
            CullExpiredSuppressions();

            if (_suppressionTimer > 0f)
            {
                return;
            }

            PulseSuppression();
            _suppressionTimer = suppressionPulseInterval;
        }

        private void PulseSuppression()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, suppressionRadius);
            if (hits == null || hits.Length == 0)
            {
                return;
            }

            float now = Time.time;
            foreach (Collider2D hit in hits)
            {
                if (!hit)
                {
                    continue;
                }

                EnemyStats stats = hit.GetComponentInParent<EnemyStats>();
                if (stats == null)
                {
                    continue;
                }

                stats.ApplyTemporaryMultiplier(EnemyStats.StatType.MoveSpeed, suppressionSpeedMultiplier, suppressionDuration, true);
                stats.ApplyTemporaryMultiplier(EnemyStats.StatType.FireRate, suppressionFireRateMultiplier, suppressionDuration, true);

                bool isNewOrRefreshed = !_suppressedEnemies.TryGetValue(stats, out float expiry) || expiry <= now;
                _suppressedEnemies[stats] = now + suppressionDuration;

                if (isNewOrRefreshed)
                {
                    DamageNumberManager.ShowText(stats.transform.position, "Suppressed", suppressionTextColor, suppressionTextScale);
                }
            }
        }

        private void CullExpiredSuppressions()
        {
            if (_suppressedEnemies.Count == 0)
            {
                return;
            }

            float now = Time.time;
            _toRemove.Clear();

            foreach (var pair in _suppressedEnemies)
            {
                if (pair.Value <= now)
                {
                    _toRemove.Add(pair.Key);
                }
            }

            if (_toRemove.Count > 0)
            {
                for (int i = 0; i < _toRemove.Count; i++)
                {
                    _suppressedEnemies.Remove(_toRemove[i]);
                }
            }
        }
        #endregion Suppression

        #region Sharpshooter
        private void ApplySharpshooterBonuses()
        {
            if (_stats == null)
            {
                return;
            }

            if (!IsSharpshooterWeapon(_weaponManager ? _weaponManager.CurrentWeapon : null))
            {
                RemoveSharpshooterBonuses();
                return;
            }

            if (_sharpshooterApplied)
            {
                return;
            }

            _stats.CritChance = Mathf.Clamp01(_stats.CritChance + sharpshooterCritBonus);
            _stats.CritDamageMult = Mathf.Max(1f, _stats.CritDamageMult * sharpshooterCritDamageMult);
            _sharpshooterApplied = true;
        }

        private void RemoveSharpshooterBonuses()
        {
            if (!_sharpshooterApplied || _stats == null)
            {
                return;
            }

            _stats.CritChance = Mathf.Clamp01(_stats.CritChance - sharpshooterCritBonus);
            _stats.CritDamageMult = Mathf.Max(1f, _stats.CritDamageMult / Mathf.Max(0.0001f, sharpshooterCritDamageMult));
            _sharpshooterApplied = false;
        }

        private bool IsSharpshooterWeapon(Weapon weapon)
        {
            if (weapon == null)
            {
                return false;
            }

            return weapon.weaponClass == Weapon.WeaponClass.SemiRifle;
        }
        #endregion Sharpshooter
    }
}
