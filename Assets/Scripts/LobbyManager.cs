using Steamworks;
using UnityEngine;
using Unity.Netcode;
using Netcode.Transports;
using System;

namespace FF
{
    public class LobbyManager : MonoBehaviour
    {
        public static LobbyManager Instance { get; private set; }
        public static event Action OnClientJoinedLobby;

        private const string HostAddressKey = "HostAddress";

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
                NetworkManager.Singleton.SceneManager.LoadScene("Main", UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
        }

        private void OnLobbyCreated(LobbyCreated_t callback)
        {
            if (callback.m_eResult != EResult.k_EResultOK)
            {
                return;
            }

            NetworkManager.Singleton.StartHost();

            SteamMatchmaking.SetLobbyData(
                new CSteamID(callback.m_ulSteamIDLobby),
                HostAddressKey,
                SteamUser.GetSteamID().ToString());
        }

        private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
        {
            SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
        }

        private void OnLobbyEntered(LobbyEnter_t callback)
        {
            if (NetworkManager.Singleton.IsHost) return;

            OnClientJoinedLobby?.Invoke();

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
