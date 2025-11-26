using System.Collections;
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
        [Header("Defaults")]
        [SerializeField] private CharacterDefinition defaultCharacter;
        [SerializeField] private HatDefinition defaultHat;
        [SerializeField] private Weapon defaultWeapon;
        [SerializeField] private bool applyDefaultSelection = true;

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

        public static void PlayGame()
        {
            Instance?.LoadGameplayScene();
        }

        public void LoadMainMenuScene()
        {
            ResetPersistentState();
            SetCursorVisibility(true);
            LoadScene(mainMenuSceneName);
        }

        public void LoadGameplayScene()
        {
            ApplyDefaultCharacterSelection();
            LoadScene(gameplaySceneName);
        }

        public void ReloadCurrentScene()
        {
            if (GameManager.I != null)
            {
                GameManager.I.ResetGameState();
            }
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
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

        void ApplyDefaultCharacterSelection()
        {
            if (!applyDefaultSelection || CharacterSelectionState.HasSelection)
            {
                return;
            }

            CharacterSelectionState.SetSelection(defaultCharacter, defaultHat, defaultWeapon);
        }

        static void SetCursorVisibility(bool visible)
        {
            Cursor.visible = visible;
            Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Confined;
        }

        private static void LoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return;

            Time.timeScale = 1f;
            Instance.StartCoroutine(LoadSceneAsyncRoutine(sceneName));
        }

        private static IEnumerator LoadSceneAsyncRoutine(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            op.allowSceneActivation = true;

            while (!op.isDone)
                yield return null;
        }

        IEnumerator Start()
        {
            yield return null;
            //yield return Prewarm();
        }

        IEnumerator Prewarm()
        {
            yield return PrewarmFolder<GameObject>("Prefabs");
            yield return PrewarmFolder<Sprite>("Art");
            yield return PrewarmFolder<AudioClip>("Audio");

            Debug.Log("Prewarming complete!");
        }

        IEnumerator PrewarmFolder<T>(string folderPath) where T : Object
        {
            T[] assets = Resources.LoadAll<T>(folderPath);

            foreach (var asset in assets)
            {
                var req = Resources.LoadAsync<T>(folderPath + "/" + asset.name);
                yield return req;
            }
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
