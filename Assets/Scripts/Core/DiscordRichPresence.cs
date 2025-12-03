using UnityEngine;
using Discord.Sdk;

namespace FF
{
    public class DiscordRichPresence : MonoBehaviour
    {
        [SerializeField] private ulong applicationId;
        [SerializeField] private string largeImageKey = "logo";
        [SerializeField] private string largeImageText = "MyGame";

        private Client client;
        private long startTimestamp;

        void Start()
        {
            client = new Client();            // no login flow
            startTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            UpdatePresence("In Menu", "Idle");
        }

        void Update()
        {
            client.RunCallbacks();
        }

        public void UpdatePresence(string details, string state)
        {
            var activity = new Activity();
            activity.SetType(ActivityTypes.Playing);
            activity.SetDetails(details);
            activity.SetState(state);

            // optional: set timestamps
            var ts = new ActivityTimestamps();
            ts.SetStart(startTimestamp);
            activity.SetTimestamps(ts);

            var assets = new ActivityAssets();
            assets.SetLargeImage(largeImageKey);
            assets.SetLargeText(largeImageText);
            activity.SetAssets(assets);

            client.UpdateRichPresence(activity, result =>
            {
                if (!result.Successful())
                    Debug.LogWarning("Discord RPC failed: " + result.Error());
            });
        }

        void OnApplicationQuit()
        {
            client.Dispose();
        }
    }
}
