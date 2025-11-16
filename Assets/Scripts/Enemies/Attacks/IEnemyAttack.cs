using UnityEngine;

namespace FF
{
    public interface IEnemyAttack
    {
        void TickAttack(Enemy enemy, Transform player, EnemyStats stats, AutoShooter shooter, float deltaTime);
    }
}
