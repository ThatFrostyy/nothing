using UnityEngine;

namespace FF
{
    /// <summary>
    /// Centralized audio playback so the project relies on a single AudioSource.
    /// Prevents sound dropouts when many transient objects would otherwise
    /// spawn their own sources.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class SoundManager : MonoBehaviour
    {
        private static SoundManager _instance;
        private AudioSource _audioSource;

        [SerializeField] private bool dontDestroyOnLoad = true;

        public static SoundManager Instance
        {
            get
            {
                if (_instance != null)
                {
                    return _instance;
                }

                _instance = FindObjectOfType<SoundManager>();
                if (_instance != null)
                {
                    return _instance;
                }

                GameObject host = new("SoundManager");
                _instance = host.AddComponent<SoundManager>();
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }

            EnsureAudioSource();
        }

        private void EnsureAudioSource()
        {
            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
            }

            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }

            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.spatialBlend = 0f;
            _audioSource.ignoreListenerPause = true;
        }

        public static void PlaySfx(AudioClip clip)
        {
            PlaySfx(clip, 1f, 1f);
        }

        public static void PlaySfx(AudioClip clip, float volume)
        {
            PlaySfx(clip, volume, 1f);
        }

        public static void PlaySfx(AudioClip clip, float volume, float pitch)
        {
            if (!clip)
            {
                return;
            }

            Instance?.PlayInternal(clip, volume, pitch);
        }

        private void PlayInternal(AudioClip clip, float volume, float pitch)
        {
            EnsureAudioSource();
            float previousPitch = _audioSource.pitch;
            _audioSource.pitch = pitch;
            float resolvedVolume = Mathf.Clamp01(volume);
            _audioSource.PlayOneShot(clip, resolvedVolume);
            _audioSource.pitch = previousPitch;
        }
    }
}
