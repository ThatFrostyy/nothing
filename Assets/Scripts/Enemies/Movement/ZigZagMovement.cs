using UnityEngine;

namespace FF
{
    [AddComponentMenu("FF/Enemies/Movement/Zig Zag Movement")]
    public class ZigZagMovement : MonoBehaviour, IEnemyMovement
    {
        [SerializeField, Min(0.1f)] private float speedMultiplier = 1f;
        [SerializeField, Min(0.1f)] private float zigZagFrequency = 2f;
        [SerializeField, Min(0f)] private float zigZagAmplitude = 2.5f;
        private float _phaseOffset;

        void Awake()
        {
            _phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        public Vector2 GetDesiredVelocity(Enemy enemy, Transform player, EnemyStats stats, Rigidbody2D body, float deltaTime)
        {
            if (!player)
            {
                return Vector2.zero;
            }

            float speed = (stats ? stats.MoveSpeed : 3f) * speedMultiplier;
            Vector2 toPlayer = (Vector2)(player.position - enemy.transform.position);
            float distance = toPlayer.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                return Vector2.zero;
            }

            Vector2 forward = toPlayer / Mathf.Max(distance, 0.001f);
            Vector2 right = new Vector2(-forward.y, forward.x);
            float wave = Mathf.Sin(Time.time * zigZagFrequency + _phaseOffset);
            Vector2 zigzag = forward + right * wave * zigZagAmplitude * 0.1f;
            return zigzag.normalized * speed;
        }
    }
}
