using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

namespace FF
{
    public class GameLauncher : MonoBehaviour
    {
        public static GameLauncher Instance { get; private set; }

        [SerializeField] private string gameplayScene = "Main";

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
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        public void LaunchSinglePlayer()
        {
            // Ensure we're not connected if we're trying to launch single player
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
            SceneManager.LoadScene(gameplayScene, LoadSceneMode.Single);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == gameplayScene)
            {
                // Check if we are NOT in a networked session (either as host or client)
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                {
                    // Find the spawner in the newly loaded scene and tell it to spawn the player
                    PlayerSpawner spawner = FindObjectOfType<PlayerSpawner>();
                    if (spawner != null)
                    {
                        spawner.SpawnSinglePlayer();
                    }
                    else
                    {
                        Debug.LogError("PlayerSpawner not found in the gameplay scene! Cannot spawn player for single-player mode.");
                    }
                }
            }
        }
    }
}
