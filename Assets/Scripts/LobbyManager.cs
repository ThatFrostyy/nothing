using Steamworks;
using UnityEngine;
using Unity.Netcode;
using Netcode.Transports;
using UnityEngine.SceneManagement;

namespace FF
{
    public class LobbyManager : MonoBehaviour
    {
        public static LobbyManager Instance { get; private set; }

        private const string HostAddressKey = "HostAddress";
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

        public void HostLobby()
        {
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4);
        }

        public void StartGame()
        {
            if (NetworkManager.Singleton.IsHost)
            {
                NetworkManager.Singleton.SceneManager.LoadScene(gameplayScene, LoadSceneMode.Single);
            }
        }

        private void OnLobbyCreated(LobbyCreated_t callback)
        {
            if (callback.m_eResult != EResult.k_EResultOK)
            {
                return;
            }

            SteamMatchmaking.SetLobbyData(
                new CSteamID(callback.m_ulSteamIDLobby),
                HostAddressKey,
                SteamUser.GetSteamID().ToString());

            NetworkManager.Singleton.StartHost();
        }

        private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
        {
            SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
        }

        private void OnLobbyEntered(LobbyEnter_t callback)
        {
            if (NetworkManager.Singleton.IsHost) return;

            string hostAddress = SteamMatchmaking.GetLobbyData(
                new CSteamID(callback.m_ulSteamIDLobby),
                HostAddressKey);

            var transport = NetworkManager.Singleton.GetComponent<SteamNetworkingSocketsTransport>();
            transport.ConnectToSteamID = ulong.Parse(hostAddress);
            NetworkManager.Singleton.StartClient();
        }

        protected Callback<LobbyCreated_t> LobbyCreated;
        protected Callback<GameLobbyJoinRequested_t> GameLobbyJoinRequested;
        protected Callback<LobbyEnter_t> LobbyEntered;

        private void Start()
        {
            if (!SteamManager.Initialized) return;

            LobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            GameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
            LobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        }
    }
}
