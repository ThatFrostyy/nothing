using UnityEngine;
using UnityEngine.AI;

namespace FF
{
    [AddComponentMenu("FF/Enemies/Movement/Zig Zag Movement (NavMesh)")]
    public class ZigZagMovement : MonoBehaviour, IEnemyMovement
    {
        [Header("Zig Zag Settings")]
        [SerializeField, Min(0.1f)] private float speedMultiplier = 1f;
        [SerializeField, Min(0.1f)] private float zigZagFrequency = 2f;
        [SerializeField, Min(0f)] private float zigZagAmplitude = 2.5f;

        private float _phaseOffset;

        void Awake()
        {
            _phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        public Vector2 GetDesiredVelocity(
            Enemy enemy,
            Transform player,
            EnemyStats stats,
            NavMeshAgent agent,
            float deltaTime)
        {
            if (!player)
                return Vector2.zero;

            float speed = (stats ? stats.MoveSpeed : 3f) * speedMultiplier;

            Vector2 toPlayer = (Vector2)(player.position - enemy.transform.position);
            float distance = toPlayer.magnitude;
            if (distance < 0.001f)
                return Vector2.zero;

            Vector2 forward = toPlayer / distance;
            Vector2 right = new(-forward.y, forward.x);

            float wave = Mathf.Sin(Time.time * zigZagFrequency + _phaseOffset);

            Vector2 zigZag = forward + wave * zigZagAmplitude * 0.1f * right;

            return zigZag.normalized * speed;
        }
    }
}
