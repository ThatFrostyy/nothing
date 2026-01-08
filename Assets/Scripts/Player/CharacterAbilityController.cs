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
        [SerializeField, Min(1f)] private float dashSpeedMultiplier = 2.25f;
        [SerializeField, Min(0.05f)] private float dashDuration = 0.35f;
        [SerializeField, Min(0.05f)] private float dashCooldown = 2.5f;
        [SerializeField, Min(1)] private int dashCharges = 1;
        [SerializeField] private AudioClip dashSfx;
        [SerializeField] private GameObject dashEffect;
        [SerializeField] private LayerMask dashImpactLayers = ~0;

        [Header("Suppression Aura")]
        [SerializeField, Min(0.5f)] private float suppressionRadius = 5.5f;
        [SerializeField, Range(0.05f, 1f)] private float suppressionSpeedMultiplier = 0.65f;
        [SerializeField, Min(0.25f)] private float suppressionDuration = 3.5f;
        [SerializeField, Min(0.05f)] private float suppressionPulseInterval = 0.75f;
        [SerializeField, Range(0.05f, 1f)] private float suppressionFireRateMultiplier = 0.8f;
        [SerializeField] private Color suppressionTextColor = new(0.85f, 0.58f, 1f);
        [SerializeField, Min(0.25f)] private float suppressionTextScale = 0.9f;
        [SerializeField] private Color suppressionPanicTextColor = new(1f, 0.2f, 0.2f);
        [SerializeField, Min(0.25f)] private float suppressionPanicTextScale = 0.9f;

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
        private int _dashChargeBonus;
        private int _currentDashCharges;
        private Vector2 _lastMoveInput;
        private bool _dashRequested;
        private float _suppressionTimer;
        private readonly Dictionary<EnemyStats, float> _suppressedEnemies = new();
        private readonly List<EnemyStats> _toRemove = new();
        private bool _sharpshooterApplied;
        private float _lastAbilityRechargeProgress = -1f;
        private AbilityType _lastAbilityType = AbilityType.None;
        private bool _lastAbilityRechargeVisible;
        private float _dashFireRateBonus;
        private float _dashFireRateDuration;
        private float _dashImpactDamageBonus;
        private float _dashImpactRadius;
        private float _dashImpactForce;
        private float _dashImpactKnockbackDuration;
        private readonly List<Enemy> _dashImpactTargets = new();
        private readonly Collider2D[] _dashImpactHits = new Collider2D[32];
        private float _suppressionDamageReductionBonus;
        private float _suppressionExtraSlowBonus;
        private float _suppressionRadiusBonus;
        private float _suppressionPanicChance;
        private float _suppressionPanicDuration;

        public AbilityType ActiveAbility => _activeAbility;
        public float AbilityRechargeProgress => GetAbilityRechargeProgress(_activeAbility);
        public bool AbilityUsesRecharge => AbilityUsesCooldown(_activeAbility);

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _rigidbody = GetComponent<Rigidbody2D>();
            _weaponManager = GetComponentInChildren<WeaponManager>();
            _currentDashCharges = GetMaxDashCharges();
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
                _currentDashCharges = GetMaxDashCharges();
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

        public void ConfigureDashChargeBonus(int bonusCharges)
        {
            _dashChargeBonus = Mathf.Max(0, bonusCharges);
            _currentDashCharges = Mathf.Min(_currentDashCharges, GetMaxDashCharges());
            if (_activeAbility == AbilityType.Dash)
            {
                _currentDashCharges = GetMaxDashCharges();
                _dashCooldownTimer = 0f;
            }

            NotifyAbilityRechargeUpdated(true);
        }

        public void ConfigureDashFireRateBonus(float bonusPercent, float durationSeconds)
        {
            _dashFireRateBonus = Mathf.Max(0f, bonusPercent);
            _dashFireRateDuration = Mathf.Max(0f, durationSeconds);
        }

        public void ConfigureDashImpactBlast(float damagePercent, float radius, float force, float knockbackDuration)
        {
            _dashImpactDamageBonus = Mathf.Max(0f, damagePercent);
            _dashImpactRadius = Mathf.Max(0f, radius);
            _dashImpactForce = Mathf.Max(0f, force);
            _dashImpactKnockbackDuration = Mathf.Max(0f, knockbackDuration);
        }

        public void ConfigureSuppressionBonuses(
            float damageReductionPercent,
            float extraSlowPercent,
            float radiusPercent,
            float panicChance,
            float panicDurationSeconds)
        {
            _suppressionDamageReductionBonus = Mathf.Clamp01(damageReductionPercent);
            _suppressionExtraSlowBonus = Mathf.Clamp01(extraSlowPercent);
            _suppressionRadiusBonus = Mathf.Max(0f, radiusPercent);
            _suppressionPanicChance = Mathf.Clamp01(panicChance);
            _suppressionPanicDuration = Mathf.Max(0f, panicDurationSeconds);
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
                : character != null ? character.AbilityId : null;

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
                if (dashCooldown <= 0.001f)
                {
                    return _currentDashCharges > 0 ? 1f : 0f;
                }

                if (_currentDashCharges >= GetMaxDashCharges())
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
            if (_currentDashCharges < GetMaxDashCharges() && _dashCooldownTimer > 0f)
            {
                _dashCooldownTimer = Mathf.Max(0f, _dashCooldownTimer - deltaTime);
            }

            if (_currentDashCharges < GetMaxDashCharges() && dashCooldown <= 0.001f)
            {
                _currentDashCharges = GetMaxDashCharges();
                _dashCooldownTimer = 0f;
            }

            if (_currentDashCharges < GetMaxDashCharges() && _dashCooldownTimer <= 0f && dashCooldown > 0.001f)
            {
                _currentDashCharges++;
                if (_currentDashCharges < GetMaxDashCharges())
                {
                    _dashCooldownTimer = dashCooldown;
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
            if (_currentDashCharges <= 0)
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

        private void TriggerDash()
        {
            _currentDashCharges = Mathf.Max(0, _currentDashCharges - 1);
            if (_currentDashCharges < GetMaxDashCharges() && _dashCooldownTimer <= 0f)
            {
                _dashCooldownTimer = dashCooldown;
            }

            _dashTimer = dashDuration;
            if (_stats != null)
            {
                _stats.ApplyTemporaryMultiplier(PlayerStats.StatType.MoveSpeed, dashSpeedMultiplier, dashDuration);
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

            ApplyDashCombatBonuses();
            PlayDashFeedback();
        }

        private int GetMaxDashCharges()
        {
            return Mathf.Max(1, dashCharges + _dashChargeBonus);
        }

        private void ApplyDashCombatBonuses()
        {
            if (_stats != null && _dashFireRateBonus > 0f && _dashFireRateDuration > 0f)
            {
                _stats.ApplyTemporaryMultiplier(PlayerStats.StatType.FireRate, 1f + _dashFireRateBonus, _dashFireRateDuration);
            }

            if (_dashImpactRadius <= 0f || (_dashImpactDamageBonus <= 0f && _dashImpactForce <= 0f))
            {
                return;
            }

            int hits = Physics2D.OverlapCircleNonAlloc(transform.position, _dashImpactRadius, _dashImpactHits, dashImpactLayers);
            if (hits <= 0)
            {
                return;
            }

            int damage = _stats != null ? Mathf.RoundToInt(_stats.GetDamageInt() * _dashImpactDamageBonus) : 0;
            _dashImpactTargets.Clear();
            for (int i = 0; i < hits; i++)
            {
                Collider2D hit = _dashImpactHits[i];
                if (!hit)
                {
                    continue;
                }

                Enemy enemy = hit.GetComponentInParent<Enemy>();
                if (!enemy)
                {
                    continue;
                }

                if (_dashImpactTargets.Contains(enemy))
                {
                    continue;
                }

                _dashImpactTargets.Add(enemy);

                if (damage > 0 && enemy.TryGetComponent(out Health health))
                {
                    health.Damage(damage);
                }

                if (_dashImpactForce > 0f)
                {
                    Vector2 direction = (enemy.transform.position - transform.position);
                    if (direction.sqrMagnitude < 0.0001f)
                    {
                        direction = UnityEngine.Random.insideUnitCircle;
                    }

                    enemy.ApplyKnockback(direction.normalized * _dashImpactForce, _dashImpactKnockbackDuration);
                }
            }
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
            float radiusMultiplier = 1f + Mathf.Max(0f, _suppressionRadiusBonus);
            float effectiveRadius = Mathf.Max(0.1f, suppressionRadius * radiusMultiplier);
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, effectiveRadius);
            if (hits == null || hits.Length == 0)
            {
                return;
            }

            float now = Time.time;
            float extraSlowMultiplier = _suppressionExtraSlowBonus > 0f
                ? Mathf.Clamp(1f - _suppressionExtraSlowBonus, 0.05f, 1f)
                : 1f;
            float effectiveSlowMultiplier = Mathf.Clamp(suppressionSpeedMultiplier * extraSlowMultiplier, 0.05f, 1f);
            float damageReductionMultiplier = _suppressionDamageReductionBonus > 0f
                ? Mathf.Clamp(1f - _suppressionDamageReductionBonus, 0.05f, 1f)
                : 1f;
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

                stats.ApplyTemporaryMultiplier(EnemyStats.StatType.MoveSpeed, effectiveSlowMultiplier, suppressionDuration, true);
                stats.ApplyTemporaryMultiplier(EnemyStats.StatType.FireRate, suppressionFireRateMultiplier, suppressionDuration, true);
                if (_suppressionDamageReductionBonus > 0f)
                {
                    stats.ApplyTemporaryMultiplier(EnemyStats.StatType.Damage, damageReductionMultiplier, suppressionDuration, true);
                }

                bool isNewOrRefreshed = !_suppressedEnemies.TryGetValue(stats, out float expiry) || expiry <= now;
                _suppressedEnemies[stats] = now + suppressionDuration;

                if (isNewOrRefreshed)
                {
                    DamageNumberManager.ShowText(stats.transform.position, "Suppressed", suppressionTextColor, suppressionTextScale);
                    TryApplyPanic(stats);
                }
            }
        }

        private void TryApplyPanic(EnemyStats stats)
        {
            if (_suppressionPanicChance <= 0f || _suppressionPanicDuration <= 0f)
            {
                return;
            }

            if (UnityEngine.Random.value > _suppressionPanicChance)
            {
                return;
            }

            Enemy enemy = stats ? stats.GetComponentInParent<Enemy>() : null;
            if (!enemy)
            {
                return;
            }

            enemy.ApplyStun(_suppressionPanicDuration);
            DamageNumberManager.ShowText(stats.transform.position, "PANIC", suppressionPanicTextColor, suppressionPanicTextScale);
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
