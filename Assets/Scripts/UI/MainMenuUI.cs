using UnityEngine;

namespace FF
{
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private SceneFlowController sceneFlow;
        [SerializeField] private GameObject characterSelectionPanel;

        void Awake()
        {
            sceneFlow = sceneFlow ? sceneFlow : SceneFlowController.EnsureInstance();
        }

        public void StartGame()
        {
            sceneFlow = sceneFlow ? sceneFlow : SceneFlowController.EnsureInstance();

            if (sceneFlow)
                sceneFlow.LoadGameplayScene();
        }

        public void QuitGame()
        {
            SceneFlowController.QuitGame();
        }

        public void ToggleCharacterSelection(bool visible)
        {
            if (characterSelectionPanel)
            {
                characterSelectionPanel.SetActive(visible);
            }
        }
    }
}
