using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        public void LaunchClient()
        {
            NetworkManager.Singleton.StartClient();
        }

        public void LaunchSinglePlayer()
        {
            SceneManager.LoadScene(gameplayScene, LoadSceneMode.Single);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == gameplayScene && NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                PlayerSpawner.Instance.SpawnSinglePlayer();
            }
        }
    }
}
