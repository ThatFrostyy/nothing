using System.Collections.Generic;
using UnityEngine;

namespace FF
{
    [RequireComponent(typeof(Health))]
    public class WeaponCrate : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform dropPoint;

        [Header("Drops")]
        [SerializeField] private WeaponPickup[] pickupPool;
        [SerializeField, Min(0f)] private float pickupLifetimeSeconds = 20f;

        [Header("Lifetime")]
        [SerializeField, Min(0f)] private float lifetimeSeconds = 30f;

        [Header("Effects")]
        [SerializeField] private GameObject breakFx;
        [SerializeField] private AudioClip breakSfx;
        [SerializeField] private AudioClip hitSfx;
        [SerializeField, Min(0f)] private float hitSfxCooldown = 0.05f;

        private Health _health;
        private float _lifetimeTimer;
        private bool _isBroken;
        private bool _isExpired;
        private float _hitSfxTimer;

        private static readonly HashSet<WeaponCrate> activeCrates = new();

        public event System.Action<WeaponCrate> OnBroken;
        public event System.Action<WeaponCrate> OnExpired;

        public static event System.Action<WeaponCrate> OnAnyBroken;

        public static IReadOnlyCollection<WeaponCrate> ActiveCrates => activeCrates;

        public void ConfigurePickups(WeaponPickup[] pool)
        {
            pickupPool = pool;
        }

        public void SetPickupLifetime(float seconds)
        {
            pickupLifetimeSeconds = Mathf.Max(0f, seconds);
        }

        public void SetLifetime(float seconds)
        {
            lifetimeSeconds = Mathf.Max(0f, seconds);
        }

        void Awake()
        {
            _health = GetComponent<Health>();

            if (!dropPoint)
            {
                dropPoint = transform;
            }
        }

        void OnEnable()
        {
            _lifetimeTimer = 0f;
            _isBroken = false;
            _isExpired = false;
            _hitSfxTimer = 0f;

            if (_health)
            {
                _health.OnDeath += HandleBroken;
                _health.OnDamaged += HandleDamaged;
            }

            activeCrates.Add(this);
        }

        void Update()
        {
            if (_hitSfxTimer > 0f)
            {
                _hitSfxTimer -= Time.deltaTime;
            }

            if (_isBroken || _isExpired)
            {
                return;
            }

            if (lifetimeSeconds > 0f)
            {
                _lifetimeTimer += Time.deltaTime;
                if (_lifetimeTimer >= lifetimeSeconds)
                {
                    TriggerExpired();
                }
            }
        }

        void OnDisable()
        {
            if (_health)
            {
                _health.OnDeath -= HandleBroken;
                _health.OnDamaged -= HandleDamaged;
            }

            activeCrates.Remove(this);
        }

        void HandleBroken()
        {
            if (_isBroken || _isExpired)
            {
                return;
            }

            _isBroken = true;

            SpawnWeaponPickup();
            PlayBreakFx();
            PlayBreakSfx();

            OnBroken?.Invoke(this);
            OnAnyBroken?.Invoke(this);
        }

        void HandleDamaged(int amount)
        {
            if (_isBroken || _isExpired)
            {
                return;
            }

            if (_hitSfxTimer > 0f)
            {
                return;
            }

            PlayHitSfx();
            _hitSfxTimer = hitSfxCooldown;
        }

        void TriggerExpired()
        {
            if (_isBroken || _isExpired)
            {
                return;
            }

            _isExpired = true;
            OnExpired?.Invoke(this);
            PlayBreakFx();
            PlayBreakSfx();
            Destroy(gameObject);
        }

        void SpawnWeaponPickup()
        {
            WeaponPickup pickupPrefab = GetRandomPickup();
            if (!pickupPrefab)
            {
                return;
            }

            Vector3 spawnPos = dropPoint ? dropPoint.position : transform.position;
            var pickup = Instantiate(pickupPrefab, spawnPos, Quaternion.identity);
            pickup.SetLifetime(pickupLifetimeSeconds);
        }

        WeaponPickup GetRandomPickup()
        {
            if (pickupPool == null || pickupPool.Length == 0)
            {
                return null;
            }

            int index = Random.Range(0, pickupPool.Length);
            return pickupPool[index];
        }

        void PlayBreakFx()
        {
            if (!breakFx)
            {
                return;
            }

            PoolManager.Get(breakFx, transform.position, Quaternion.identity);
        }

        void PlayBreakSfx()
        {
            if (!breakSfx)
            {
                return;
            }

            AudioPlaybackPool.PlayOneShot(breakSfx, transform.position);
        }

        void PlayHitSfx()
        {
            if (!hitSfx)
            {
                return;
            }

            AudioPlaybackPool.PlayOneShot(hitSfx, transform.position);
        }
    }
}
