using UnityEngine;


namespace FF
{
    public class Bullet : MonoBehaviour, IPoolable
    {
        [Header("Bullet Settings")]
        [SerializeField] float speed = 26f;
        [SerializeField] float lifetime = 2f;
        [SerializeField] GameObject bloodFX;
        [SerializeField] LayerMask hitMask;
        [SerializeField, Min(0f)] float hitRadius = 0.25f;

        int damage;
        float t;
        string teamTag;
        PoolToken poolToken;
        float baseSpeed;

        public void SetDamage(int d) => damage = d;
        public void SetOwner(string tag) => teamTag = tag;
        public void SetSpeed(float newSpeed) => speed = Mathf.Max(0.01f, newSpeed);
        public float BaseSpeed => baseSpeed;

        void Awake()
        {
            poolToken = GetComponent<PoolToken>();
            if (!poolToken)
            {
                poolToken = gameObject.AddComponent<PoolToken>();
            }

            baseSpeed = Mathf.Max(0.01f, speed);
        }

        void Update()
        {
            transform.Translate(speed * Time.deltaTime * Vector3.right, Space.Self);
            t += Time.deltaTime;
            CheckForHits();
            if (t > lifetime && poolToken != null)
            {
                poolToken.Release();
            }
        }

        void CheckForHits()
        {
            if (damage <= 0 || hitRadius <= 0f)
            {
                return;
            }

            Health[] targets = FindObjectsOfType<Health>();
            foreach (var hp in targets)
            {
                if (!hp)
                {
                    continue;
                }

                GameObject target = hp.gameObject;
                if (!string.IsNullOrEmpty(teamTag) && target.CompareTag(teamTag))
                {
                    continue;
                }

                if (((1 << target.layer) & hitMask) == 0)
                {
                    continue;
                }

                float sqrDistance = (target.transform.position - transform.position).sqrMagnitude;
                if (sqrDistance > hitRadius * hitRadius)
                {
                    continue;
                }

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
                if (poolToken != null)
                {
                    poolToken.Release();
                }

                break;
            }
        }

        #region Pooling
        public void OnTakenFromPool()
        {
            t = 0f;
        }

        public void OnReturnedToPool()
        {
            t = 0f;
            damage = 0;
            teamTag = null;
            speed = baseSpeed;
        }
        #endregion Pooling
    }
}
