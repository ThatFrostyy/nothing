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
        private enum AbilityType { None, Dash, Suppression, Sharpshooter, AntiTank }

        [Header("General")]
        [SerializeField, Tooltip("If left empty we try to infer from the selected character.")]
        private string abilityIdOverride;

        [Header("Dash")]
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
        private float _suppressionTimer;
        private readonly Dictionary<EnemyStats, float> _suppressedEnemies = new();
        private readonly List<EnemyStats> _toRemove = new();
        private bool _sharpshooterApplied;

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
            if (_dashCooldownTimer > 0f)
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
            _dashCooldownTimer = dashCooldown;
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

            PlayDashFeedback();
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
