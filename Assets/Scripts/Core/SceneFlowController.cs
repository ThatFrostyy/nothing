using UnityEngine;
using UnityEngine.SceneManagement;

namespace FF
{
    public class SceneFlowController : MonoBehaviour
    {
        public static SceneFlowController Instance { get; private set; }

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
            ResetPersistentState();
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

        void ResetPersistentState()
        {
            SceneReferenceRegistry.ResetSceneReferences();

            if (GameManager.I != null)
            {
                GameManager.I.ResetGameState();
            }

            if (UpgradeManager.I != null)
            {
                UpgradeManager.I.ResetState();
            }
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
