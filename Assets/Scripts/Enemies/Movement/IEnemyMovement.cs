using UnityEngine;
using UnityEngine.AI;

namespace FF
{
    public interface IEnemyMovement
    {
        /// <summary>
        /// Return desired world-space velocity for this enemy.
        /// Enemy will move using its NavMeshAgent.
        /// </summary>
        Vector2 GetDesiredVelocity(Enemy enemy, Transform player, EnemyStats stats, NavMeshAgent agent, float deltaTime);
    }
}
