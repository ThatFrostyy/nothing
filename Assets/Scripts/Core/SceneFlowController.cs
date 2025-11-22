using UnityEngine;
using UnityEngine.SceneManagement;

namespace FF
{
    public class SceneFlowController : MonoBehaviour
    {
        public static SceneFlowController Instance { get; private set; }

        public string MainMenuSceneName => mainMenuSceneName;
        public string GameplaySceneName => gameplaySceneName;

        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private string gameplaySceneName = "Main";
        [SerializeField] private bool persistAcrossScenes = true;

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

        public void LoadMainMenuScene()
        {
            LoadScene(mainMenuSceneName);
        }

        public void LoadGameplayScene()
        {
            LoadScene(gameplaySceneName);
        }

        public void ReloadCurrentScene()
        {
            LoadScene(SceneManager.GetActiveScene().name);
        }

        public AsyncOperation LoadSceneAsync(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return null;
            }

            Time.timeScale = 1f;
            return SceneManager.LoadSceneAsync(sceneName);
        }

        private static void LoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }

        public static void QuitGame()
        {
    #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
    #else
            Application.Quit();
    #endif
        }
    }
}
