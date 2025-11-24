using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FF
{
    [System.Serializable]
    public class UpgradeSpawnConfig
    {
        public UpgradePickup pickupPrefab;
        [SerializeField, Min(0f)] public float initialSpawnDelay = 0f;
        [SerializeField] public bool spawnOnStart = true;
        [SerializeField] public bool useRespawnDelay = true;
        [SerializeField, Min(0f)] public float respawnDelay = 1f;
        [SerializeField] public AnimationCurve spawnIntervalCurve = AnimationCurve.Linear(0f, 15f, 300f, 5f);
        [SerializeField] public AnimationCurve pickupsPerSpawnCurve = AnimationCurve.Constant(0f, 1f, 1f);
        [SerializeField] public AnimationCurve maxActiveCurve = AnimationCurve.Constant(0f, 1f, 1f);
    }

    public class UpgradePickupSpawner : MonoBehaviour
    {
        [SerializeField] private UpgradeSpawnConfig[] pickupConfigs;
        [SerializeField] private Transform player;
        [SerializeField] private Camera sceneCamera;
        [SerializeField, Min(0f)] private float offscreenPadding = 2f;
        [SerializeField, Min(0f)] private float minSpawnRadius = 6f;

        private readonly List<SpawnState> spawnStates = new();
        private readonly Dictionary<UpgradePickup, SpawnState> pickupToState = new();

        private class SpawnState
        {
            public UpgradeSpawnConfig Config { get; }
            public List<UpgradePickup> ActivePickups { get; } = new();
            public Coroutine RespawnRoutine { get; set; }
            public Coroutine SpawnLoopRoutine { get; set; }

            public SpawnState(UpgradeSpawnConfig config)
            {
                Config = config;
            }
        }

        void Awake()
        {
            if (!ValidateDependencies())
            {
                Debug.LogError($"{nameof(UpgradePickupSpawner)} on {name} disabled due to missing dependencies.", this);
                enabled = false;
                return;
            }

            InitializeSpawnStates();
        }

        void OnValidate()
        {
            if (!player)
            {
                var playerObj = GameObject.FindWithTag("Player");
                if (playerObj)
                {
                    player = playerObj.transform;
                }
            }

            if (!sceneCamera)
            {
                sceneCamera = GetComponentInParent<Camera>();
            }
        }

        void OnDisable()
        {
            foreach (var kvp in pickupToState)
            {
                if (kvp.Key)
                {
                    Unsubscribe(kvp.Key);
                }
            }

            pickupToState.Clear();

            for (int i = 0; i < spawnStates.Count; i++)
            {
                SpawnState state = spawnStates[i];

                for (int j = 0; j < state.ActivePickups.Count; j++)
                {
                    if (state.ActivePickups[j])
                    {
                        Unsubscribe(state.ActivePickups[j]);
                    }
                }

                state.ActivePickups.Clear();

                if (state.RespawnRoutine != null)
                {
                    StopCoroutine(state.RespawnRoutine);
                    state.RespawnRoutine = null;
                }

                if (state.SpawnLoopRoutine != null)
                {
                    StopCoroutine(state.SpawnLoopRoutine);
                    state.SpawnLoopRoutine = null;
                }
            }
        }

        private void InitializeSpawnStates()
        {
            spawnStates.Clear();

            if (pickupConfigs == null || pickupConfigs.Length == 0)
            {
                return;
            }

            for (int i = 0; i < pickupConfigs.Length; i++)
            {
                UpgradeSpawnConfig config = pickupConfigs[i];
                if (config == null || config.pickupPrefab == null)
                {
                    continue;
                }

                var state = new SpawnState(config);
                spawnStates.Add(state);

                float initialDelay = Mathf.Max(0f, config.initialSpawnDelay);
                bool spawnImmediately = config.spawnOnStart;

                state.SpawnLoopRoutine = StartCoroutine(SpawnLoop(state, initialDelay, spawnImmediately));
            }
        }

        private IEnumerator SpawnLoop(SpawnState state, float initialDelay, bool spawnImmediately)
        {
            if (spawnImmediately)
            {
                if (initialDelay > 0f)
                {
                    yield return new WaitForSeconds(initialDelay);
                }

                AttemptSpawn(state);
            }
            else if (initialDelay > 0f)
            {
                yield return new WaitForSeconds(initialDelay);
            }

            while (true)
            {
                float interval = EvaluateInterval(state.Config.spawnIntervalCurve);
                if (interval > 0f)
                {
                    yield return new WaitForSeconds(interval);
                }
                else
                {
                    yield return null;
                }

                AttemptSpawn(state);
            }
        }

        private void AttemptSpawn(SpawnState state)
        {
            if (player == null || state == null || state.Config == null)
            {
                return;
            }

            int maxActive = Mathf.Max(0, EvaluateCount(state.Config.maxActiveCurve, 1));
            int pickupsPerSpawn = Mathf.Max(1, EvaluateCount(state.Config.pickupsPerSpawnCurve, 1));
            int availableSlots = Mathf.Max(0, maxActive - state.ActivePickups.Count);
            int toSpawn = Mathf.Min(pickupsPerSpawn, availableSlots);

            for (int i = 0; i < toSpawn; i++)
            {
                SpawnPickup(state);
            }
        }

        private int EvaluateCount(AnimationCurve curve, int fallback)
        {
            if (curve == null || curve.length == 0)
            {
                return fallback;
            }

            return Mathf.RoundToInt(curve.Evaluate(Time.timeSinceLevelLoad));
        }

        private float EvaluateInterval(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0)
            {
                return 0.1f;
            }

            return Mathf.Max(0.05f, curve.Evaluate(Time.timeSinceLevelLoad));
        }

        private void SpawnPickup(SpawnState state)
        {
            Vector3 spawnPos = GetSpawnPosition();
            UpgradePickup pickupPrefab = state.Config.pickupPrefab;
            if (!pickupPrefab)
            {
                return;
            }

            var pickup = Instantiate(pickupPrefab, spawnPos, Quaternion.identity);
            state.ActivePickups.Add(pickup);
            pickupToState[pickup] = state;

            pickup.OnCollected += HandlePickupFinished;
            pickup.OnExpired += HandlePickupFinished;
        }

        private void HandlePickupFinished(UpgradePickup pickup)
        {
            if (!pickup)
            {
                return;
            }

            if (pickupToState.TryGetValue(pickup, out var state))
            {
                Unsubscribe(pickup);
                state.ActivePickups.Remove(pickup);
                pickupToState.Remove(pickup);

                if (state.RespawnRoutine != null)
                {
                    StopCoroutine(state.RespawnRoutine);
                }

                if (state.Config.useRespawnDelay)
                {
                    state.RespawnRoutine = StartCoroutine(RespawnAfterDelay(state));
                }
            }
            else
            {
                Unsubscribe(pickup);
            }
        }

        private IEnumerator RespawnAfterDelay(SpawnState state)
        {
            float waitTime = Mathf.Max(0f, state.Config.respawnDelay);
            if (waitTime > 0f)
            {
                yield return new WaitForSeconds(waitTime);
            }

            AttemptSpawn(state);
            state.RespawnRoutine = null;
        }

        private void Unsubscribe(UpgradePickup pickup)
        {
            pickup.OnCollected -= HandlePickupFinished;
            pickup.OnExpired -= HandlePickupFinished;
        }

        private Vector3 GetSpawnPosition()
        {
            Vector3 center = player ? player.position : Vector3.zero;
            Camera cam = sceneCamera;

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

        private bool ValidateDependencies()
        {
            bool ok = true;

            if (!player)
            {
                Debug.LogError("Missing player reference.", this);
                ok = false;
            }

            if (!sceneCamera)
            {
                Debug.LogError("Missing scene camera reference.", this);
                ok = false;
            }

            return ok;
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
