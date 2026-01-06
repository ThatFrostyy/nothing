using System.Collections;
using UnityEngine;

namespace FF
{
    public class PlayerCombatEffectController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WeaponManager weaponManager;
        [SerializeField] private AutoShooter playerShooter;
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private Health playerHealth;

        [Header("Secondary Weapon Synergy")]
        [SerializeField, Min(0f)] private float synergyMinInterval = 4f;
        [SerializeField, Min(0f)] private float synergyMaxInterval = 8f;
        [SerializeField, Range(1f, 2f)] private float synergyFireRateMultiplier = 1.1f;
        [SerializeField, Min(0f)] private float synergyFireRateDuration = 4f;
        [SerializeField, Range(0.05f, 1f)] private float synergyDamageResistanceMultiplier = 0.75f;
        [SerializeField, Min(0f)] private float synergyDamageResistanceDuration = 2f;
        [SerializeField, Min(0f)] private float synergyOrbVacuumDuration = 1f;
        [SerializeField, Min(0f)] private float synergyOrbVacuumRadiusMultiplier = 4f;
        [SerializeField, Min(0f)] private float synergyOrbVacuumSpeedMultiplier = 4f;
        [SerializeField] private Color synergyTextColor = new(0.45f, 1f, 0.6f);
        [SerializeField, Min(0.1f)] private float synergyTextScale = 0.9f;
        [SerializeField] private string synergyFireRateText = "Synergy: +10% Fire Rate";
        [SerializeField] private string synergyDamageResistText = "Synergy: Damage Resist";
        [SerializeField] private string synergyOrbVacuumText = "Synergy: Orb Vacuum";

        [Header("MG Suppression")]
        [SerializeField, Min(0.5f)] private float mgSuppressionRadius = 6f;
        [SerializeField, Min(1)] private int mgSuppressionThreshold = 20;
        [SerializeField, Min(0f)] private float mgSuppressionDecay = 12f;
        [SerializeField, Range(0.05f, 1f)] private float mgSuppressionMoveSpeedMultiplier = 0.7f;
        [SerializeField, Range(0.05f, 1f)] private float mgSuppressionFireRateMultiplier = 0.8f;
        [SerializeField, Min(0.25f)] private float mgSuppressionDuration = 2.5f;
        [SerializeField] private Color mgSuppressionTextColor = new(1f, 0.3f, 0.2f);
        [SerializeField, Min(0.25f)] private float mgSuppressionTextScale = 0.9f;

        [Header("Progression Short Range Damage")]
        [SerializeField, Min(0.1f)] private float progressionShortRangeInterval = 1f;
        [SerializeField] private LayerMask progressionShortRangeLayers = ~0;

        private bool _usingSecondary;
        private float _nextSynergyTime;
        private bool _slot0UsedSinceSynergy;
        private bool _slot1UsedSinceSynergy;
        private float _suppressionMeter;
        private float _suppressedUntil;
        private Coroutine _orbVacuumRoutine;
        private float _progressionShortRangeDamageBonus;
        private float _progressionShortRangeRadius;
        private float _progressionShortRangeTimer;
        private readonly Collider2D[] _progressionShortRangeHits = new Collider2D[32];
        private readonly System.Collections.Generic.List<Enemy> _progressionShortRangeTargets = new();
        private float _progressionKillMoveSpeedBonus;
        private float _progressionKillMoveSpeedDuration;
        private float _progressionSustainedDamageBonus;
        private float _progressionSustainedDamageDelay;
        private float _progressionSustainedFireTimer;
        private bool _progressionSustainedFireActive;
        private float _progressionMgSustainedFireRateBonus;
        private float _progressionMgSustainedFireRateDelay;
        private float _progressionMgSustainedFireTimer;
        private bool _progressionMgSustainedFireActive;
        private float _progressionRifleDamageBonus;
        private float _progressionRifleFireRateBonus;
        private float _progressionRifleProjectileSpeedBonus;
        private float _progressionRifleMoveDamageBonus;
        private float _progressionRifleMoveSpeedBonus;
        private float _progressionRevivePercent;
        private bool _progressionReviveAvailable;
        private float _progressionExplosionDamageBonus;
        private float _progressionExplosionRadiusBonus;
        private float _progressionExplosionResistanceBonus;
        private float _progressionExplosionBossDamageBonus;
        private float _progressionExplosionKillChance;
        private float _progressionExplosionKnockbackBonus;
        private float _progressionExplosionHitDamageBonus;
        private float _progressionExplosionHitDamageDuration;
        private float _progressionExplosionPostDamageReductionBonus;
        private float _progressionExplosionPostDamageReductionDuration;
        private float _progressionStationaryDamageReduction;
        private float _progressionStationaryDuration;
        private float _progressionStationaryTimer;
        private bool _progressionStationaryActive;
        private Rigidbody2D _playerBody;

