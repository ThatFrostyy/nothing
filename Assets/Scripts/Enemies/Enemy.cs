using UnityEngine;

namespace FF
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Enemy : MonoBehaviour
    {
        [SerializeField] private float _moveSpeed = 3.2f;
        [SerializeField] private int _touchDamage = 10;

        private Rigidbody2D _rigidbody;
        private Transform _player;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _player = GameObject.FindWithTag("Player")?.transform;
        }

        private void FixedUpdate()
        {
            if (!_player)
                return;

            Vector2 direction = (_player.position - transform.position).normalized;
            _rigidbody.velocity = direction * _moveSpeed;
            transform.right = direction;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.collider.TryGetComponent<Health>(out var health))
                health.Damage(_touchDamage);
        }
    }
}
