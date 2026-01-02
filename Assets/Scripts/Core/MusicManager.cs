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
        [SerializeField] private float intensityRiseDuration = 10f;
        [SerializeField, Min(0f)] private float firstWaveStartDelay = 1.5f;

        // Pause system
        private bool _isPaused = false;
        private Coroutine _pauseFadeRoutine;
        [SerializeField] private float pauseFadeDuration = 0.5f;
        [SerializeField, Range(0f, 1f)] private float pauseVolumeScale = 0.25f;

        private AudioSource _active;
        private AudioSource _standby;

        private bool _gameStarted = false;

        private bool _actionMusicStarted = false;
        private bool _fullVolumeReached = false;
        private float _intensityFadeTimer = 0f;

        private Coroutine _firstWaveRoutine;

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

        public static void RefreshFromPrefs()
        {
            if (Instance == null)
            {
                return;
            }

            float value = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePrefKey, DefaultVolume));
            Instance.ApplyVolume(value, false);
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

            // Preload music audio data to avoid runtime decoding I/O when clips are first played.
            PreloadMusicClips();

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
            if (_isPaused) return;  // <-- STOP overwriting pause volume!

            if (_active != null)

            if (_gameStarted && _actionMusicStarted && !_fullVolumeReached)
                FadeGameIntensityUp();
        }


        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryBindGameManager();

            if (scene.name.Contains("Menu"))
            {
                _gameStarted = false;
                _actionMusicStarted = false;
                _fullVolumeReached = false;

                if (_menuRoutine != null) StopCoroutine(_menuRoutine);
                _menuRoutine = StartCoroutine(MenuFadeInRoutine());
            }
            else
            {
                _isPaused = false;
                _gameStarted = true;
                _actionMusicStarted = false;
                _fullVolumeReached = false;
                _currentState = MusicState.Action;
                StopAllCoroutines();
                StopAllMusic();
            }
        }

        public void SetPaused(bool paused)
        {

            if (_isPaused == paused)
                return;

            _isPaused = paused;

            if (_pauseFadeRoutine != null)
                StopCoroutine(_pauseFadeRoutine);

            if (!paused)
            {
                // ⭐ THIS IS THE FIX ⭐
                _fullVolumeReached = false;

                // Also reset intensity timer based on current volume
                float current = _active != null ? _active.volume : 0f;
                _intensityFadeTimer = (current / MusicVolume) * intensityRiseDuration;
            }

            _pauseFadeRoutine = StartCoroutine(PauseFadeRoutine(paused));
        }

        private void PreloadMusicClips()
        {
            // Try to ask Unity to load audio data ahead of time for each clip.
            TryPreloadClip(menuMusic);
            TryPreloadClip(actionMusic);
            TryPreloadClip(intenseMusic);
            TryPreloadClip(bossMusic);
        }

        private void TryPreloadClip(AudioClip clip)
        {
            if (clip == null)
                return;

            // If audio is already ready, skip. Otherwise request loading.
            // LoadAudioData is a no-op if audio data is already loaded.
            try
            {
                if (!clip.isReadyToPlay)
                {
                    clip.LoadAudioData();
                }
            }
            catch
            {
                // Fail silently if the platform doesn't support explicit preload calls.
            }
        }

        private IEnumerator PauseFadeRoutine(bool paused)
        {
            float t = 0f;
            float startA = _active != null ? _active.volume : 0f;
            float startB = _standby != null ? _standby.volume : 0f;

            float target;

            if (paused)
            {
                // Pausing → lower volume
                target = MusicVolume * pauseVolumeScale;
            }
            else
            {
                // UNPAUSING → return to intensity-controlled volume
                float intensityProgress = Mathf.Clamp01(_intensityFadeTimer / intensityRiseDuration);
                target = Mathf.Lerp(0f, MusicVolume, intensityProgress);

            }


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

            // Ensure final values are set exactly
            if (_active != null) _active.volume = target;
            if (_standby != null) _standby.volume = target;

            // We are done fading. Let Update() take over again.
        }

        // Add this helper method inside the MusicManager class
        private float GetCurrentIntensityVolume()
        {
            // If the game hasn't started or action music hasn't started, intensity is 0.
            if (!_gameStarted || !_actionMusicStarted)
                return 0f;

            // Calculate the 't' value the intensity fade is currently using.
            float t = Mathf.Clamp01(_intensityFadeTimer / intensityRiseDuration);

            // Calculate the volume that FadeGameIntensityUp() is currently setting.
            return Mathf.Lerp(0f, MusicVolume, t);
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
                _intensityFadeTimer = 0f;
                _fullVolumeReached = false;
                if (_firstWaveRoutine != null) StopCoroutine(_firstWaveRoutine);
                _firstWaveRoutine = StartCoroutine(StartFirstWaveMusicRoutine());
                return;
            }

            // Don't switch until intensity is at full volume
            if (!_fullVolumeReached)
                return;

            // ---- CUSTOM WAVE RULES ----
            if (wave >= 13)
            {
                // Wave 13+ = Boss music
                SwitchState(MusicState.Boss);
            }
            else if (wave >= 8)
            {
                // Wave 8–11 = Intense music
                SwitchState(MusicState.Intense);
            }
            else
            {
                // Wave 2–7 = Action music
                SwitchState(MusicState.Action);
            }
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

            // Use Time.deltaTime if this runs every frame and you want it tied to frame rate, 
            // but stick with Time.unscaledDeltaTime for reliability with your coroutines.
            _intensityFadeTimer += Time.unscaledDeltaTime;

            // CAP THE TIMER TO THE DURATION
            if (_intensityFadeTimer >= intensityRiseDuration)
            {
                _intensityFadeTimer = intensityRiseDuration; // Stop the timer here!
                _fullVolumeReached = true;
            }

            float t = Mathf.Clamp01(_intensityFadeTimer / intensityRiseDuration);
            float v = Mathf.Lerp(0f, MusicVolume, t);

            _active.volume = v;
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
            _active.time = 0f;
            _active.Play();
            _currentClip = clip;
        }

        private void StopAllMusic()
        {
            if (_active != null)
            {
                _active.Stop();
                _active.clip = null;
            }

            if (_standby != null)
            {
                _standby.Stop();
                _standby.clip = null;
            }
        }

        private IEnumerator StartFirstWaveMusicRoutine()
        {
            if (firstWaveStartDelay > 0f)
                yield return new WaitForSeconds(firstWaveStartDelay);

            _actionMusicStarted = true;
            PlayImmediate(actionMusic, startVolume: 0f);
            _intensityFadeTimer = 0f;
            _fullVolumeReached = false;
            _firstWaveRoutine = null;
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
                if (_isPaused)
                    yield break;

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
                SteamCloudSave.SaveToCloud();
            }
        }
    }
}
