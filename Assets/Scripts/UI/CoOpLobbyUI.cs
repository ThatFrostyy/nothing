using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace FF
{
    public class CoOpLobbyUI : MonoBehaviour
    {
        [SerializeField] private Button readyButton;
        [SerializeField] private Button startGameButton;

        private void Start()
        {
            readyButton.onClick.AddListener(OnReadyButtonClicked);
            startGameButton.onClick.AddListener(OnStartGameButtonClicked);
        }

        private void OnReadyButtonClicked()
        {
            // Logic for when a client clicks the "Ready" button will go here.
            Debug.Log("Ready button clicked!");
        }

        private void OnStartGameButtonClicked()
        {
            LobbyManager.Instance.StartGame();
        }
    }
}
