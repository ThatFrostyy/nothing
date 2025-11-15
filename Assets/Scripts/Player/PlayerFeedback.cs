using UnityEngine;

namespace FF
{
    [RequireComponent(typeof(Health))]
    public class PlayerFeedback : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Health health;
        [SerializeField] private XPWallet wallet;
        [SerializeField] private AudioSource audioSource;

        [Header("Hit Feedback")]
        [SerializeField] private AudioClip hitSound;
        [SerializeField, Range(0f, 1f)] private float hitSoundVolume = 1f;
        [SerializeField, Min(0f)] private float hitShakeDuration = 0.18f;
        [SerializeField, Min(0f)] private float hitShakeIntensity = 0.18f;

        [Header("Level Up Feedback")]
        [SerializeField] private AudioClip levelUpSound;
        [SerializeField, Range(0f, 1f)] private float levelUpSoundVolume = 1f;
        [SerializeField] private GameObject levelUpParticles;

        void Awake()
        {
            if (!health)
            {
                health = GetComponent<Health>();
            }

            if (!wallet)
            {
                wallet = GetComponent<XPWallet>();
            }

            if (!audioSource)
            {
                audioSource = GetComponent<AudioSource>();
            }
        }

        void OnEnable()
        {
            if (health != null)
            {
                health.OnDamaged += HandleDamaged;
            }

            if (wallet != null)
            {
                wallet.OnLevelUp += HandleLevelUp;
            }
        }

        void OnDisable()
        {
            if (health != null)
            {
                health.OnDamaged -= HandleDamaged;
            }

            if (wallet != null)
            {
                wallet.OnLevelUp -= HandleLevelUp;
            }
        }

        void HandleDamaged(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            if (hitShakeDuration > 0f || hitShakeIntensity > 0f)
            {
                CameraShake.Shake(hitShakeDuration, hitShakeIntensity);
            }

            PlayClip(hitSound, hitSoundVolume);
        }

        void HandleLevelUp(int level)
        {
            if (levelUpParticles)
            {
                var spawned = Instantiate(levelUpParticles, transform.position, Quaternion.identity);
                spawned.transform.SetParent(transform, true);

                float lifetime = 0f;
                var particleSystems = spawned.GetComponentsInChildren<ParticleSystem>();
                foreach (var ps in particleSystems)
                {
                    var main = ps.main;
                    float estimated = main.duration + main.startLifetime.constantMax;
                    lifetime = Mathf.Max(lifetime, estimated);
                }

                Destroy(spawned, lifetime > 0f ? lifetime : 5f);
            }

            PlayClip(levelUpSound, levelUpSoundVolume);
        }

        void PlayClip(AudioClip clip, float volume)
        {
            if (!clip)
            {
                return;
            }

            if (audioSource)
            {
                audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
            }
            else
            {
                AudioSource.PlayClipAtPoint(clip, transform.position, Mathf.Clamp01(volume));
            }
        }
    }
}
