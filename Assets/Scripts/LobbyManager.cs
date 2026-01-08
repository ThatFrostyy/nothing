using Steamworks;
using UnityEngine;
using Unity.Netcode;
using Netcode.Transports;


namespace FF
{
    public class LobbyManager : MonoBehaviour
    {
        private const string HostAddressKey = "HostAddress";

        public void HostLobby()
        {
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4);
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

        private void OnLobbyEntered(LobbyEntered_t callback)
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
        protected Callback<LobbyEntered_t> LobbyEntered;

        private void Start()
        {
            if (!SteamManager.Initialized) return;

            LobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            GameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
            LobbyEntered = Callback<LobbyEntered_t>.Create(OnLobbyEntered);
        }
    }
}