        private void Awake()
        {
            weaponManager = weaponManager ? weaponManager : GetComponentInChildren<WeaponManager>();
            playerShooter = playerShooter ? playerShooter : weaponManager != null ? weaponManager.Shooter : null;
            playerStats = playerStats ? playerStats : GetComponent<PlayerStats>();
            playerHealth = playerHealth ? playerHealth : GetComponent<Health>();
            _playerBody = GetComponent<Rigidbody2D>();
            if (!playerShooter)
            {
                playerShooter = GetComponentInChildren<AutoShooter>();
            }
        }

        private void OnEnable()
        {
            AutoShooter.OnRoundsFired += HandleRoundsFired;
            Enemy.OnAnyEnemyKilledByWeapon += HandleEnemyKilledByWeapon;
            if (weaponManager)
            {
                weaponManager.OnWeaponEquipped += HandleWeaponEquipped;
            }

            if (playerHealth != null)
            {
                playerHealth.OnBeforeDeath += HandleBeforeDeath;
            }

            UpdateSecondaryUsageState(true);
        }

        private void OnDisable()
        {
            AutoShooter.OnRoundsFired -= HandleRoundsFired;
            Enemy.OnAnyEnemyKilledByWeapon -= HandleEnemyKilledByWeapon;
            if (weaponManager)
            {
                weaponManager.OnWeaponEquipped -= HandleWeaponEquipped;
            }

            if (playerHealth != null)
            {
                playerHealth.OnBeforeDeath -= HandleBeforeDeath;
            }

            if (_orbVacuumRoutine != null)
            {
                StopCoroutine(_orbVacuumRoutine);
                _orbVacuumRoutine = null;
            }

            XPOrb.SetGlobalAttractionMultipliers(1f, 1f);
        }

        private void Update()
        {
            UpdateSecondaryUsageState(false);
            UpdateSuppressionMeter(Time.deltaTime);
            UpdateProgressionShortRangeDamage(Time.deltaTime);
            UpdateProgressionBonuses(Time.deltaTime);
        }

        public void ConfigureProgressionShortRangeDamage(float damagePercent, float radius)
        {
            _progressionShortRangeDamageBonus = Mathf.Max(0f, damagePercent);
            _progressionShortRangeRadius = Mathf.Max(0f, radius);
            _progressionShortRangeTimer = 0f;
        }

        public void ConfigureProgressionKillMoveSpeed(float moveSpeedPercent, float duration)
        {
            _progressionKillMoveSpeedBonus = Mathf.Max(0f, moveSpeedPercent);
            _progressionKillMoveSpeedDuration = Mathf.Max(0f, duration);
        }

        public void ConfigureProgressionSustainedFireDamage(float damagePercent, float delaySeconds)
        {
            _progressionSustainedDamageBonus = Mathf.Max(0f, damagePercent);
            _progressionSustainedDamageDelay = Mathf.Max(0f, delaySeconds);
            _progressionSustainedFireTimer = 0f;
            _progressionSustainedFireActive = false;
        }

        public void ConfigureProgressionMgSustainedFireRate(float fireRatePercent, float delaySeconds)
        {
            _progressionMgSustainedFireRateBonus = Mathf.Max(0f, fireRatePercent);
            _progressionMgSustainedFireRateDelay = Mathf.Max(0f, delaySeconds);
            _progressionMgSustainedFireTimer = 0f;
            _progressionMgSustainedFireActive = false;
        }

        public void ConfigureProgressionRifleBonuses(float damagePercent, float fireRatePercent, float projectileSpeedPercent)
        {
            _progressionRifleDamageBonus = Mathf.Max(0f, damagePercent);
            _progressionRifleFireRateBonus = Mathf.Max(0f, fireRatePercent);
            _progressionRifleProjectileSpeedBonus = Mathf.Max(0f, projectileSpeedPercent);
        }

        public void ConfigureProgressionRifleMoveBonuses(float damagePercent, float moveSpeedPercent)
        {
            _progressionRifleMoveDamageBonus = Mathf.Max(0f, damagePercent);
            _progressionRifleMoveSpeedBonus = Mathf.Max(0f, moveSpeedPercent);
        }

