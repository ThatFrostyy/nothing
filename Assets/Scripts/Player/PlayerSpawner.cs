using Unity.Netcode;
using UnityEngine;

namespace FF
{
    public class PlayerSpawner : NetworkBehaviour
    {
        public static PlayerSpawner Instance { get; private set; }

        [SerializeField] private GameObject playerPrefab;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                SpawnPlayer(NetworkManager.Singleton.LocalClientId);
                NetworkManager.Singleton.OnClientConnectedCallback += SpawnPlayer;
            }
        }

        private void SpawnPlayer(ulong clientId)
        {
            if (!IsServer) return;

            GameObject playerInstance = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            playerInstance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
        }

        public void SpawnSinglePlayer()
        {
            Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= SpawnPlayer;
            }
        }
    }
}
