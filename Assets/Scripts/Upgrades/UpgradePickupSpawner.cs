using System.Collections;
using UnityEngine;

namespace FF
{
    public class UpgradePickupSpawner : MonoBehaviour
    {
        [SerializeField] private UpgradePickup[] pickupPrefabs;
        [SerializeField] private Transform player;
        [SerializeField, Min(0f)] private float offscreenPadding = 2f;
        [SerializeField, Min(0f)] private float minSpawnRadius = 6f;
        [Header("Spawn Timing")]
        [SerializeField, Min(0f)] private float initialSpawnDelay = 0f;
        [SerializeField, Min(0f)] private float spawnInterval = 15f;
        [SerializeField, Min(0f)] private float respawnDelay = 1f;
        [SerializeField] private bool spawnOnStart = true;

        [Header("Spawn Counts")]
        [SerializeField, Min(1)] private int pickupsPerSpawn = 1;
        [SerializeField, Min(1)] private int maxActivePickups = 1;

        private readonly System.Collections.Generic.List<UpgradePickup> activePickups = new();
        private Coroutine respawnRoutine;
        private Coroutine spawnLoopRoutine;

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

            if (spawnOnStart)
            {
                SpawnWave();
            }

            if (spawnInterval > 0f || initialSpawnDelay > 0f)
            {
                spawnLoopRoutine = StartCoroutine(SpawnLoop());
            }
        }

        void OnDisable()
        {
            for (int i = 0; i < activePickups.Count; i++)
            {
                if (activePickups[i])
                {
                    Unsubscribe(activePickups[i]);
                }
            }

            activePickups.Clear();

            if (respawnRoutine != null)
            {
                StopCoroutine(respawnRoutine);
                respawnRoutine = null;
            }

            if (spawnLoopRoutine != null)
            {
                StopCoroutine(spawnLoopRoutine);
                spawnLoopRoutine = null;
            }
        }

        private IEnumerator SpawnLoop()
        {
            if (initialSpawnDelay > 0f)
            {
                yield return new WaitForSeconds(initialSpawnDelay);
                SpawnWave();
            }

            while (spawnInterval > 0f)
            {
                yield return new WaitForSeconds(spawnInterval);
                SpawnWave();
            }
        }

        private void SpawnWave()
        {
            for (int i = 0; i < pickupsPerSpawn && activePickups.Count < maxActivePickups; i++)
            {
                SpawnPickup();
            }
        }

        private void SpawnPickup()
        {
            if (pickupPrefabs == null || pickupPrefabs.Length == 0 || player == null)
            {
                return;
            }

            Vector3 spawnPos = GetSpawnPosition();
            var pickupPrefab = GetRandomPickupPrefab();
            if (!pickupPrefab)
            {
                return;
            }

            var pickup = Instantiate(pickupPrefab, spawnPos, Quaternion.identity);
            activePickups.Add(pickup);
            pickup.OnCollected += HandlePickupFinished;
            pickup.OnExpired += HandlePickupFinished;
        }

        private void HandlePickupFinished(UpgradePickup pickup)
        {
            if (pickup)
            {
                Unsubscribe(pickup);
            }

            activePickups.Remove(pickup);

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

            SpawnWave();
            respawnRoutine = null;
        }

        private void Unsubscribe(UpgradePickup pickup)
        {
            pickup.OnCollected -= HandlePickupFinished;
            pickup.OnExpired -= HandlePickupFinished;
        }

        private UpgradePickup GetRandomPickupPrefab()
        {
            int index = Random.Range(0, pickupPrefabs.Length);
            return pickupPrefabs[index];
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