        public void ConfigureProgressionRevive(float revivePercent)
        {
            _progressionRevivePercent = Mathf.Clamp01(revivePercent);
            _progressionReviveAvailable = _progressionRevivePercent > 0f;
        }

        public void ConfigureProgressionExplosionBonuses(
            float damagePercent,
            float radiusPercent,
            float resistancePercent,
            float bossDamagePercent,
            float killExplosionChance,
            float knockbackPercent,
            float hitDamagePercent,
            float hitDamageDuration,
            float postExplosionDamageReductionPercent,
            float postExplosionDamageReductionDuration)
        {
            _progressionExplosionDamageBonus = Mathf.Max(0f, damagePercent);
            _progressionExplosionRadiusBonus = Mathf.Max(0f, radiusPercent);
            _progressionExplosionResistanceBonus = Mathf.Max(0f, resistancePercent);
            _progressionExplosionBossDamageBonus = Mathf.Max(0f, bossDamagePercent);
            _progressionExplosionKillChance = Mathf.Clamp01(killExplosionChance);
            _progressionExplosionKnockbackBonus = Mathf.Max(0f, knockbackPercent);
            _progressionExplosionHitDamageBonus = Mathf.Max(0f, hitDamagePercent);
            _progressionExplosionHitDamageDuration = Mathf.Max(0f, hitDamageDuration);
            _progressionExplosionPostDamageReductionBonus = Mathf.Max(0f, postExplosionDamageReductionPercent);
            _progressionExplosionPostDamageReductionDuration = Mathf.Max(0f, postExplosionDamageReductionDuration);
        }

        public void ConfigureProgressionStandingStillDamageReduction(float damageReductionPercent, float durationSeconds)
        {
            _progressionStationaryDamageReduction = Mathf.Clamp01(damageReductionPercent);
            _progressionStationaryDuration = Mathf.Max(0f, durationSeconds);
            _progressionStationaryTimer = 0f;
            _progressionStationaryActive = false;
            if (playerHealth != null)
            {
                playerHealth.SetConditionalDamageMultiplier(1f);
            }
        }

        public void ApplyProgressionExplosionConfig(GrenadeProjectile grenade)
        {
            if (!grenade)
            {
                return;
            }

            grenade.ConfigureProgressionExplosion(
                _progressionExplosionDamageBonus,
                _progressionExplosionRadiusBonus,
                _progressionExplosionBossDamageBonus,
                _progressionExplosionKnockbackBonus,
                _progressionExplosionKillChance,
                _progressionExplosionHitDamageBonus,
                _progressionExplosionHitDamageDuration,
                this);
        }

        public float GetExplosionResistanceMultiplier()
        {
            float multiplier = 1f - Mathf.Clamp01(_progressionExplosionResistanceBonus);
            return Mathf.Clamp(multiplier, 0.05f, 1f);
        }

        public void ApplyProgressionExplosionDamageReduction()
        {
            if (_progressionExplosionPostDamageReductionBonus <= 0f
                || _progressionExplosionPostDamageReductionDuration <= 0f
                || playerHealth == null)
            {
                return;
            }

            float multiplier = 1f - Mathf.Clamp01(_progressionExplosionPostDamageReductionBonus);
            multiplier = Mathf.Clamp(multiplier, 0.05f, 1f);
            playerHealth.ApplyTemporaryDamageMultiplier(multiplier, _progressionExplosionPostDamageReductionDuration);
        }

        private void UpdateSecondaryUsageState(bool forceReset)
        {
            // NOTE: Synergy should trigger when the player is using the secondary *primary* slot (slot index 1),
            // not when a special slot weapon (isSpecial) is equipped. Use the weapon manager's current slot index.
            bool isSecondary = false;
            if (weaponManager != null && weaponManager.CurrentWeapon != null)
            {
                isSecondary = weaponManager.CurrentSlotIndex == 1;
            }

            if (forceReset)
            {
                ResetSynergyUsage();
            }

            if (forceReset || isSecondary != _usingSecondary)
            {
                _usingSecondary = isSecondary;
                if (_usingSecondary)
                {
                    ScheduleNextSynergy();
                }
            }
        }

        private void ScheduleNextSynergy()
        {
            float min = Mathf.Min(synergyMinInterval, synergyMaxInterval);
            float max = Mathf.Max(synergyMinInterval, synergyMaxInterval);
            _nextSynergyTime = Time.time + Random.Range(min, max);
        }

