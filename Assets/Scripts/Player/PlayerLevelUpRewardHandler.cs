using UnityEngine;

namespace FF
{
    public class PlayerLevelUpRewardHandler : MonoBehaviour
    {
        private XPWallet _wallet;
        private Health _health;
        private int _maxHpBonusPerLevel;
        private int _healPerLevel;

        public void Configure(XPWallet wallet, Health health, int maxHpBonusPerLevel, int healPerLevel)
        {
            Unsubscribe();

            _wallet = wallet;
            _health = health;
            _maxHpBonusPerLevel = Mathf.Max(0, maxHpBonusPerLevel);
            _healPerLevel = Mathf.Max(0, healPerLevel);

            if (_wallet != null && (_maxHpBonusPerLevel > 0 || _healPerLevel > 0))
            {
                _wallet.OnLevelUp += HandleLevelUp;
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void HandleLevelUp(int level)
        {
            _ = level;
            if (_health == null)
            {
                return;
            }

            if (_maxHpBonusPerLevel > 0)
            {
                int newMax = Mathf.Max(1, _health.MaxHP + _maxHpBonusPerLevel);
                _health.SetMaxHP(newMax, false);
            }

            if (_healPerLevel > 0)
            {
                _health.Heal(_healPerLevel);
            }
        }

        private void Unsubscribe()
        {
            if (_wallet != null)
            {
                _wallet.OnLevelUp -= HandleLevelUp;
            }
        }
    }
}
