using UnityEngine;


namespace FF
{
    public class Bullet : MonoBehaviour, IPoolable
    {
        [SerializeField] float speed = 26f;
        [SerializeField] float lifetime = 2f;
        [SerializeField] GameObject bloodFX;
        [SerializeField] LayerMask hitMask;

        int damage;
        float t;
        string teamTag;
        PoolToken poolToken;

        public void SetDamage(int d) => damage = d;
        public void SetOwner(string tag) => teamTag = tag;

        void Awake()
        {
            poolToken = GetComponent<PoolToken>();
            if (!poolToken)
            {
                poolToken = gameObject.AddComponent<PoolToken>();
            }
        }

        void Update()
        {
            transform.Translate(speed * Time.deltaTime * Vector3.right, Space.Self);
            t += Time.deltaTime;
            if (t > lifetime)
            {
                poolToken?.Release();
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag(teamTag)) return;
            if (((1 << other.gameObject.layer) & hitMask) == 0) return;

            if (other.TryGetComponent<Health>(out var hp))
            {
                hp.Damage(damage);

                if (bloodFX)
                {
                    GameObject fx = PoolManager.Get(bloodFX, transform.position, Quaternion.identity);
                    if (fx && !fx.TryGetComponent<PooledParticleSystem>(out var pooled))
                    {
                        pooled = fx.AddComponent<PooledParticleSystem>();
                        pooled.OnTakenFromPool();
                    }
                }

                CameraShake.Shake(0.05f, 0.05f);
                poolToken?.Release();
            }
        }

        public void OnTakenFromPool()
        {
            t = 0f;
        }

        public void OnReturnedToPool()
        {
            t = 0f;
            damage = 0;
            teamTag = null;
        }
    }
}
