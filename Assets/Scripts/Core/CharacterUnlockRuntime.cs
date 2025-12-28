using UnityEngine;
using UnityEngine.SceneManagement;

namespace FF
{
    public class CharacterUnlockRuntime : MonoBehaviour, ISceneReferenceHandler
    {
        public static CharacterUnlockRuntime Instance { get; private set; }

        private bool _sessionActive;
        private bool _gameManagerHooked;
        private CharacterDefinition _sessionCharacter;
        private Health _playerHealth;
        private float _noDamageTimer;
        private float _lastSaveTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null)
            {
                return;
            }

            var host = new GameObject("CharacterUnlockRuntime");
            DontDestroyOnLoad(host);
            Instance = host.AddComponent<CharacterUnlockRuntime>();
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
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += HandleSceneChanged;
            PlayerController.OnPlayerReady += HandlePlayerReady;
            Enemy.OnAnyEnemyKilled += HandleEnemyKilled;
            Enemy.OnAnyEnemyKilledByWeapon += HandleEnemyKilledByWeapon;
            WeaponCrate.OnAnyBroken += HandleCrateBroken;
            KillSlowMotion.OnBulletTimeTriggered += HandleBulletTimeTriggered;
            SceneReferenceRegistry.Register(this);
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleSceneChanged;
            PlayerController.OnPlayerReady -= HandlePlayerReady;
            Enemy.OnAnyEnemyKilled -= HandleEnemyKilled;
            Enemy.OnAnyEnemyKilledByWeapon -= HandleEnemyKilledByWeapon;
            WeaponCrate.OnAnyBroken -= HandleCrateBroken;
            KillSlowMotion.OnBulletTimeTriggered -= HandleBulletTimeTriggered;
            UnhookGameManager();
            UnsubscribeHealth();
            CharacterUnlockProgress.ForceSave();
            SceneReferenceRegistry.Unregister(this);
        }

        private void Update()
        {
            if (!_gameManagerHooked)
            {
                TryHookGameManager();
            }

            UpdateNoDamageTimer();
            SaveIfNeeded();
        }

        public void ClearSceneReferences()
        {
            UnhookGameManager();
            UnsubscribeHealth();
        }

        private void HandlePlayerReady(PlayerController controller)
        {
            ResetSession();
            _sessionActive = true;
            _sessionCharacter = CharacterSelectionState.SelectedCharacter;
            CharacterUnlockProgress.RecordRunStarted();

            if (controller != null)
            {
                _playerHealth = controller.GetComponentInChildren<Health>();
                if (_playerHealth)
                {
                    _playerHealth.OnDamaged += HandlePlayerDamaged;
                }
            }
        }

        private void HandleEnemyKilled(Enemy enemy)
        {
            CharacterUnlockProgress.RecordKill();

            if (enemy != null && enemy.IsBoss)
            {
                CharacterUnlockProgress.RecordBossKill();
            }
        }

        private void HandleEnemyKilledByWeapon(Enemy enemy, Weapon weapon)
        {
            CharacterUnlockProgress.RecordWeaponKill(weapon);
        }

        private void HandleCrateBroken(WeaponCrate crate)
        {
            if (crate != null)
            {
                CharacterUnlockProgress.RecordCrateDestroyed();
            }
        }

        private void HandleWaveStarted(int wave)
        {
            if (!_sessionActive)
            {
                return;
            }

            CharacterUnlockProgress.RecordWaveReached(wave, _sessionCharacter);
        }

        private void HandlePlayerDamaged(int _)
        {
            _noDamageTimer = 0f;
        }

        private void HandleSceneChanged(Scene previous, Scene next)
        {
            if (previous.name != next.name)
            {
                ResetSession();
            }
        }

        private void HandleBulletTimeTriggered()
        {
            CharacterUnlockProgress.RecordBulletTimeMoment();
        }

        private void UpdateNoDamageTimer()
        {
            if (!_sessionActive || _playerHealth == null)
            {
                return;
            }

            _noDamageTimer += Time.unscaledDeltaTime;
            CharacterUnlockProgress.RecordNoDamageDuration(_noDamageTimer);
        }

        private void SaveIfNeeded()
        {
            if (Time.unscaledTime - _lastSaveTime < 2f)
            {
                return;
            }

            _lastSaveTime = Time.unscaledTime;
            CharacterUnlockProgress.SaveIfDirty();
        }

        private void ResetSession()
        {
            _sessionActive = false;
            _sessionCharacter = null;
            _noDamageTimer = 0f;
            UnsubscribeHealth();
        }

        private void TryHookGameManager()
        {
            if (_gameManagerHooked || GameManager.I == null)
            {
                return;
            }

            GameManager.I.OnWaveStarted += HandleWaveStarted;
            _gameManagerHooked = true;
        }

        private void UnhookGameManager()
        {
            if (!_gameManagerHooked || GameManager.I == null)
            {
                _gameManagerHooked = false;
                return;
            }

            GameManager.I.OnWaveStarted -= HandleWaveStarted;
            _gameManagerHooked = false;
        }

        private void UnsubscribeHealth()
        {
            if (_playerHealth)
            {
                _playerHealth.OnDamaged -= HandlePlayerDamaged;
            }

            _playerHealth = null;
        }
    }
}
