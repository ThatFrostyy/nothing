using UnityEngine;

namespace FF
{
    [AddComponentMenu("FF/Enemies/Attacks/Grenade Throw Attack")]
    public class GrenadeThrowAttack : MonoBehaviour, IEnemyAttack
    {
        [Header("Grenade Settings")]
        [SerializeField] private GameObject grenadePrefab;

        [Header("Throw Settings")]
        [SerializeField, Min(0.1f)] private float cooldown = 3f;
        [SerializeField, Min(0.1f)] private float throwForce = 10f;
        [SerializeField, Min(0f)] private float arcHeight = 1.5f;

        private float _cooldownTimer;

        public void TickAttack(Enemy enemy, Transform player, EnemyStats stats, AutoShooter shooter, float deltaTime)
        {
            if (!grenadePrefab || !player)
            {
                return;
            }

            _cooldownTimer = Mathf.Max(0f, _cooldownTimer - deltaTime);
            if (_cooldownTimer > 0f)
            {
                return;
            }

            Vector2 toPlayer = (Vector2)(player.position - enemy.transform.position);
            if (toPlayer.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            SpawnGrenade(enemy.transform.position, toPlayer.normalized);
            _cooldownTimer = cooldown;
        }

        private void SpawnGrenade(Vector3 origin, Vector2 direction)
        {
            GameObject grenade = Instantiate(grenadePrefab, origin, Quaternion.identity);
            if (grenade.TryGetComponent(out Rigidbody2D body))
            {
                Vector2 velocity = direction * throwForce;
                velocity.y += arcHeight;
                body.linearVelocity = velocity;
            }
        }
    }
}
