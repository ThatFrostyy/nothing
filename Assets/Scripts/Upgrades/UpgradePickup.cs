using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FF
{
    public class UpgradePickup : MonoBehaviour
    {
        [SerializeField] private float lifetimeSeconds = 60f;
        [SerializeField] private SpriteRenderer indicatorRenderer;
        [SerializeField, Min(0f)] private float pickupRadius = 0.75f;
        [Header("Visual Settings")]
        [SerializeField] private float hoverAmplitude = 0.25f;
        [SerializeField] private float hoverSpeed = 2f;
        [SerializeField] private float rotationAmplitude = 5f;
        [SerializeField] private float rotationSpeed = 2f;
        [SerializeField] private float pulseAmplitude = 0.05f;
        [SerializeField] private float pulseSpeed = 3f;
        [SerializeField] private float pickupShrinkSpeed = 12f;
        [SerializeField] private float pickupFadeSpeed = 8f;
        [SerializeField] private float lightFadeDuration = 0.2f;
        [SerializeField] private float shakeDuration = 0.1f;
        [SerializeField] private float shakeMagnitude = 0.15f;
        [SerializeField] private GameObject pickupFx;

        [SerializeField] private UpgradePickupEffect effect;
        private float lifetimeTimer;
        private float idleTimer;
        private Vector3 startLocalPosition;
        private Vector3 startLocalScale;
        private Light2D glow;
        private bool isDespawning;
        private bool consumed;
        private Coroutine despawnRoutine;
        private Transform cachedPlayer;
        private PlayerStats cachedStats;
        private Health cachedHealth;

        public event Action<UpgradePickup> OnCollected;
        public event Action<UpgradePickup> OnExpired;
        public static event Action<UpgradePickupEffect> OnEffectApplied;

        void Awake()
        {
            glow = GetComponentInChildren<Light2D>();
            startLocalPosition = transform.localPosition;
            startLocalScale = transform.localScale;
        }

        void OnEnable()
        {
            lifetimeTimer = 0f;
            idleTimer = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            startLocalPosition = transform.localPosition;
            startLocalScale = transform.localScale;
            consumed = false;
            isDespawning = false;
        }

        void Update()
        {
            if (!isDespawning)
            {
                lifetimeTimer += Time.deltaTime;

                if (lifetimeSeconds > 0f && lifetimeTimer >= lifetimeSeconds)
                {
                    TriggerExpired();
                }

                AnimateIdle();
                DetectPlayer();
            }
            else
            {
                AnimatePickup();
            }
        }

        public UpgradePickupEffect Effect => effect;

        public void SetEffect(UpgradePickupEffect newEffect)
        {
            effect = newEffect;
        }

        private bool TryApplyEffect()
        {
            if (effect == null)
            {
                return false;
            }

            PlayerStats stats = cachedStats;
            Health health = cachedHealth;

            switch (effect.Type)
            {
                case UpgradePickupEffect.EffectType.Heal:
                    if (health == null)
                    {
                        return false;
                    }
                    health.Heal(effect.HealAmount);
                    break;
                case UpgradePickupEffect.EffectType.DamageBoost:
                    if (stats == null)
                    {
                        return false;
                    }
                    stats.ApplyTemporaryMultiplier(PlayerStats.StatType.Damage, effect.Multiplier, effect.Duration);
                    NotifyEffectApplied();
                    break;
                case UpgradePickupEffect.EffectType.MoveSpeedBoost:
                    if (stats == null)
                    {
                        return false;
                    }
                    stats.ApplyTemporaryMultiplier(PlayerStats.StatType.MoveSpeed, effect.Multiplier, effect.Duration);
                    NotifyEffectApplied();
                    break;
                case UpgradePickupEffect.EffectType.FireRateBoost:
                    if (stats == null)
                    {
                        return false;
                    }
                    stats.ApplyTemporaryMultiplier(PlayerStats.StatType.FireRate, effect.Multiplier, effect.Duration);
                    NotifyEffectApplied();
                    break;
                default:
                    return false;
            }

            return true;
        }

        private void DetectPlayer()
        {
            if (consumed || pickupRadius <= 0f)
            {
                return;
            }

            EnsurePlayerCached();
            if (!cachedPlayer)
            {
                return;
            }

            float sqrDistance = (cachedPlayer.position - transform.position).sqrMagnitude;
            if (sqrDistance > pickupRadius * pickupRadius)
            {
                return;
            }

            if (!TryApplyEffect())
            {
                return;
            }

            consumed = true;
            PlayPickupSound();
            SpawnPickupFx();
            TriggerCollected();
        }

        private void EnsurePlayerCached()
        {
            if (cachedPlayer && cachedPlayer.gameObject.activeInHierarchy)
            {
                return;
            }

            GameObject player = GameObject.FindWithTag("Player");
            cachedPlayer = player ? player.transform : null;
            cachedStats = cachedPlayer ? cachedPlayer.GetComponentInParent<PlayerStats>() : null;
            cachedHealth = cachedPlayer ? cachedPlayer.GetComponentInParent<Health>() : null;
        }

        private void AnimateIdle()
        {
            idleTimer += Time.deltaTime;

            float yOffset = Mathf.Sin(idleTimer * hoverSpeed) * hoverAmplitude;
            transform.localPosition = startLocalPosition + new Vector3(0, yOffset, 0);

            float zRot = Mathf.Sin(idleTimer * rotationSpeed) * rotationAmplitude;
            transform.localRotation = Quaternion.Euler(0, 0, zRot);

            float scalePulse = 1f + Mathf.Sin(idleTimer * pulseSpeed) * pulseAmplitude;
            transform.localScale = startLocalScale * scalePulse;
        }

        private void PlayPickupSound()
        {
            if (effect == null || !effect.PickupSound)
            {
                return;
            }

            AudioPlaybackPool.PlayOneShot(effect.PickupSound, transform.position);
        }

        private void NotifyEffectApplied()
        {
            if (effect != null && effect.Duration > 0f)
            {
                OnEffectApplied?.Invoke(effect);
            }
        }

        private void TriggerExpired()
        {
            if (consumed)
            {
                return;
            }

            consumed = true;
            TriggerDespawn();
            OnExpired?.Invoke(this);
        }

        private void TriggerCollected()
        {
            CameraShake.Shake(shakeDuration, shakeMagnitude);
            TriggerDespawn();
            OnCollected?.Invoke(this);
        }

        private void TriggerDespawn()
        {
            if (isDespawning)
            {
                return;
            }

            isDespawning = true;
            if (despawnRoutine == null)
            {
                despawnRoutine = StartCoroutine(DespawnRoutine());
            }
        }

        private IEnumerator DespawnRoutine()
        {
            if (glow)
            {
                yield return StartCoroutine(FadeLight2D(glow));
            }

            while (transform.localScale.magnitude > 0.05f)
            {
                AnimatePickup();
                yield return null;
            }

            Destroy(gameObject);
        }

        private void AnimatePickup()
        {
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, Time.deltaTime * pickupShrinkSpeed);

            if (indicatorRenderer)
            {
                Color c = indicatorRenderer.color;
                c.a = Mathf.Lerp(c.a, 0f, Time.deltaTime * pickupFadeSpeed);
                indicatorRenderer.color = c;
            }
        }

        private IEnumerator FadeLight2D(Light2D light)
        {
            float start = light.intensity;
            float t = 0f;
            while (t < lightFadeDuration)
            {
                t += Time.deltaTime;
                light.intensity = Mathf.Lerp(start, 0f, t / lightFadeDuration);
                yield return null;
            }

            light.intensity = 0f;
            light.enabled = false;
        }

        private void SpawnPickupFx()
        {
            if (!pickupFx)
            {
                return;
            }

            PoolManager.Get(pickupFx, transform.position, Quaternion.identity);
        }
    }
}
