using UnityEngine;

namespace FF
{
    [AddComponentMenu("FF/Enemies/Attacks/Shooter Attack")]
    public class ShooterAttack : MonoBehaviour, IEnemyAttack
    {
        [Header("Shooting Settings")]
        [SerializeField, Min(0.1f)] private float bufferDistance = 1f;
        [SerializeField] private bool fireWhenCloserThanPreferred = true;

        public void TickAttack(Enemy enemy, Transform player, EnemyStats stats, AutoShooter shooter, float deltaTime)
        {
            if (!shooter || !player)
            {
                return;
            }

            float shootDistance = stats ? stats.ShootingDistance : 8f;
            float buffer = Mathf.Max(0f, stats ? stats.DistanceBuffer : bufferDistance);
            float distance = Vector2.Distance(enemy.transform.position, player.position);
            bool shouldFire = distance <= shootDistance + buffer;
            if (!fireWhenCloserThanPreferred && distance < shootDistance - buffer)
            {
                shouldFire = false;
            }

            if (enemy)
            {
                shouldFire = false;
            }

            shooter.SetFireHeld(shouldFire);
        }
    }
}
