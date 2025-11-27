using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FF
{
    [System.Serializable]
    public class WeaponCrateSpawnConfig
    {
        public WeaponCrate cratePrefab;
        public WeaponPickup[] pickupPool;
        [SerializeField, Min(0f)] public float initialSpawnDelay = 0f;
        [SerializeField] public bool spawnOnStart = true;
        [SerializeField] public bool useRespawnDelay = true;
        [SerializeField, Min(0f)] public float respawnDelay = 1f;
        [SerializeField] public AnimationCurve spawnIntervalCurve = AnimationCurve.Linear(0f, 15f, 300f, 5f);
        [SerializeField] public AnimationCurve cratesPerSpawnCurve = AnimationCurve.Constant(0f, 1f, 1f);
        [SerializeField] public AnimationCurve maxActiveCurve = AnimationCurve.Constant(0f, 1f, 1f);
        [SerializeField, Min(0f)] public float crateLifetimeSeconds = 30f;
        [SerializeField, Min(0f)] public float pickupLifetimeSeconds = 20f;
        [SerializeField, Min(0f)] public float timeoutSpawnRadius = 4f;
    }

    public class WeaponCrateSpawner : MonoBehaviour
    {
        [SerializeField] private WeaponCrateSpawnConfig[] crateConfigs;
        [SerializeField] private Transform player;
        [SerializeField, Min(0f)] private float offscreenPadding = 2f;
        [SerializeField, Min(0f)] private float minSpawnRadius = 8f;

        private readonly List<SpawnState> spawnStates = new();
        private readonly Dictionary<WeaponCrate, SpawnState> crateToState = new();

        private class SpawnState
        {
            public WeaponCrateSpawnConfig Config { get; }
            public List<WeaponCrate> ActiveCrates { get; } = new();
            public Coroutine RespawnRoutine { get; set; }
            public Coroutine SpawnLoopRoutine { get; set; }

            public SpawnState(WeaponCrateSpawnConfig config)
            {
                Config = config;
            }
        }

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

            InitializeSpawnStates();
        }

        void OnDisable()
        {
            foreach (var kvp in crateToState)
            {
                if (kvp.Key)
                {
                    Unsubscribe(kvp.Key);
                }
            }

            crateToState.Clear();

            for (int i = 0; i < spawnStates.Count; i++)
            {
                SpawnState state = spawnStates[i];

                for (int j = 0; j < state.ActiveCrates.Count; j++)
                {
                    if (state.ActiveCrates[j])
                    {
                        Unsubscribe(state.ActiveCrates[j]);
                    }
                }

                state.ActiveCrates.Clear();

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

        void InitializeSpawnStates()
        {
            spawnStates.Clear();

            if (crateConfigs == null || crateConfigs.Length == 0)
            {
                return;
            }

            for (int i = 0; i < crateConfigs.Length; i++)
            {
                WeaponCrateSpawnConfig config = crateConfigs[i];
                if (config == null || config.cratePrefab == null)
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

        IEnumerator SpawnLoop(SpawnState state, float initialDelay, bool spawnImmediately)
        {
            if (spawnImmediately)
            {
                AttemptSpawn(state, false);
            }

            if (initialDelay > 0f)
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

                AttemptSpawn(state, false);
            }
        }

        void AttemptSpawn(SpawnState state, bool spawnClose)
        {
            if (player == null || state == null || state.Config == null)
            {
                return;
            }

            int maxActive = Mathf.Max(0, EvaluateCount(state.Config.maxActiveCurve, 1));
            int cratesPerSpawn = Mathf.Max(1, EvaluateCount(state.Config.cratesPerSpawnCurve, 1));
            int availableSlots = Mathf.Max(0, maxActive - state.ActiveCrates.Count);
            int toSpawn = Mathf.Min(cratesPerSpawn, availableSlots);

            for (int i = 0; i < toSpawn; i++)
            {
                SpawnCrate(state, spawnClose);
            }
        }

        int EvaluateCount(AnimationCurve curve, int fallback)
        {
            if (curve == null || curve.length == 0)
            {
                return fallback;
            }

            return Mathf.RoundToInt(curve.Evaluate(Time.timeSinceLevelLoad));
        }

        float EvaluateInterval(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0)
            {
                return 0.1f;
            }

            return Mathf.Max(0.05f, curve.Evaluate(Time.timeSinceLevelLoad));
        }

        void SpawnCrate(SpawnState state, bool spawnClose)
        {
            Vector3 spawnPos = spawnClose
                ? GetSpawnPosition(Mathf.Max(0.5f, state.Config.timeoutSpawnRadius), false)
                : GetSpawnPosition(minSpawnRadius, true);
            WeaponCrate cratePrefab = state.Config.cratePrefab;
            if (!cratePrefab)
            {
                return;
            }

            var crate = Instantiate(cratePrefab, spawnPos, Quaternion.identity);
            crate.ConfigurePickups(state.Config.pickupPool);
            crate.SetPickupLifetime(state.Config.pickupLifetimeSeconds);
            crate.SetLifetime(state.Config.crateLifetimeSeconds);

            state.ActiveCrates.Add(crate);
            crateToState[crate] = state;

            crate.OnBroken += HandleCrateBroken;
            crate.OnExpired += HandleCrateExpired;
        }

        void HandleCrateBroken(WeaponCrate crate)
        {
            HandleCrateFinished(crate, false);
        }

        void HandleCrateExpired(WeaponCrate crate)
        {
            HandleCrateFinished(crate, true);
        }

        void HandleCrateFinished(WeaponCrate crate, bool spawnClose)
        {
            if (!crate)
            {
                return;
            }

            if (crateToState.TryGetValue(crate, out var state))
            {
                Unsubscribe(crate);
                state.ActiveCrates.Remove(crate);
                crateToState.Remove(crate);

                if (state.RespawnRoutine != null)
                {
                    StopCoroutine(state.RespawnRoutine);
                }

                if (state.Config.useRespawnDelay)
                {
                    state.RespawnRoutine = StartCoroutine(RespawnAfterDelay(state, spawnClose));
                }
                else
                {
                    AttemptSpawn(state, spawnClose);
                }
            }
            else
            {
                Unsubscribe(crate);
            }
        }

        IEnumerator RespawnAfterDelay(SpawnState state, bool spawnClose)
        {
            float waitTime = Mathf.Max(0f, state.Config.respawnDelay);
            if (waitTime > 0f)
            {
                yield return new WaitForSeconds(waitTime);
            }

            AttemptSpawn(state, spawnClose);
            state.RespawnRoutine = null;
        }

        void Unsubscribe(WeaponCrate crate)
        {
            crate.OnBroken -= HandleCrateBroken;
            crate.OnExpired -= HandleCrateExpired;
        }

        Vector3 GetSpawnPosition(float preferredRadius, bool keepOffscreen)
        {
            Vector3 center = player ? player.position : Vector3.zero;
            Camera cam = Camera.main;

            float effectiveRadius = Mathf.Max(0.5f, preferredRadius);

            if (keepOffscreen && cam && cam.orthographic)
            {
                float halfHeight = cam.orthographicSize;
                float halfWidth = halfHeight * cam.aspect;
                float distance = Mathf.Sqrt(halfHeight * halfHeight + halfWidth * halfWidth) + offscreenPadding;
                distance = Mathf.Max(distance, effectiveRadius);
                return center + (Vector3)(GetDirection() * distance);
            }

            return center + (Vector3)(GetDirection() * effectiveRadius);
        }

        static Vector2 GetDirection()
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
