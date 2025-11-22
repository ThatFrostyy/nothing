using UnityEngine;

namespace FF
{
    public interface IEnemyMovement
    {
        Vector2 GetDesiredVelocity(Enemy enemy, Transform player, EnemyStats stats, float deltaTime);
    }
}
