using Unity.Netcode;
using UnityEngine;

namespace FF
{
    public class ReviveController : NetworkBehaviour
    {
        public float reviveDistance = 2f;
        public KeyCode reviveKey = KeyCode.E;

        private void Update()
        {
            if (!IsOwner) return;

            if (Input.GetKeyDown(reviveKey))
            {
                TryRevive();
            }
        }

        private void TryRevive()
        {
            if (!IsOwner) return;

            var players = FindObjectsOfType<Health>();
            foreach (var playerHealth in players)
            {
                if (playerHealth.CurrentHP <= 0 && Vector3.Distance(transform.position, playerHealth.transform.position) <= reviveDistance)
                {
                    ReviveServerRpc(playerHealth.NetworkObjectId);
                    break;
                }
            }
        }

        [ServerRpc]
        private void ReviveServerRpc(ulong targetId)
        {
            var target = NetworkManager.Singleton.SpawnManager.SpawnedObjects[targetId];
            var health = target.GetComponent<Health>();
            health.Revive();
        }
    }
}
