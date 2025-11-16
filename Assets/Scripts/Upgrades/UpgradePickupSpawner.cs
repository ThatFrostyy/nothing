using System.Collections;
using UnityEngine;

namespace FF
{
    public class UpgradePickupSpawner : MonoBehaviour
    {
        [SerializeField] private UpgradePickup pickupPrefab;
        [SerializeField] private UpgradePickupEffect[] availableEffects;
        [SerializeField] private Transform player;
        [SerializeField, Min(0f)] private float offscreenPadding = 2f;
        [SerializeField, Min(0f)] private float minSpawnRadius = 6f;
        [SerializeField, Min(0f)] private float respawnDelay = 1f;

        private UpgradePickup activePickup;
        private Coroutine respawnRoutine;

        void Start()
        {
            if (!player)
            {
                var playerObj = GameObject.FindWithTag("Player");
                if (playerObj)
                {
                    player = playerObj.transform;
                }
            }

            SpawnPickup();
        }

        void OnDisable()
        {
            if (activePickup)
            {
                Unsubscribe(activePickup);
            }

            if (respawnRoutine != null)
            {
                StopCoroutine(respawnRoutine);
            }
        }

        private void SpawnPickup()
        {
            if (pickupPrefab == null || player == null || availableEffects == null || availableEffects.Length == 0)
            {
                return;
            }

            Vector3 spawnPos = GetSpawnPosition();
            activePickup = Instantiate(pickupPrefab, spawnPos, Quaternion.identity);
            activePickup.SetEffect(GetRandomEffect());
            activePickup.OnCollected += HandlePickupFinished;
            activePickup.OnExpired += HandlePickupFinished;
        }

        private void HandlePickupFinished(UpgradePickup pickup)
        {
            if (pickup)
            {
                Unsubscribe(pickup);
            }

            activePickup = null;

            if (respawnRoutine != null)
            {
                StopCoroutine(respawnRoutine);
            }

            respawnRoutine = StartCoroutine(RespawnAfterDelay());
        }

        private IEnumerator RespawnAfterDelay()
        {
            if (respawnDelay > 0f)
            {
                yield return new WaitForSeconds(respawnDelay);
            }

            SpawnPickup();
            respawnRoutine = null;
        }

        private void Unsubscribe(UpgradePickup pickup)
        {
            pickup.OnCollected -= HandlePickupFinished;
            pickup.OnExpired -= HandlePickupFinished;
        }

        private UpgradePickupEffect GetRandomEffect()
        {
            int index = Random.Range(0, availableEffects.Length);
            return availableEffects[index];
        }

        private Vector3 GetSpawnPosition()
        {
            Vector3 center = player ? player.position : Vector3.zero;
            Camera cam = Camera.main;

            if (cam && cam.orthographic)
            {
                float halfHeight = cam.orthographicSize;
                float halfWidth = halfHeight * cam.aspect;
                float distance = Mathf.Sqrt(halfHeight * halfHeight + halfWidth * halfWidth) + offscreenPadding;
                distance = Mathf.Max(distance, minSpawnRadius);
                return center + (Vector3)(GetDirection() * distance);
            }

            float fallbackRadius = Mathf.Max(minSpawnRadius, offscreenPadding + 5f);
            return center + (Vector3)(GetDirection() * fallbackRadius);
        }

        private static Vector2 GetDirection()
        {
            Vector2 dir = Random.insideUnitCircle;
            if (dir == Vector2.zero)
            {
                dir = Vector2.right;
            }

            return dir.normalized;
        }
    }
}
