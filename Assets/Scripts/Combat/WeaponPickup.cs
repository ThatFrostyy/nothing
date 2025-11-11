using UnityEngine;

namespace FF
{
    public class WeaponPickup : MonoBehaviour
    {
        [Header("Weapon Data")]
        [SerializeField] Weapon weaponData;

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

        Vector3 startPos;
        Vector3 startScale;
        SpriteRenderer sr;
        bool isPickedUp = false;
        float timer = 0f;

        void Awake()
        {
            startPos = transform.localPosition;
            startScale = transform.localScale;
            sr = GetComponent<SpriteRenderer>();
        }

        void Update()
        {
            if (!isPickedUp)
            {
                IdleAnimation();
            }
            else
            {
                PickupAnimation();
            }
        }

        void IdleAnimation()
        {
            timer += Time.deltaTime;

            // Hover effect
            float yOffset = Mathf.Sin(timer * hoverSpeed) * hoverAmplitude;
            transform.localPosition = startPos + new Vector3(0, yOffset, 0);

            // Gentle rotation / tilt
            float zRot = Mathf.Sin(timer * rotationSpeed) * rotationAmplitude;
            transform.localRotation = Quaternion.Euler(0, 0, zRot);

            // Pulse scale
            float scalePulse = 1f + Mathf.Sin(timer * pulseSpeed) * pulseAmplitude;
            transform.localScale = startScale * scalePulse;
        }

        void PickupAnimation()
        {
            // Shrink toward zero
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, Time.deltaTime * pickupShrinkSpeed);

            if (sr)
            {
                Color c = sr.color;
                c.a = Mathf.Lerp(c.a, 0f, Time.deltaTime * pickupFadeSpeed);
                sr.color = c;
            }

            CameraShake.Shake(shakeDuration, shakeMagnitude);

            if (transform.localScale.magnitude < 0.05f)
                Destroy(gameObject);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent<WeaponManager>(out var wm))
            {
                wm.Equip(weaponData);
                PlayPickupSound();
                TriggerPickupAnimation();
            }
        }

        void TriggerPickupAnimation()
        {
            isPickedUp = true;
        }

        void PlayPickupSound()
        {
            if (pickupSound == null) return;
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        }
    }
}
