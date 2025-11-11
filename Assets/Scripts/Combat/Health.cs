using UnityEngine;


namespace FF
{
    public class Health : MonoBehaviour
    {
        [SerializeField] int maxHP = 50;

        int hp;
        public System.Action OnDeath;

        void Awake() => hp = maxHP;

        public void Damage(int amount)
        {
            hp -= amount;
            if (hp <= 0) Die();
        }

        private void Die()
        {
            OnDeath?.Invoke();
            Destroy(gameObject);
        }
    }
}