        private void HandleWeaponEquipped(Weapon weapon)
        {
            UpdateSecondaryUsageState(false);

            if (weaponManager == null)
            {
                return;
            }

            if (weaponManager.CurrentSlotIndex == 0)
            {
                _slot0UsedSinceSynergy = false;
            }
            else if (weaponManager.CurrentSlotIndex == 1)
            {
                _slot1UsedSinceSynergy = false;
            }
        }

        private void HandleRoundsFired(AutoShooter shooter, int count)
        {
            if (shooter == null)
            {
                return;
            }

            if (shooter == playerShooter)
            {
                RecordPrimarySlotUsage();
                TryTriggerSynergy();
                return;
            }

            TryApplySuppression(shooter, count);
        }

        private void HandleEnemyKilledByWeapon(Enemy enemy, Weapon weapon)
        {
            if (_progressionKillMoveSpeedBonus <= 0f || _progressionKillMoveSpeedDuration <= 0f || playerStats == null)
            {
                return;
            }

            if (!IsPlayerWeapon(weapon))
            {
                return;
            }

            playerStats.ApplyTemporaryMultiplier(
                PlayerStats.StatType.MoveSpeed,
                1f + _progressionKillMoveSpeedBonus,
                _progressionKillMoveSpeedDuration);
        }

