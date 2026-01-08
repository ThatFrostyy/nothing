using UnityEngine;

namespace FF
{
    public class PlayerUpgradeController : MonoBehaviour
    {
        private PlayerStats _playerStats;
        private Health _health;

        private void Awake()
        {
            _playerStats = GetComponent<PlayerStats>();
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            if (_health != null)
            {
                _health.OnDamaged += HandleDamage;
            }
            Health.OnDamageDealt += HandleDamageDealt;
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.OnDamaged -= HandleDamage;
            }
            Health.OnDamageDealt -= HandleDamageDealt;
        }

        private void Update()
        {
            if (_playerStats != null)
            {
                XPOrb.SetGlobalAttractionMultipliers(_playerStats.GetXPGatherRadius(), 1f);
            }
        }

        private void HandleDamage(int damage)
        {
            if (_playerStats != null && _playerStats.AdrenalineRush)
            {
                _playerStats.ApplyTemporaryMultiplier(PlayerStats.StatType.MoveSpeed, 1.3f, 3f);
                _playerStats.ApplyTemporaryMultiplier(PlayerStats.StatType.FireRate, 1.2f, 3f);
            }
        }

        private void HandleDamageDealt(int damage)
        {
            if (_playerStats != null && _playerStats.VampiricStrikes)
            {
                _health.Heal(Mathf.RoundToInt(damage * 0.05f));
            }
        }
    }
}
