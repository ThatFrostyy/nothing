using UnityEngine;


namespace FF
{
    public class Health : MonoBehaviour
    {
        [SerializeField] int maxHP = 50;

        int hp;
        public System.Action<int> OnDamaged;
        public System.Action OnDeath;

        void Awake() => hp = maxHP;

        public void Damage(int amount)
        {
            if (amount <= 0) return;

            int previousHp = hp;
            hp = Mathf.Max(0, hp - amount);

            int damageApplied = Mathf.Min(amount, previousHp);
            if (damageApplied > 0)
            {
                OnDamaged?.Invoke(damageApplied);
            }

            if (hp <= 0) Die();
        }

        private void Die()
        {
            OnDeath?.Invoke();
            Destroy(gameObject);
        }
    }
}