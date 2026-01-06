using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FF
{
    public class CharacterProgressionRuntime : MonoBehaviour, ISceneReferenceHandler
    {
        public static CharacterProgressionRuntime Instance { get; private set; }

        private int _highestWave;
        private int _killCount;
        private bool _sessionActive;
        private bool _finalized;
        private bool _gameManagerHooked;

        private readonly HashSet<Health> _trackedHealth = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null)
            {
                return;
            }

            var host = new GameObject("CharacterProgressionRuntime");
            DontDestroyOnLoad(host);
            Instance = host.AddComponent<CharacterProgressionRuntime>();
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
            SceneReferenceRegistry.Register(this);
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleSceneChanged;
            PlayerController.OnPlayerReady -= HandlePlayerReady;
            UnhookGameManager();
            UnsubscribeHealth();
            SceneReferenceRegistry.Unregister(this);
        }

        private void Update()
        {
            if (!_gameManagerHooked)
            {
                TryHookGameManager();
            }
        }

        public void ClearSceneReferences()
        {
            UnhookGameManager();
            UnsubscribeHealth();
        }

        private void TryHookGameManager()
        {
            if (_gameManagerHooked || GameManager.I == null)
            {
                return;
            }

            GameManager.I.OnKillCountChanged += HandleKillCountChanged;
            GameManager.I.OnWaveStarted += HandleWaveStarted;
            _killCount = GameManager.I.KillCount;
            _highestWave = GameManager.I.Wave;
            _gameManagerHooked = true;
        }

        private void UnhookGameManager()
        {
            if (!_gameManagerHooked || GameManager.I == null)
            {
                _gameManagerHooked = false;
                return;
            }

            GameManager.I.OnKillCountChanged -= HandleKillCountChanged;
            GameManager.I.OnWaveStarted -= HandleWaveStarted;
            _gameManagerHooked = false;
        }

        private void HandlePlayerReady(PlayerController controller)
        {
            ResetSession();
            _sessionActive = true;

            if (controller != null)
            {
                PlayerStats stats = controller.GetComponent<PlayerStats>();
                XPWallet wallet = controller.GetComponentInChildren<XPWallet>();
                CharacterAbilityController abilityController = controller.GetComponent<CharacterAbilityController>();
                PlayerCombatEffectController combatEffects = controller.GetComponent<PlayerCombatEffectController>();
                CharacterProgressionService.ApplyPermanentUpgrades(
                    CharacterSelectionState.SelectedCharacter,
                    stats,
                    wallet,
                    abilityController,
                    combatEffects);

                Health health = controller.GetComponentInChildren<Health>();
                if (health && _trackedHealth.Add(health))
                {
                    health.OnDeath += HandlePlayerDeath;
                }
            }
        }

        private void HandlePlayerDeath()
        {
            TryFinalizeRun();
        }

        private void HandleKillCountChanged(int kills)
        {
            _killCount = Mathf.Max(0, kills);
        }

        private void HandleWaveStarted(int wave)
        {
            _highestWave = Mathf.Max(_highestWave, wave);
        }

        private void HandleSceneChanged(Scene previous, Scene next)
        {
            if (previous.name != next.name)
            {
                TryFinalizeRun();
            }
        }

        public void TryFinalizeRun()
        {
            if (!_sessionActive || _finalized)
            {
                return;
            }

            _finalized = true;
            CharacterDefinition character = CharacterSelectionState.SelectedCharacter;
            CharacterProgressionService.AddRunExperience(character, _highestWave, _killCount);
        }

        private void ResetSession()
        {
            _highestWave = 0;
            _killCount = 0;
            _finalized = false;
            _sessionActive = false;
            XPOrb.SetGlobalAttractionMultipliers(1f, 1f);
        }

        private void UnsubscribeHealth()
        {
            foreach (Health health in _trackedHealth)
            {
                if (health)
                {
                    health.OnDeath -= HandlePlayerDeath;
                }
            }

            _trackedHealth.Clear();
        }
    }
}
