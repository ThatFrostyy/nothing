using UnityEngine;


namespace FF
{
    public class Bullet : MonoBehaviour, IPoolable
    {
        [Header("Bullet Settings")]
        [SerializeField] float speed = 26f;
        [SerializeField] float lifetime = 2f;
        [SerializeField] GameObject bloodFX;
        [SerializeField] GameObject crateFX;
        [SerializeField] LayerMask hitMask;
        [SerializeField] float knockbackStrength = 0f;
        [SerializeField] float knockbackDuration = 0.2f;

        int damage;
        bool isCriticalDamage;
        float t;
        string teamTag;
        PoolToken poolToken;
        float baseSpeed;
        Weapon sourceWeapon;
        int pierceRemaining;

        public void SetDamage(int d, bool isCritical = false)
        {
            damage = d;
            isCriticalDamage = isCritical;
        }
        public void SetOwner(string tag) => teamTag = tag;
        public void SetSpeed(float newSpeed) => speed = Mathf.Max(0.01f, newSpeed);
        public void SetSourceWeapon(Weapon weapon) => sourceWeapon = weapon;
        public void SetKnockback(float strength, float duration)
        {
            knockbackStrength = Mathf.Max(0f, strength);
            knockbackDuration = Mathf.Max(0f, duration);
        }
        public void SetPierceCount(int count) => pierceRemaining = Mathf.Max(0, count);
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
            if (t > lifetime && poolToken != null)
            {
                poolToken.Release();
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag(teamTag)) return;
            if (((1 << other.gameObject.layer) & hitMask) == 0) return;

            if (other.TryGetComponent<Health>(out var hp))
            {
                hp.Damage(damage, sourceWeapon, isCriticalDamage);

                if (knockbackStrength > 0f && other.TryGetComponent<Enemy>(out var enemy) && teamTag == "Player")
                {
                    Vector2 direction = transform.right;
                    Vector2 force = direction.normalized * knockbackStrength;
                    enemy.ApplyKnockback(force, knockbackDuration);
                }

                TryApplyBurn(other);

                if (other.gameObject.layer.ToString() == "Crate" && crateFX)
                {
                    SpawnImpactVfx(crateFX);
                }
                else if (bloodFX)
                {
                    SpawnImpactVfx(bloodFX);
                }

                if (sourceWeapon && sourceWeapon.burnImpactVfx)
                {
                    SpawnImpactVfx(sourceWeapon.burnImpactVfx);
                }

                CameraShake.Shake(0.05f, 0.05f);
                if (pierceRemaining > 0)
                {
                    pierceRemaining--;
                }
                else if (poolToken != null)
                {
                    poolToken.Release();
                }
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
            isCriticalDamage = false;
            teamTag = null;
            speed = baseSpeed;
            knockbackStrength = 0f;
            sourceWeapon = null;
            pierceRemaining = 0;
        }
        #endregion Pooling

        private void TryApplyBurn(Collider2D other)
        {
            if (sourceWeapon == null || !sourceWeapon.appliesBurn)
            {
                return;
            }

            if (!other.TryGetComponent<Enemy>(out var enemy))
            {
                return;
            }

            enemy.ApplyBurn(
                Mathf.Max(0f, sourceWeapon.burnDuration),
                Mathf.Max(0, sourceWeapon.burnDamagePerSecond),
                Mathf.Max(0.05f, sourceWeapon.burnTickInterval),
                sourceWeapon.burnTargetVfx,
                sourceWeapon);
        }

        private void SpawnImpactVfx(GameObject prefab)
        {
            if (!prefab)
            {
                return;
            }

            GameObject fx = PoolManager.Get(prefab, transform.position, Quaternion.identity);
            if (fx && !fx.TryGetComponent<PooledParticleSystem>(out var pooled))
            {
                pooled = fx.AddComponent<PooledParticleSystem>();
                pooled.OnTakenFromPool();
            }
            else if (fx && fx.TryGetComponent<PooledParticleSystem>(out var pooledFx))
            {
                pooledFx.OnTakenFromPool();
            }
        }
    }
}
