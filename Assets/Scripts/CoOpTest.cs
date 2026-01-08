using UnityEngine;
using UnityEngine.UI;

namespace FF
{
    public class CoOpTest : MonoBehaviour
    {
        public Button hostButton;
        public LobbyManager lobbyManager;

        private void Start()
        {
            hostButton.onClick.AddListener(() =>
            {
                lobbyManager.HostLobby();
            });
        }
    }
}