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
        Transform ownerTransform;
        float closeRangeMultiplier = 1f;
        float closeRangeRadius;

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
        public void SetCloseRangeBonus(Transform owner, float multiplier, float radius)
        {
            ownerTransform = owner;
            closeRangeMultiplier = Mathf.Max(1f, multiplier);
            closeRangeRadius = Mathf.Max(0f, radius);
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
            if (Time.timeScale <= Mathf.Epsilon && PauseMenuController.IsMenuOpen)
            {
                return;
            }

            float deltaTime = Time.timeScale < 0.999f ? Time.unscaledDeltaTime : Time.deltaTime;

            transform.Translate(speed * deltaTime * Vector3.right, Space.Self);
            t += deltaTime;
            if (t > lifetime && poolToken != null)
            {
                poolToken.Release();
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!string.IsNullOrEmpty(teamTag) && other.CompareTag(teamTag))
                return;

            if (((1 << other.gameObject.layer) & hitMask) == 0) return;

            if (other.TryGetComponent<Health>(out var hp))
            {
                int adjustedDamage = damage;
                if (ownerTransform && closeRangeMultiplier > 1f && closeRangeRadius > 0f)
                {
                    float distance = Vector2.Distance(ownerTransform.position, other.transform.position);
                    if (distance <= closeRangeRadius)
                    {
                        adjustedDamage = Mathf.Max(1, Mathf.RoundToInt(damage * closeRangeMultiplier));
                    }
                }

                hp.Damage(adjustedDamage, sourceWeapon, isCriticalDamage);

                if (knockbackStrength > 0f && other.TryGetComponent<Enemy>(out var enemy) && teamTag == "Player")
                {
                    Vector2 direction = transform.right;
                    Vector2 force = direction.normalized * knockbackStrength;
                    enemy.ApplyKnockback(force, knockbackDuration);
                }

                TryApplyBurn(other);

                if (other.gameObject.layer == LayerMask.NameToLayer("Crate") && crateFX)
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
            ownerTransform = null;
            closeRangeMultiplier = 1f;
            closeRangeRadius = 0f;
        }
        #endregion Pooling

        private void TryApplyBurn(Collider2D other)
        {
            if (sourceWeapon == null || !sourceWeapon.appliesBurn)
            {
                return;
            }

            if (sourceWeapon.burnDamagePerSecond <= 0 || sourceWeapon.burnDuration <= 0f)
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
                sourceWeapon,
                sourceWeapon.burnTargetVfxOffset);
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
