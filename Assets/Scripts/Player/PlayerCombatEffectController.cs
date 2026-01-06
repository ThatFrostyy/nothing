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

        private void Awake()
        {
            weaponManager = weaponManager ? weaponManager : GetComponentInChildren<WeaponManager>();
            playerShooter = playerShooter ? playerShooter : weaponManager != null ? weaponManager.Shooter : null;
            playerStats = playerStats ? playerStats : GetComponent<PlayerStats>();
            playerHealth = playerHealth ? playerHealth : GetComponent<Health>();
            if (!playerShooter)
            {
                playerShooter = GetComponentInChildren<AutoShooter>();
            }
        }

        private void OnEnable()
        {
            AutoShooter.OnRoundsFired += HandleRoundsFired;
            if (weaponManager)
            {
                weaponManager.OnWeaponEquipped += HandleWeaponEquipped;
            }

            UpdateSecondaryUsageState(true);
        }

        private void OnDisable()
        {
            AutoShooter.OnRoundsFired -= HandleRoundsFired;
            if (weaponManager)
            {
                weaponManager.OnWeaponEquipped -= HandleWeaponEquipped;
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
        }

        public void ConfigureProgressionShortRangeDamage(float damagePercent, float radius)
        {
            _progressionShortRangeDamageBonus = Mathf.Max(0f, damagePercent);
            _progressionShortRangeRadius = Mathf.Max(0f, radius);
            _progressionShortRangeTimer = 0f;
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
