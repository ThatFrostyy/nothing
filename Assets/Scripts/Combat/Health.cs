using UnityEngine;


namespace FF
{
    public class Health : MonoBehaviour
    {
        [SerializeField] int maxHP = 50;

        int hp;

        public System.Action<int> OnDamaged;
        public System.Action OnDeath;
        public System.Action<int, int> OnHealthChanged;

        public int MaxHP => maxHP;
        public int CurrentHP => hp;

        void Awake()
        {
            hp = maxHP;
            var healthChanged = OnHealthChanged;
            if (healthChanged != null)
            {
                healthChanged(hp, maxHP);
            }
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

        private void Die()
        {
            var deathHandler = OnDeath;
            if (deathHandler != null)
            {
                deathHandler();
            }
            Destroy(gameObject);
        }
    }
}