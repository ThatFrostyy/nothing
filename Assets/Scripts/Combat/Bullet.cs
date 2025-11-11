using UnityEngine;


namespace FF
{
    public class Bullet : MonoBehaviour
    {
        [SerializeField] float speed = 26f;
        [SerializeField] float lifetime = 2f;
        [SerializeField] GameObject bloodFX;
        [SerializeField] LayerMask hitMask;

        int damage;
        float t;

        public void SetDamage(int d) => damage = d;

        void Update()
        {
            transform.Translate(speed * Time.deltaTime * Vector3.right, Space.Self);
            t += Time.deltaTime; if (t > lifetime) Destroy(gameObject);
        }


        void OnTriggerEnter2D(Collider2D other)
        {
            if (((1 << other.gameObject.layer) & hitMask) == 0) return;
            if (other.TryGetComponent<Health>(out var hp))
            {
                hp.Damage(damage);

                if (bloodFX)
                    Instantiate(bloodFX, transform.position, Quaternion.identity);

                CameraShake.Shake(0.05f, 0.05f);
                Destroy(gameObject);
            }
        }
    }
}