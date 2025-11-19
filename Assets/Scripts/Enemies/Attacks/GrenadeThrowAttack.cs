using UnityEngine;
using UnityEngine.Serialization;

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
        [FormerlySerializedAs("arcHeight")]
        [SerializeField, Min(0f)] private float slowdownRate = 1.5f;

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

            SpawnGrenade(enemy, stats, toPlayer.normalized);
            _cooldownTimer = cooldown;
        }

        private void SpawnGrenade(Enemy enemy, EnemyStats stats, Vector2 direction)
        {
            if (!enemy)
            {
                return;
            }

            GameObject grenade = Instantiate(grenadePrefab, enemy.transform.position, Quaternion.identity);
            if (!grenade)
            {
                return;
            }

            if (grenade.TryGetComponent(out GrenadeProjectile projectile))
            {
                float multiplier = stats ? stats.GetDamageMultiplier() : 1f;
                projectile.Launch(direction, -1, multiplier, enemy.tag, null, 0f, 1f, 1f, null, throwForce, slowdownRate);
                return;
            }

            if (grenade.TryGetComponent(out Rigidbody2D body))
            {
                Vector2 velocity = direction * throwForce;
                body.linearVelocity = velocity;
            }
        }
    }
}
