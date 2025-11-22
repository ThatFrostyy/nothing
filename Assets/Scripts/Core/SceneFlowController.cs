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

        private void LoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            Time.timeScale = 1f;
            StartCoroutine(LoadSceneRoutine(sceneName));
        }

        private System.Collections.IEnumerator LoadSceneRoutine(string sceneName)
        {
            LoadingScreen.Instance.Show();

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            operation.allowSceneActivation = false;

            while (!operation.isDone)
            {
                float progress = Mathf.Clamp01(operation.progress / 0.9f);
                LoadingScreen.Instance.UpdateProgress(progress);

                if (operation.progress >= 0.9f)
                {
                    operation.allowSceneActivation = true;
                }

                yield return null;
            }

            LoadingScreen.Instance.Hide();
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
