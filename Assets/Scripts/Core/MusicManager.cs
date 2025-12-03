using System.Collections;
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

        private enum MusicState { Action, Intense, Boss }
        private MusicState _currentState = MusicState.Action;

        [Header("Clips")]
        [SerializeField] private AudioClip menuMusic;
        [SerializeField] private AudioClip actionMusic;
        [SerializeField] private AudioClip intenseMusic;
        [SerializeField] private AudioClip bossMusic;

        [Header("Durations")]
        [SerializeField] private float menuFadeDuration = 1.5f;
        [SerializeField] private float crossfadeDuration = 1.5f;
        [SerializeField] private float intensityRiseDuration = 8f;

        [Header("Tier Settings")]
        [SerializeField] private int intenseWave = 4;
        [SerializeField] private int bossWaveInterval = 10;

        // Pause system
        private bool _isPaused = false;
        private Coroutine _pauseFadeRoutine;
        [SerializeField] private float pauseFadeDuration = 0.5f;
        [SerializeField, Range(0f, 1f)] private float pauseVolumeScale = 0.25f;

        private AudioSource _active;
        private AudioSource _standby;

        private bool _inMenu = true;
        private bool _gameStarted = false;

        private bool _firstWaveStarted = false;
        private bool _fullVolumeReached = false;
        private float _intensityFadeTimer = 0f;

        private AudioClip _currentClip;
        private Coroutine _crossfadeRoutine;
        private Coroutine _menuRoutine;

        private GameManager _gm;

        private float _lastAppliedVolume = DefaultVolume;

        public static void SetVolume(float value)
        {
            if (Instance != null)
                Instance.ApplyVolume(value, true);
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
            CreateSources();

            LoadSavedVolume();
            _lastAppliedVolume = MusicVolume;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryBindGameManager();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnbindGameManager();
        }

        private void Update()
        {
            if (_gameStarted && _firstWaveStarted && !_fullVolumeReached)
                FadeGameIntensityUp();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryBindGameManager();

            if (scene.name.Contains("Menu"))
            {
                _inMenu = true;
                _gameStarted = false;
                _firstWaveStarted = false;
                _fullVolumeReached = false;

                if (_menuRoutine != null) StopCoroutine(_menuRoutine);
                _menuRoutine = StartCoroutine(MenuFadeInRoutine());
            }
            else
            {
                _inMenu = false;
                _gameStarted = true;
                _firstWaveStarted = false;
                _fullVolumeReached = false;
                _currentState = MusicState.Action;

                PlayImmediate(actionMusic, startVolume: 0f);
            }
        }

        public void SetPaused(bool paused)
        {
            if (_isPaused == paused)
                return;

            _isPaused = paused;

            if (_pauseFadeRoutine != null)
                StopCoroutine(_pauseFadeRoutine);

            _pauseFadeRoutine = StartCoroutine(PauseFadeRoutine(paused));
        }

        private IEnumerator PauseFadeRoutine(bool paused)
        {
            float t = 0f;

            float startA = _active != null ? _active.volume : 0f;
            float startB = _standby != null ? _standby.volume : 0f;

            float target = paused ? MusicVolume * pauseVolumeScale : MusicVolume;

            while (t < pauseFadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = t / pauseFadeDuration;

                if (_active != null)
                    _active.volume = Mathf.Lerp(startA, target, p);

                if (_standby != null)
                    _standby.volume = Mathf.Lerp(startB, target, p);

                yield return null;
            }

            if (_active != null) _active.volume = target;
            if (_standby != null) _standby.volume = target;
        }

        private void TryBindGameManager()
        {
            UnbindGameManager();
            _gm = GameManager.I;

            if (_gm != null)
                _gm.OnWaveStarted += HandleWaveStart;
        }

        private void UnbindGameManager()
        {
            if (_gm != null)
                _gm.OnWaveStarted -= HandleWaveStart;

            _gm = null;
        }

        private void HandleWaveStart(int wave)
        {
            if (wave == 1)
            {
                _firstWaveStarted = true;
                _intensityFadeTimer = 0f;
                return;
            }

            if (!_fullVolumeReached)
                return;

            // ?? After full volume, waves now control tier switching:
            if (wave % bossWaveInterval == 0)
                SwitchState(MusicState.Boss);
            else if (wave >= intenseWave)
                SwitchState(MusicState.Intense);
            else
                SwitchState(MusicState.Action);
        }

        // ------------------------------
        // MENU MUSIC LOGIC
        // ------------------------------

        private IEnumerator MenuFadeInRoutine()
        {
            PlayImmediate(menuMusic, startVolume: 0f);

            float t = 0f;
            while (t < menuFadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float v = Mathf.Lerp(0f, MusicVolume, t / menuFadeDuration);
                if (_active != null)
                    _active.volume = v;
                yield return null;
            }

            if (_active != null)
                _active.volume = MusicVolume;
        }

        public void FadeOutMenuAndStartGame()
        {
            if (_menuRoutine != null) StopCoroutine(_menuRoutine);
            StartCoroutine(MenuFadeOutRoutine());
        }

        private IEnumerator MenuFadeOutRoutine()
        {
            float t = 0f;
            float startVol = _active != null ? _active.volume : 0f;

            while (t < menuFadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float v = Mathf.Lerp(startVol, 0f, t / menuFadeDuration);
                if (_active != null)
                    _active.volume = v;
                yield return null;
            }

            if (_active != null)
                _active.volume = 0f;
        }

        // ------------------------------
        // GAME INTENSITY FADE-UP
        // ------------------------------

        private void FadeGameIntensityUp()
        {
            if (_active == null)
                return;

            _intensityFadeTimer += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(_intensityFadeTimer / intensityRiseDuration);
            float v = Mathf.Lerp(0.15f * MusicVolume, MusicVolume, t);

            _active.volume = v;

            if (t >= 1f)
                _fullVolumeReached = true;
        }

        // ------------------------------
        // MUSIC PLAYBACK
        // ------------------------------

        private void SwitchState(MusicState newState)
        {
            if (_currentState == newState)
                return;

            _currentState = newState;

            AudioClip target = newState switch
            {
                MusicState.Action => actionMusic,
                MusicState.Intense => intenseMusic,
                MusicState.Boss => bossMusic,
                _ => actionMusic
            };

            PlayMusic(target);
        }

        private void PlayImmediate(AudioClip clip, float startVolume)
        {
            if (_active == null)
                return;

            _active.clip = clip;
            _active.volume = startVolume;
            _active.loop = true;
            _active.Play();
            _currentClip = clip;
        }

        private void PlayMusic(AudioClip clip)
        {
            if (_currentClip == clip || clip == null)
                return;

            if (_crossfadeRoutine != null)
                StopCoroutine(_crossfadeRoutine);

            _crossfadeRoutine = StartCoroutine(CrossfadeRoutine(clip));
        }

        private IEnumerator CrossfadeRoutine(AudioClip clip)
        {
            if (_standby == null || _active == null)
                yield break;

            _standby.clip = clip;
            _standby.loop = true;
            _standby.volume = 0f;
            _standby.Play();

            float t = 0f;
            float startA = _active.volume;

            while (t < crossfadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = t / crossfadeDuration;

                _active.volume = Mathf.Lerp(startA, 0f, p);
                _standby.volume = Mathf.Lerp(0f, MusicVolume, p);

                yield return null;
            }

            _active.volume = 0f;
            _standby.volume = MusicVolume;

            var tmp = _active;
            _active = _standby;
            _standby = tmp;

            _standby.Stop();
            _currentClip = clip;
        }

        private void CreateSources()
        {
            _active = CreateSource("Music A");
            _standby = CreateSource("Music B");
        }

        private AudioSource CreateSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.spatialBlend = 0f;
            src.playOnAwake = false;
            src.ignoreListenerPause = true;
            return src;
        }

        private void LoadSavedVolume()
        {
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePrefKey, DefaultVolume));
        }

        private void ApplyVolume(float value, bool persist)
        {
            float newVol = Mathf.Clamp01(value);

            float factor = (_lastAppliedVolume <= 0.0001f) ? newVol : newVol / _lastAppliedVolume;

            if (_active != null)
                _active.volume *= factor;

            if (_standby != null)
                _standby.volume *= factor;

            MusicVolume = newVol;
            _lastAppliedVolume = newVol;

            if (persist)
            {
                PlayerPrefs.SetFloat(VolumePrefKey, MusicVolume);
                PlayerPrefs.Save();
            }
        }
    }
}
