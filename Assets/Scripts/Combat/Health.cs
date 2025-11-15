using UnityEngine;


namespace FF
{
    public class Health : MonoBehaviour
    {
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
                var damagedHandler = OnDamaged;
                if (damagedHandler != null)
                {
                    damagedHandler(damageApplied);
                }
            }

            var healthChanged = OnHealthChanged;
            if (healthChanged != null)
            {
                healthChanged(hp, maxHP);
            }

            if (hp <= 0) Die();
        }

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

            var healthChanged = OnHealthChanged;
            if (healthChanged != null)
            {
                healthChanged(hp, maxHP);
            }
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

        public void ResetToBase()
        {
            CacheBaseValues();
            SetMaxHP(baseMaxHP, true);
        }

        void CacheBaseValues()
        {
            baseMaxHP = Mathf.Max(1, maxHP);
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

            var healthChanged = OnHealthChanged;
            if (healthChanged != null)
            {
                healthChanged(hp, baseMaxHP);
            }
        }

        private void Die()
        {
            var deathHandler = OnDeath;
            if (deathHandler != null)
            {
                deathHandler();
            }
            Destroy(gameObject);
        }

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
