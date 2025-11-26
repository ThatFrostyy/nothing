using UnityEngine;

namespace FF
{
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private SceneFlowController sceneFlow;

        void Awake()
        {
            if (!sceneFlow)
            {
                sceneFlow = FindAnyObjectByType<SceneFlowController>();
            }
        }

        public void StartGame()
        {
            if (sceneFlow)
            {
                sceneFlow.LoadGameplayScene();
            }
        }

        public void QuitGame()
        {
            SceneFlowController.QuitGame();
        }
    }
}
