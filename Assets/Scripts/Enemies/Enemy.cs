using UnityEngine;


namespace FF
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Enemy : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 3.2f;
        [SerializeField] int touchDamage = 10;

        Rigidbody2D rb; Transform player;


        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            player = GameObject.FindWithTag("Player")?.transform;
        }


        void FixedUpdate()
        {
            if (!player) return;
            Vector2 dir = (player.position - transform.position).normalized;
            rb.linearVelocity = dir * moveSpeed;
            transform.right = dir;
        }


        void OnCollisionEnter2D(Collision2D c)
        {
            if (c.collider.TryGetComponent<Health>(out var hp)) hp.Damage(touchDamage);
        }
    }
}