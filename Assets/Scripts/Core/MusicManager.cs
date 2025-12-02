using UnityEngine;
using UnityEngine.SceneManagement;

namespace FF
{
    public class MusicManager : MonoBehaviour
    {
        private const string VolumePrefKey = "MusicVolume";
        private const float DefaultVolume = 0.85f;

        public static MusicManager Instance { get; private set; }
        public static float MusicVolume { get; private set; } = DefaultVolume;

        [Header("Clips")]
        [SerializeField] private AudioClip menuMusic;
        [SerializeField] private AudioClip actionMusic;
        [SerializeField] private AudioClip intenseMusic;
        [SerializeField] private AudioClip bossMusic;

        [Header("Dynamics")]
        [SerializeField, Min(0.1f)] private float crossfadeDuration = 1.5f;
        [SerializeField, Min(0.01f)] private float intensitySmoothing = 1.5f;
        [SerializeField, Min(1f)] private float waveForMaxIntensity = 16f;
        [SerializeField, Min(1)] private int bossWaveIntervalHint = 5;
        [SerializeField, Range(0f, 1f)] private float introVolumeScale = 0.15f;

        private AudioSource _activeSource;
        private AudioSource _standbySource;
        private float _activeMix = 1f;
        private float _standbyMix = 0f;
        private AudioClip _currentClip;
        private Coroutine _crossfadeRoutine;

        private GameManager _gameManager;
        private int _lastWave;
        private bool _isBossWave;
        private float _smoothedIntensity;

        public static void SetVolume(float value)
        {
            if (Instance != null)
            {
                Instance.ApplyVolume(value, true);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureSources();
            LoadSavedVolume();
            ApplyVolume(MusicVolume, false);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            RefreshSceneBindings();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            ReleaseSceneBindings();
        }

        private void Update()
        {
            UpdateIntensity();
            ApplyStateFromIntensity();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshSceneBindings();
            _smoothedIntensity = 0f;
            _isBossWave = false;
            _lastWave = 0;
            ApplyStateFromIntensity(true);
        }

        private void RefreshSceneBindings()
        {
            ReleaseSceneBindings();

            _gameManager = GameManager.I;
            if (_gameManager != null)
            {
                _gameManager.OnWaveStarted += HandleWaveStarted;
                _lastWave = _gameManager.Wave;
            }
        }

        private void ReleaseSceneBindings()
        {
            if (_gameManager != null)
            {
                _gameManager.OnWaveStarted -= HandleWaveStarted;
                _gameManager = null;
            }

        }

        private void HandleWaveStarted(int wave)
        {
            _lastWave = wave;
            _isBossWave = bossWaveIntervalHint > 0 && wave > 0 && wave % bossWaveIntervalHint == 0;
            if (_isBossWave)
            {
                _smoothedIntensity = Mathf.Max(_smoothedIntensity, 0.9f);
            }
        }

        private void UpdateIntensity()
        {
            if (_gameManager == null)
            {
                _smoothedIntensity = Mathf.MoveTowards(_smoothedIntensity, 0f, Time.unscaledDeltaTime * intensitySmoothing);
                return;
            }

            float waveIntensity = Mathf.InverseLerp(1f, Mathf.Max(1f, waveForMaxIntensity), Mathf.Max(1, _lastWave));
            float target = waveIntensity;

            if (_isBossWave)
            {
                target = 1f;
            }

            _smoothedIntensity = Mathf.MoveTowards(_smoothedIntensity, Mathf.Clamp01(target), Time.unscaledDeltaTime * intensitySmoothing);
        }

        private void ApplyStateFromIntensity(bool force = false)
        {
            AudioClip desiredClip = SelectClip();
            if (desiredClip == _currentClip && !force)
            {
                return;
            }

            PlayMusic(desiredClip);
        }

        private AudioClip SelectClip()
        {
            if (_gameManager == null)
            {
                return menuMusic;
            }

            if (_isBossWave && bossMusic)
            {
                return bossMusic;
            }

            if (_smoothedIntensity < 0.35f)
            {
                return menuMusic;
            }

            if (_smoothedIntensity < 0.7f)
            {
                return actionMusic ? actionMusic : menuMusic;
            }

            return intenseMusic ? intenseMusic : actionMusic;
        }

        private void PlayMusic(AudioClip clip)
        {
            if (!clip)
            {
                StopAllMusic();
                _currentClip = null;
                return;
            }

            if (_currentClip == clip && _activeSource && _activeSource.isPlaying)
            {
                return;
            }

            if (_crossfadeRoutine != null)
            {
                StopCoroutine(_crossfadeRoutine);
            }

            _crossfadeRoutine = StartCoroutine(CrossfadeToClip(clip));
            _currentClip = clip;
        }

        private System.Collections.IEnumerator CrossfadeToClip(AudioClip clip)
        {
            _standbySource.clip = clip;
            _standbySource.loop = true;
            _standbySource.Play();

            _standbyMix = 0f;

            float duration = Mathf.Max(0.01f, crossfadeDuration);
            float elapsed = 0f;
            float startActive = _activeMix;
            float startStandby = _standbyMix;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _activeMix = Mathf.Lerp(startActive, 0f, t);
                _standbyMix = Mathf.Lerp(startStandby, 1f, t);
                ApplyVolume(MusicVolume, false);
                yield return null;
            }

            _activeMix = 0f;
            _standbyMix = 1f;
            ApplyVolume(MusicVolume, false);

            var previousActive = _activeSource;
            _activeSource = _standbySource;
            _standbySource = previousActive;

            if (_standbySource)
            {
                _standbySource.Stop();
                _standbySource.clip = null;
            }

            _activeMix = 1f;
            _standbyMix = 0f;
            _crossfadeRoutine = null;
        }

        private void StopAllMusic()
        {
            if (_activeSource)
            {
                _activeSource.Stop();
            }

            if (_standbySource)
            {
                _standbySource.Stop();
            }
        }

        private void EnsureSources()
        {
            if (!_activeSource)
            {
                _activeSource = CreateSource("MusicSource (A)");
            }

            if (!_standbySource)
            {
                _standbySource = CreateSource("MusicSource (B)");
            }
        }

        private AudioSource CreateSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            var source = go.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            return source;
        }

        private void LoadSavedVolume()
        {
            MusicVolume = PlayerPrefs.GetFloat(VolumePrefKey, DefaultVolume);
            MusicVolume = Mathf.Clamp01(MusicVolume);
        }

        private void ApplyVolume(float value, bool persist)
        {
            MusicVolume = Mathf.Clamp01(value);
            float intensityVolumeScale = _gameManager == null ? 1f : Mathf.Lerp(introVolumeScale, 1f, _smoothedIntensity);
            if (_activeSource)
            {
                _activeSource.volume = MusicVolume * _activeMix * intensityVolumeScale;
            }

            if (_standbySource)
            {
                _standbySource.volume = MusicVolume * _standbyMix * intensityVolumeScale;
            }

            if (persist)
            {
                PlayerPrefs.SetFloat(VolumePrefKey, MusicVolume);
                PlayerPrefs.Save();
            }
        }
    }
}
