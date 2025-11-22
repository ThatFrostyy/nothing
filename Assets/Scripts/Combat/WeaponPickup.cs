using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;

namespace FF
{
    public class WeaponPickup : MonoBehaviour
    {
        [Header("Weapon Data")]
        [SerializeField] Weapon weaponData;
        [SerializeField, Min(0f)] float pickupRadius = 0.75f;

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
        bool isPickedUp = false;
        float timer = 0f;
        WeaponManager cachedWeaponManager;

        void Awake()
        {
            startPos = transform.localPosition;
            startScale = transform.localScale;
            sr = GetComponent<SpriteRenderer>();
            glow = GetComponentInChildren<Light2D>();
        }

        void Update()
        {
            if (!isPickedUp)
            {
                IdleAnimation();
                DetectPlayer();
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

            CameraShake.Shake(shakeDuration, shakeMagnitude);

            if (transform.localScale.magnitude < 0.05f)
            {
                Destroy(gameObject);
            }
        }

        private void TriggerPickupAnimation()
        {
            isPickedUp = true;
            if (glow) StartCoroutine(FadeLight2D(glow));
        }
        #endregion Animations

        private void PlayPickupSound()
        {
            if (pickupSound == null) return;

            AudioPlaybackPool.PlayOneShot(pickupSound, transform.position);
        }

        void DetectPlayer()
        {
            if (isPickedUp || pickupRadius <= 0f)
            {
                return;
            }

            if (!cachedWeaponManager || !cachedWeaponManager.isActiveAndEnabled)
            {
                CacheWeaponManager();
            }

            if (!cachedWeaponManager || !weaponData)
            {
                return;
            }

            float sqrDistance = (cachedWeaponManager.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance > pickupRadius * pickupRadius)
            {
                return;
            }

            if (cachedWeaponManager.TryEquip(weaponData))
            {
                isPickedUp = true;
                PlayPickupSound();
                SpawnPickupFx();
                TriggerPickupAnimation();
            }
        }

        void CacheWeaponManager()
        {
            cachedWeaponManager = FindObjectOfType<WeaponManager>();
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
