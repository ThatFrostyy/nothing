using System;
using Discord;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FF
{
    public class DiscordRichPresence : MonoBehaviour
    {
        public static DiscordRichPresence Instance { get; private set; }

        [Header("Discord Application")]
        [SerializeField] private long applicationId;
        [SerializeField] private bool persistAcrossScenes = true;

        [Header("Images (configured in the Discord Developer Portal)")]
        [SerializeField] private string largeImageKey = "logo";
        [SerializeField] private string largeImageText = "Nothing";
        [SerializeField] private string smallImageKey = string.Empty;
        [SerializeField] private string smallImageText = string.Empty;

        [Header("Presence Text")]
        [SerializeField] private string menuDetails = "Browsing the menu";
        [SerializeField] private string menuState = "Idle";
        [SerializeField] private string gameplayDetailsFormat = "Surviving wave {0}";
        [SerializeField] private string gameplayStateFormat = "Wave {0} | {1} kills";

        private Discord.Discord discord;
        private ActivityManager activityManager;
        private long startTimestamp;

        private GameManager cachedGameManager;

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
        }

        void OnEnable()
        {
            SceneManager.activeSceneChanged += HandleSceneChanged;
            TryAttachGameManager(SceneManager.GetActiveScene());
            EnsureInitialized();
            RefreshPresence(SceneManager.GetActiveScene());
        }

        void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleSceneChanged;
            DetachGameManager();
            Shutdown();
        }

        void Update()
        {
            discord?.RunCallbacks();
        }

        public void SetCustomPresence(string details, string state)
        {
            if (activityManager == null)
                return;

            var activity = BuildBaseActivity();
            activity.Details = details;
            activity.State = state;
            PushActivity(activity);
        }

        void EnsureInitialized()
        {
            if (discord != null)
                return;

            if (applicationId == 0)
            {
                Debug.LogWarning("DiscordRichPresence is missing an Application ID.");
                return;
            }

            startTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            discord = new Discord.Discord(applicationId, (ulong)CreateFlags.NoRequireDiscord);
            activityManager = discord.GetActivityManager();
        }

        void HandleSceneChanged(Scene oldScene, Scene newScene)
        {
            TryAttachGameManager(newScene);
            RefreshPresence(newScene);
        }

        void TryAttachGameManager(Scene activeScene)
        {
            if (activeScene.name != "Main")
            {
                DetachGameManager();
                return;
            }

            var gameManager = GameManager.I;
            if (gameManager == cachedGameManager)
            {
                return;
            }

            DetachGameManager();
            cachedGameManager = gameManager;
            if (cachedGameManager == null)
            {
                return;
            }

            cachedGameManager.OnWaveStarted += HandleWaveStarted;
            cachedGameManager.OnKillCountChanged += HandleKillCountChanged;
        }

        void DetachGameManager()
        {
            if (cachedGameManager == null)
                return;

            cachedGameManager.OnWaveStarted -= HandleWaveStarted;
            cachedGameManager.OnKillCountChanged -= HandleKillCountChanged;
            cachedGameManager = null;
        }

        void HandleWaveStarted(int wave)
        {
            if (cachedGameManager == null)
                return;

            RefreshGameplayPresence(wave, cachedGameManager.KillCount);
        }

        void HandleKillCountChanged(int killCount)
        {
            if (cachedGameManager == null)
                return;

            RefreshGameplayPresence(cachedGameManager.Wave, killCount);
        }

        void RefreshPresence(Scene activeScene)
        {
            if (activityManager == null)
                return;

            if (activeScene.name == "Main")
            {
                int wave = cachedGameManager != null ? cachedGameManager.Wave : 0;
                int kills = cachedGameManager != null ? cachedGameManager.KillCount : 0;
                RefreshGameplayPresence(wave, kills);
            }
            else
            {
                var activity = BuildBaseActivity();
                activity.Details = menuDetails;
                activity.State = menuState;
                PushActivity(activity);
            }
        }

        void RefreshGameplayPresence(int wave, int killCount)
        {
            if (activityManager == null)
                return;

            var activity = BuildBaseActivity();
            activity.Details = string.Format(gameplayDetailsFormat, Mathf.Max(1, wave));
            activity.State = string.Format(gameplayStateFormat, Mathf.Max(1, wave), Mathf.Max(0, killCount));
            PushActivity(activity);
        }

        Activity BuildBaseActivity()
        {
            var activity = new Activity
            {
                Timestamps = { Start = startTimestamp }
            };

            activity.Assets.LargeImage = largeImageKey;
            activity.Assets.LargeText = largeImageText;
            activity.Assets.SmallImage = smallImageKey;
            activity.Assets.SmallText = smallImageText;

            return activity;
        }

        void PushActivity(Activity activity)
        {
            activityManager.UpdateActivity(activity, result =>
            {
                if (result != Result.Ok)
                {
                    Debug.LogWarning($"Discord rich presence update failed: {result}");
                }
            });
        }

        void Shutdown()
        {
            discord?.Dispose();
            discord = null;
            activityManager = null;
        }
    }
}
