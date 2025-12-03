using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Discord.Sdk;

namespace FF
{
    public class DiscordRichPresence : MonoBehaviour, ISceneReferenceHandler
    {
        public static DiscordRichPresence Instance { get; private set; }

        [Header("Discord App")]
        [SerializeField] private ulong applicationId;
        [SerializeField] private bool persistAcrossScenes = true;

        [Header("Scene Names")]
        [SerializeField] private string menuSceneName = "MainMenu";
        [SerializeField] private string gameplaySceneName = "Main";

        [Header("Menu Presence")]
        [SerializeField] private string menuDetails = "In Menu";
        [SerializeField] private string menuState = "Idle";

        [Header("Gameplay Presence")]
        [SerializeField] private string gameplayDetailsFormat = "Wave {0}";
        [SerializeField] private string gameplayStateFormat = "{0} kills";

        [Header("Rich Presence Art")]
        [SerializeField] private string largeImageKey = "logo";
        [SerializeField] private string largeImageText = "MyGame";
        [SerializeField] private string smallImageKey = string.Empty;
        [SerializeField] private string smallImageText = string.Empty;

        private Client client;
        private ulong startTimestamp;
        private bool hasCustomPresence;
        private string customDetails;
        private string customState;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (persistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }

            SceneManager.activeSceneChanged += HandleSceneChanged;
        }

        void OnEnable()
        {
            SceneReferenceRegistry.Register(this);
        }

        void OnDisable()
        {
            SceneReferenceRegistry.Unregister(this);
            UnsubscribeFromGameplay();
        }

        void Start()
        {
            InitializeClient();
            RefreshPresence();
            TrySubscribeToGameplay();
        }

        void OnDestroy()
        {
            SceneManager.activeSceneChanged -= HandleSceneChanged;
            DisposeClient();
        }

        public void ClearSceneReferences()
        {
            UnsubscribeFromGameplay();
        }

        public void SetCustomPresence(string details, string state)
        {
            hasCustomPresence = true;
            customDetails = details;
            customState = state;
            RefreshPresence();
        }

        public void ClearCustomPresence()
        {
            hasCustomPresence = false;
            RefreshPresence();
        }

        void InitializeClient()
        {
            DisposeClient();

            if (applicationId == 0)
            {
                Debug.LogWarning("DiscordRichPresence: No application ID set.");
                return;
            }

            try
            {
                client = new Client();
                client.SetApplicationId(applicationId);
                startTimestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"DiscordRichPresence: Failed to initialize Discord client. {ex.Message}");
                client = null;
            }
        }

        void DisposeClient()
        {
            if (client != null)
            {
                client.Dispose();
                client = null;
            }
        }

        void HandleSceneChanged(Scene current, Scene next)
        {
            hasCustomPresence = false;
            startTimestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            UnsubscribeFromGameplay();
            TrySubscribeToGameplay();
            RefreshPresence();
        }

        void TrySubscribeToGameplay()
        {
            var gameManager = GameManager.I;
            if (gameManager == null)
            {
                Debug.LogWarning("DiscordRichPresence: GameManager instance not found.");
                return;
            }

            if (SceneManager.GetActiveScene().name != gameplaySceneName)
            {
                return;
            }

            Debug.Log("DiscordRichPresence: Subscribing to gameplay events.");
            gameManager.OnWaveStarted += HandleWaveStarted;
            gameManager.OnKillCountChanged += HandleKillCountChanged;
        }

        void UnsubscribeFromGameplay()
        {
            var gameManager = GameManager.I;
            if (gameManager == null)
            {
                return;
            }

            gameManager.OnWaveStarted -= HandleWaveStarted;
            gameManager.OnKillCountChanged -= HandleKillCountChanged;
        }

        void HandleWaveStarted(int _)
        {
            RefreshPresence();
        }

        void HandleKillCountChanged(int _)
        {
            RefreshPresence();
        }

        void RefreshPresence()
        {
            if (client == null)
            {
                return;
            }

            string details;
            string state;

            if (hasCustomPresence)
            {
                details = customDetails;
                state = customState;
            }
            else if (SceneManager.GetActiveScene().name == gameplaySceneName && GameManager.I != null)
            {
                details = string.Format(gameplayDetailsFormat, GameManager.I.Wave);
                state = string.Format(gameplayStateFormat, GameManager.I.KillCount);
            }
            else
            {
                details = menuDetails;
                state = menuState;
            }

            UpdatePresence(details, state);
        }

        void UpdatePresence(string details, string state)
        {
            var activity = new Activity();
            activity.SetName(Application.productName);
            activity.SetType(ActivityTypes.Playing);
            activity.SetDetails(details);
            activity.SetState(state);

            var ts = new ActivityTimestamps();
            ts.SetStart(startTimestamp);
            activity.SetTimestamps(ts);

            var assets = new ActivityAssets();
            assets.SetLargeImage(largeImageKey);
            assets.SetLargeText(largeImageText);

            if (!string.IsNullOrWhiteSpace(smallImageKey))
            {
                assets.SetSmallImage(smallImageKey);
                if (!string.IsNullOrWhiteSpace(smallImageText))
                {
                    assets.SetSmallText(smallImageText);
                }
            }

            activity.SetAssets(assets);

            client.UpdateRichPresence(activity, result =>
            {
                if (!result.Successful())
                    Debug.LogWarning("Discord RPC failed: " + result.Error());
            });
        }

        void OnApplicationQuit()
        {
            DisposeClient();
        }
    }
}