        private bool IsPlayerWeapon(Weapon weapon)
        {
            if (weaponManager == null || weapon == null)
            {
                return false;
            }

            for (int i = 0; i < weaponManager.SlotCount; i++)
            {
                if (weaponManager.GetWeaponInSlot(i) == weapon)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HandleBeforeDeath(Health health)
        {
            if (!_progressionReviveAvailable || _progressionRevivePercent <= 0f || health == null)
            {
                return false;
            }

            _progressionReviveAvailable = false;
            int healAmount = Mathf.Max(1, Mathf.RoundToInt(health.MaxHP * _progressionRevivePercent));
            health.Heal(healAmount);
            return true;
        }

        private void UpdateProgressionBonuses(float deltaTime)
        {
            if (playerStats == null)
            {
                return;
            }

            Weapon weapon = weaponManager != null ? weaponManager.CurrentWeapon : null;
            bool isRifle = weapon != null && weapon.weaponClass == Weapon.WeaponClass.SemiRifle;
            bool isMoving = _playerBody != null && _playerBody.linearVelocity.sqrMagnitude > 0.01f;
            bool isFiring = playerShooter != null && playerShooter.IsFireHeld;
            bool isMg = weapon != null && weapon.weaponClass == Weapon.WeaponClass.MG;

            UpdateSustainedFireState(deltaTime, isFiring);
            UpdateMgSustainedFireState(deltaTime, isMg && isFiring);
            UpdateStationaryDamageReduction(deltaTime, isMoving);

            float damageMultiplier = 1f;
            if (isRifle && _progressionRifleDamageBonus > 0f)
            {
                damageMultiplier *= 1f + _progressionRifleDamageBonus;
            }

            if (isRifle && isMoving && _progressionRifleMoveDamageBonus > 0f)
            {
                damageMultiplier *= 1f + _progressionRifleMoveDamageBonus;
            }

            if (_progressionSustainedFireActive && _progressionSustainedDamageBonus > 0f)
            {
                damageMultiplier *= 1f + _progressionSustainedDamageBonus;
            }

            float moveMultiplier = 1f;
            if (isRifle && isMoving && _progressionRifleMoveSpeedBonus > 0f)
            {
                moveMultiplier *= 1f + _progressionRifleMoveSpeedBonus;
            }

            float fireRateMultiplier = 1f;
            if (isRifle && _progressionRifleFireRateBonus > 0f)
            {
                fireRateMultiplier *= 1f + _progressionRifleFireRateBonus;
            }

            if (isMg && _progressionMgSustainedFireActive && _progressionMgSustainedFireRateBonus > 0f)
            {
                fireRateMultiplier *= 1f + _progressionMgSustainedFireRateBonus;
            }

            float projectileSpeedMultiplier = 1f;
            if (isRifle && _progressionRifleProjectileSpeedBonus > 0f)
            {
                projectileSpeedMultiplier *= 1f + _progressionRifleProjectileSpeedBonus;
            }

            playerStats.SetConditionalDamageMultiplier(damageMultiplier);
            playerStats.SetConditionalMoveMultiplier(moveMultiplier);
            playerStats.SetConditionalFireRateMultiplier(fireRateMultiplier);
            playerStats.SetConditionalProjectileSpeedMultiplier(projectileSpeedMultiplier);
        }

        private void UpdateSustainedFireState(float deltaTime, bool isFiring)
        {
            if (_progressionSustainedDamageBonus <= 0f)
            {
                _progressionSustainedFireTimer = 0f;
                _progressionSustainedFireActive = false;
                return;
            }

            if (isFiring)
            {
                _progressionSustainedFireTimer += deltaTime;
                if (_progressionSustainedDamageDelay <= 0f)
                {
                    _progressionSustainedFireActive = true;
                }
                else if (_progressionSustainedFireTimer >= _progressionSustainedDamageDelay)
                {
                    _progressionSustainedFireActive = true;
                }
            }
            else
            {
                _progressionSustainedFireTimer = 0f;
                _progressionSustainedFireActive = false;
            }
        }

        private void UpdateMgSustainedFireState(float deltaTime, bool isFiring)
        {
            if (_progressionMgSustainedFireRateBonus <= 0f)
            {
                _progressionMgSustainedFireTimer = 0f;
                _progressionMgSustainedFireActive = false;
                return;
            }

            if (isFiring)
            {
                _progressionMgSustainedFireTimer += deltaTime;
                if (_progressionMgSustainedFireRateDelay <= 0f)
                {
                    _progressionMgSustainedFireActive = true;
                }
                else if (_progressionMgSustainedFireTimer >= _progressionMgSustainedFireRateDelay)
                {
                    _progressionMgSustainedFireActive = true;
                }
            }
            else
            {
                _progressionMgSustainedFireTimer = 0f;
                _progressionMgSustainedFireActive = false;
            }
        }

        private void UpdateStationaryDamageReduction(float deltaTime, bool isMoving)
        {
            if (playerHealth == null)
            {
                return;
            }

            if (_progressionStationaryDamageReduction <= 0f || _progressionStationaryDuration <= 0f)
            {
                _progressionStationaryTimer = 0f;
                _progressionStationaryActive = false;
                playerHealth.SetConditionalDamageMultiplier(1f);
                return;
            }

            if (isMoving)
            {
                _progressionStationaryTimer = 0f;
                _progressionStationaryActive = false;
            }
            else
            {
                _progressionStationaryTimer += deltaTime;
                if (_progressionStationaryTimer >= _progressionStationaryDuration)
                {
                    _progressionStationaryActive = true;
                }
            }

            float multiplier = _progressionStationaryActive
                ? Mathf.Clamp(1f - _progressionStationaryDamageReduction, 0.05f, 1f)
                : 1f;
            playerHealth.SetConditionalDamageMultiplier(multiplier);
        }

        private void TryTriggerSynergy()
        {
            if (!_usingSecondary || Time.time < _nextSynergyTime)
            {
                return;
            }

            if (!HasSynergyLoadout() || !_slot0UsedSinceSynergy || !_slot1UsedSinceSynergy)
            {
                return;
            }

            TriggerSynergyEffect();
            ScheduleNextSynergy();
            ResetSynergyUsage();
        }

        private void RecordPrimarySlotUsage()
        {
            if (weaponManager == null || weaponManager.CurrentWeapon == null)
            {
                return;
            }

            if (weaponManager.CurrentSlotIndex == 0)
            {
                _slot0UsedSinceSynergy = true;
            }
            else if (weaponManager.CurrentSlotIndex == 1)
            {
                _slot1UsedSinceSynergy = true;
            }
        }

        private bool HasSynergyLoadout()
        {
            if (weaponManager == null)
            {
                return false;
            }

            return weaponManager.GetWeaponInSlot(0) != null && weaponManager.GetWeaponInSlot(1) != null;
        }

        private void ResetSynergyUsage()
        {
            _slot0UsedSinceSynergy = false;
            _slot1UsedSinceSynergy = false;
        }

        private void TriggerSynergyEffect()
        {
            int roll = Random.Range(0, 3);
            switch (roll)
            {
                case 0:
                    if (playerStats)
                    {
                        playerStats.ApplyTemporaryMultiplier(PlayerStats.StatType.FireRate, synergyFireRateMultiplier, synergyFireRateDuration);
                    }
                    ShowSynergyText(synergyFireRateText);
                    break;
                case 1:
                    if (playerHealth)
                    {
                        playerHealth.ApplyTemporaryDamageMultiplier(synergyDamageResistanceMultiplier, synergyDamageResistanceDuration);
                    }
                    ShowSynergyText(synergyDamageResistText);
                    break;
                case 2:
                    TriggerOrbVacuum();
                    ShowSynergyText(synergyOrbVacuumText);
                    break;
            }
        }

        private void UpdateProgressionShortRangeDamage(float deltaTime)
        {
            if (_progressionShortRangeDamageBonus <= 0f || _progressionShortRangeRadius <= 0f || playerStats == null)
            {
                return;
            }

            _progressionShortRangeTimer = Mathf.Max(0f, _progressionShortRangeTimer - deltaTime);
            if (_progressionShortRangeTimer > 0f)
            {
                return;
            }

            _progressionShortRangeTimer = Mathf.Max(0.1f, progressionShortRangeInterval);

            int damage = Mathf.RoundToInt(playerStats.GetDamageInt() * _progressionShortRangeDamageBonus);
            if (damage <= 0)
            {
                return;
            }

            int hits = Physics2D.OverlapCircleNonAlloc(
                transform.position,
                _progressionShortRangeRadius,
                _progressionShortRangeHits,
                progressionShortRangeLayers);
            if (hits <= 0)
            {
                return;
            }

            _progressionShortRangeTargets.Clear();
            for (int i = 0; i < hits; i++)
            {
                Collider2D hit = _progressionShortRangeHits[i];
                if (!hit)
                {
                    continue;
                }

                Enemy enemy = hit.GetComponentInParent<Enemy>();
                if (!enemy || _progressionShortRangeTargets.Contains(enemy))
                {
                    continue;
                }

                _progressionShortRangeTargets.Add(enemy);
                if (enemy.TryGetComponent(out Health health))
                {
                    health.Damage(damage);
                }
            }
        }

        private void ShowSynergyText(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            DamageNumberManager.ShowText(transform.position, message, synergyTextColor, synergyTextScale);
        }

        private void TriggerOrbVacuum()
        {
            if (_orbVacuumRoutine != null)
            {
                StopCoroutine(_orbVacuumRoutine);
            }

            _orbVacuumRoutine = StartCoroutine(OrbVacuumRoutine());
        }

        private IEnumerator OrbVacuumRoutine()
        {
            XPOrb.SetGlobalAttractionMultipliers(synergyOrbVacuumRadiusMultiplier, synergyOrbVacuumSpeedMultiplier);
            yield return new WaitForSeconds(synergyOrbVacuumDuration);
            XPOrb.SetGlobalAttractionMultipliers(1f, 1f);
            _orbVacuumRoutine = null;
        }

        private void TryApplySuppression(AutoShooter shooter, int count)
        {
            if (Time.time < _suppressedUntil)
            {
                return;
            }

            Weapon weapon = shooter.CurrentWeapon;
            if (weapon == null || weapon.weaponClass != Weapon.WeaponClass.MG)
            {
                return;
            }

            Enemy enemy = shooter.GetComponentInParent<Enemy>();
            if (!enemy)
            {
                return;
            }

            float radiusSqr = mgSuppressionRadius * mgSuppressionRadius;
            if ((enemy.transform.position - transform.position).sqrMagnitude > radiusSqr)
            {
                return;
            }

            _suppressionMeter += Mathf.Max(0, count);
            if (_suppressionMeter < mgSuppressionThreshold)
            {
                return;
            }

            ApplySuppression();
            _suppressionMeter = 0f;
        }

        private void ApplySuppression()
        {
            if (playerStats)
            {
                playerStats.ApplyTemporaryMultiplier(PlayerStats.StatType.MoveSpeed, mgSuppressionMoveSpeedMultiplier, mgSuppressionDuration);
                playerStats.ApplyTemporaryMultiplier(PlayerStats.StatType.FireRate, mgSuppressionFireRateMultiplier, mgSuppressionDuration);
            }

            _suppressedUntil = Time.time + mgSuppressionDuration;
            DamageNumberManager.ShowText(transform.position, "Suppressed", mgSuppressionTextColor, mgSuppressionTextScale);
        }

        private void UpdateSuppressionMeter(float deltaTime)
        {
            if (_suppressionMeter <= 0f)
            {
                return;
            }

            _suppressionMeter = Mathf.Max(0f, _suppressionMeter - mgSuppressionDecay * deltaTime);
        }
    }
}
