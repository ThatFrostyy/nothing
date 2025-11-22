using UnityEngine;
using UnityEngine.AI;

namespace FF
{
    [AddComponentMenu("FF/Enemies/Movement/Charger Movement (NavMesh)")]
    public class ChargerMovement : MonoBehaviour, IEnemyMovement
    {
        [Header("Charger Settings")]
        [SerializeField, Min(0.1f)] private float windupDuration = 1.5f;
        [SerializeField, Min(0.1f)] private float chargeDuration = 0.8f;
        [SerializeField, Min(0.1f)] private float chargeSpeedMultiplier = 2.5f;
        [SerializeField, Min(0.1f)] private float cooldownDuration = 1f;

        private float _stateTimer;
        private State _state = State.Windup;
        private Vector2 _chargeDirection;

        private enum State
        {
            Windup,
            Charging,
            Cooldown
        }

        public Vector2 GetDesiredVelocity(
            Enemy enemy,
            Transform player,
            EnemyStats stats,
            NavMeshAgent agent,
            float deltaTime)
        {
            _stateTimer -= deltaTime;
            if (_stateTimer <= 0f)
                AdvanceState(player, enemy.transform);

            float baseSpeed = stats ? stats.MoveSpeed : 3f;

            return _state switch
            {
                State.Charging => _chargeDirection * baseSpeed * chargeSpeedMultiplier,
                State.Cooldown => Vector2.zero,
                _ => Vector2.zero
            };
        }

        private void AdvanceState(Transform player, Transform enemyTransform)
        {
            switch (_state)
            {
                case State.Windup:
                    BeginCharge(player, enemyTransform);
                    break;
                case State.Charging:
                    _state = State.Cooldown;
                    _stateTimer = cooldownDuration;
                    break;
                case State.Cooldown:
                    _state = State.Windup;
                    _stateTimer = windupDuration;
                    break;
            }
        }

        private void BeginCharge(Transform player, Transform enemyTransform)
        {
            _state = State.Charging;
            _stateTimer = chargeDuration;

            if (player)
            {
                Vector2 toPlayer = (Vector2)(player.position - enemyTransform.position);
                if (toPlayer.sqrMagnitude > 0.001f)
                {
                    _chargeDirection = toPlayer.normalized;
                    return;
                }
            }

            _chargeDirection = enemyTransform.right.normalized;
        }

        void Reset()
        {
            _state = State.Windup;
            _stateTimer = windupDuration;
        }
    }
}
