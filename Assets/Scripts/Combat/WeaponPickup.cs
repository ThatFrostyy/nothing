using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;

namespace FF
{
    public class WeaponPickup : MonoBehaviour
    {
        [Header("Weapon Data")]
        [SerializeField] Weapon weaponData;

        [Header("Lifetime")]
        [SerializeField, Min(0f)] float lifetimeSeconds = 20f;
        [SerializeField] bool despawnWhenUncollected = true;

        [Header("Visual Settings")]
        [SerializeField] float hoverAmplitude = 0.25f;  // how high it floats
        [SerializeField] float hoverSpeed = 2f;         // speed of bob
        [SerializeField] float rotationAmplitude = 5f;  // tilt left/right
        [SerializeField] float rotationSpeed = 2f;
        [SerializeField] float pulseAmplitude = 0.05f;  // breathing scale
        [SerializeField] float pulseSpeed = 3f;

        [Header("Pickup Effects")]
        [SerializeField] AudioClip pickupSound;
        [SerializeField] float pickupShrinkSpeed = 12f;
        [SerializeField] float pickupFadeSpeed = 8f;
        [SerializeField] float shakeDuration = 0.1f;
        [SerializeField] float shakeMagnitude = 0.15f;
        [SerializeField] float lightFadeDuration = 0.2f; // fade duration for the glow
        [SerializeField] GameObject pickupFx;

        Vector3 startPos;
        Vector3 startScale;
        SpriteRenderer sr;
        Light2D glow;
        Collider2D pickupCollider;
        bool isDespawning;
        bool playPickupEffects;
        float timer = 0f;
        float lifetimeTimer;

        public event System.Action<WeaponPickup> OnCollected;
        public event System.Action<WeaponPickup> OnExpired;

        public void SetWeaponData(Weapon weapon)
        {
            weaponData = weapon;
        }

        public void SetLifetime(float seconds)
        {
            lifetimeSeconds = Mathf.Max(0f, seconds);
        }

        void Awake()
        {
            startPos = transform.localPosition;
            startScale = transform.localScale;
            sr = GetComponent<SpriteRenderer>();
            glow = GetComponentInChildren<Light2D>();
            pickupCollider = GetComponent<Collider2D>();
        }

        void OnEnable()
        {
            timer = 0f;
            lifetimeTimer = 0f;
            isDespawning = false;
            playPickupEffects = false;
            startPos = transform.localPosition;
            startScale = transform.localScale;

            if (pickupCollider)
            {
                pickupCollider.enabled = true;
            }

            if (sr)
            {
                Color color = sr.color;
                color.a = 1f;
                sr.color = color;
            }
        }

        void Update()
        {
            if (!isDespawning)
            {
                IdleAnimation();
                HandleLifetime();
            }
            else
            {
                PickupAnimation();
            }
        }

        #region Animations
        void IdleAnimation()
        {
            timer += Time.deltaTime;

            float yOffset = Mathf.Sin(timer * hoverSpeed) * hoverAmplitude;
            transform.localPosition = startPos + new Vector3(0, yOffset, 0);

            float zRot = Mathf.Sin(timer * rotationSpeed) * rotationAmplitude;
            transform.localRotation = Quaternion.Euler(0, 0, zRot);

            float scalePulse = 1f + Mathf.Sin(timer * pulseSpeed) * pulseAmplitude;
            transform.localScale = startScale * scalePulse;
        }

        private void PickupAnimation()
        {
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, Time.deltaTime * pickupShrinkSpeed);

            if (sr)
            {
                Color c = sr.color;
                c.a = Mathf.Lerp(c.a, 0f, Time.deltaTime * pickupFadeSpeed);
                sr.color = c;
            }

            if (playPickupEffects)
            {
                CameraShake.Shake(shakeDuration, shakeMagnitude);
            }

            if (transform.localScale.magnitude < 0.05f)
            {
                Destroy(gameObject);
            }
        }

        private void TriggerPickupAnimation(bool withEffects)
        {
            if (isDespawning)
            {
                return;
            }

            isDespawning = true;
            playPickupEffects = withEffects;

            if (pickupCollider)
            {
                pickupCollider.enabled = false;
            }

            if (glow) StartCoroutine(FadeLight2D(glow));
        }
        #endregion Animations

        private void PlayPickupSound()
        {
            if (pickupSound == null) return;

            AudioPlaybackPool.PlayOneShot(pickupSound, transform.position);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (isDespawning) return;

            if (!weaponData)
            {
                return;
            }

            if (other.TryGetComponent<WeaponManager>(out var wm) && wm.TryEquip(weaponData))
            {
                PlayPickupSound();
                SpawnPickupFx();
                TriggerPickupAnimation(true);
                OnCollected?.Invoke(this);
            }
        }

        private void HandleLifetime()
        {
            if (!despawnWhenUncollected || lifetimeSeconds <= 0f)
            {
                return;
            }

            lifetimeTimer += Time.deltaTime;
            if (lifetimeTimer >= lifetimeSeconds)
            {
                TriggerExpired();
            }
        }

        private void TriggerExpired()
        {
            if (isDespawning)
            {
                return;
            }

            TriggerPickupAnimation(false);
            OnExpired?.Invoke(this);
        }

        #region Light Fade
        IEnumerator FadeLight2D(Light2D light)
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
        #endregion Light Fade

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
