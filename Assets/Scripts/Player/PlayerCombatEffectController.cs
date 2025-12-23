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

        private bool _usingSecondary;
        private float _nextSynergyTime;
        private float _suppressionMeter;
        private float _suppressedUntil;
        private Coroutine _orbVacuumRoutine;

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
        }

        private void UpdateSecondaryUsageState(bool forceReset)
        {
            bool isSecondary = weaponManager && weaponManager.CurrentWeapon && weaponManager.CurrentWeapon.isSpecial;
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
        }

        private void HandleRoundsFired(AutoShooter shooter, int count)
        {
            if (shooter == null)
            {
                return;
            }

            if (shooter == playerShooter)
            {
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

            TriggerSynergyEffect();
            ScheduleNextSynergy();
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
