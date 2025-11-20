using UnityEngine;


namespace FF
{
    public class Health : MonoBehaviour
    {
        [Header("Health Settings")]
        [SerializeField] int maxHP = 50;

        int hp;
        int baseMaxHP;

        public System.Action<int> OnDamaged;
        public System.Action OnDeath;
        public System.Action<int, int> OnHealthChanged;

        public int MaxHP => maxHP;
        public int CurrentHP => hp;

        void Awake()
        {
            CacheBaseValues();
            ResetHealth(true);
        }

        public void Damage(int amount)
        {
            if (amount <= 0) return;

            int previousHp = hp;
            hp = Mathf.Max(0, hp - amount);

            int damageApplied = Mathf.Min(amount, previousHp);
            if (damageApplied > 0)
            {
                OnDamaged?.Invoke(damageApplied);

                if (TryGetComponent<Enemy>(out var enemy))
                {
                    bool emphasize = enemy.IsBoss || damageApplied >= Mathf.Max(10, maxHP * 0.35f);
                    DamageNumberManager.ShowDamage(transform.position, damageApplied, emphasize);
                }
            }

            OnHealthChanged?.Invoke(hp, maxHP);

            if (hp <= 0) Die();
        }

        public void Heal(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            int previousHp = hp;
            hp = Mathf.Min(maxHP, hp + amount);

            if (hp != previousHp)
            {
                OnHealthChanged?.Invoke(hp, maxHP);
            }
        }

        private void Die()
        {
            OnDeath?.Invoke();
            Destroy(gameObject);
        }

        #region Max HP Management
        public void SetMaxHP(int amount, bool refill = true)
        {
            amount = Mathf.Max(1, amount);
            maxHP = amount;
            if (refill)
            {
                hp = maxHP;
            }
            else
            {
                hp = Mathf.Clamp(hp, 0, maxHP);
            }
            OnHealthChanged?.Invoke(hp, maxHP);
        }

        public void ScaleMaxHP(float multiplier, bool refill = true)
        {
            if (multiplier <= 0f)
            {
                return;
            }

            if (baseMaxHP <= 0)
            {
                CacheBaseValues();
            }

            int scaled = Mathf.Max(1, Mathf.RoundToInt(baseMaxHP * multiplier));
            SetMaxHP(scaled, refill);
        }
        #endregion Max HP Management

        #region Resetting
        void CacheBaseValues()
        {
            baseMaxHP = Mathf.Max(1, maxHP);
        }

        public void ResetToBase()
        {
            CacheBaseValues();
            SetMaxHP(baseMaxHP, true);
        }

        void ResetHealth(bool refill)
        {
            if (baseMaxHP <= 0)
            {
                CacheBaseValues();
            }

            if (refill)
            {
                hp = Mathf.Max(1, baseMaxHP);
            }
            else
            {
                hp = Mathf.Clamp(hp, 0, baseMaxHP);
            }
            OnHealthChanged?.Invoke(hp, baseMaxHP);
        }
        #endregion Resetting

        void OnValidate()
        {
            maxHP = Mathf.Max(1, maxHP);
            if (!Application.isPlaying)
            {
                baseMaxHP = maxHP;
            }
        }
    }
}
