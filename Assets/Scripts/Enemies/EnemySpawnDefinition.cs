using UnityEngine;

namespace FF
{
    [CreateAssetMenu(menuName = "FF/Enemies/Spawn Definition", fileName = "EnemySpawnDefinition")]
    public class EnemySpawnDefinition : ScriptableObject
    {
        [Header("Prefab")]
        [SerializeField] private GameObject[] prefabs;
        [SerializeField] private PrefabSpawnOverride[] prefabOverrides;

        [Header("Wave Timing")]
        [SerializeField, Min(1)] private int startWave = 1;
        [SerializeField] private int endWave = -1;
        [SerializeField, Min(1)] private int spawnInterval = 1;
        [SerializeField] private bool spawnOnlyOnBossWaves;
        [SerializeField] private bool isBoss;

        [Header("Spawn Behaviour")]
        [SerializeField] private AnimationCurve spawnCountByWave = AnimationCurve.Linear(1f, 1f, 20f, 10f);
        [SerializeField, Range(0f, 1f)] private float spawnChance = 1f;
        [SerializeField] private bool spawnInPacks = false;
        [SerializeField, Min(0.1f)] private float packRadius = 1.5f;
        [SerializeField, Min(0)] private int poolPrewarmCount = 4;

        [Header("Scaling Override")]
        [SerializeField] private WaveAttributeScaling attributeScalingOverride;

        [Header("Optional Audio")]
        [SerializeField] private AudioClip spawnCue;

        public GameObject[] Prefabs => prefabs;
        public PrefabSpawnOverride[] PrefabOverrides => prefabOverrides;
        public int StartWave => Mathf.Max(1, startWave);
        public int EndWave => endWave;
        public int SpawnInterval => Mathf.Max(1, spawnInterval);
        public bool SpawnOnlyOnBossWaves => spawnOnlyOnBossWaves;
        public bool IsBoss => isBoss;
        public bool SpawnInPacks => spawnInPacks;
        public float PackRadius => packRadius;
        public AudioClip SpawnCue => spawnCue;
        public float SpawnChance => Mathf.Clamp01(spawnChance);
        public int PoolPrewarmCount => Mathf.Max(0, poolPrewarmCount);

        public int EvaluateSpawnCount(int wave)
        {
            return EvaluateSpawnCount(wave, spawnCountByWave);
        }

        public EnemyWaveModifiers GetWaveModifiers(int wave, WaveAttributeScaling fallback)
        {
            WaveAttributeScaling scaling = attributeScalingOverride ?? fallback;
            return scaling != null ? scaling.CreateModifiers(wave) : EnemyWaveModifiers.Identity;
        }

        public int EvaluateSpawnCount(int wave, AnimationCurve curve)
        {
            if (curve == null || curve.length == 0)
            {
                return 0;
            }

            float evaluated = curve.Evaluate(Mathf.Max(1, wave));
            return Mathf.Max(0, Mathf.CeilToInt(evaluated));
        }

        public WaveAttributeScaling GetScalingOverrideForPrefab(GameObject prefab)
        {
            PrefabSpawnOverride match = FindOverride(prefab);
            if (match != null && match.overrideScaling && match.attributeScalingOverride)
            {
                return match.attributeScalingOverride;
            }

            return attributeScalingOverride;
        }

        public System.Collections.Generic.List<PrefabEntry> GetEligiblePrefabs(
            int wave,
            bool isBossWave,
            bool allowNonBossDuringBossWave)
        {
            var entries = new System.Collections.Generic.List<PrefabEntry>();

            if (prefabs == null || prefabs.Length == 0)
            {
                return entries;
            }

            for (int i = 0; i < prefabs.Length; i++)
            {
                GameObject prefab = prefabs[i];
                if (!prefab)
                {
                    continue;
                }

                PrefabSpawnOverride spawnOverride = FindOverride(prefab);
                var entry = new PrefabEntry(this, prefab, spawnOverride);
                if (entry.IsActive(wave, isBossWave, allowNonBossDuringBossWave))
                {
                    entries.Add(entry);
                }
            }

            return entries;
        }

        public bool HasPrefabOverrides => prefabOverrides != null && prefabOverrides.Length > 0;

        private PrefabSpawnOverride FindOverride(GameObject prefab)
        {
            if (prefabOverrides == null || prefabOverrides.Length == 0 || prefab == null)
            {
                return null;
            }

            for (int i = 0; i < prefabOverrides.Length; i++)
            {
                PrefabSpawnOverride option = prefabOverrides[i];
                if (option != null && option.prefab == prefab)
                {
                    return option;
                }
            }

            return null;
        }

        [System.Serializable]
        public class PrefabSpawnOverride
        {
            [Header("Prefab")]
            public GameObject prefab;

            [Header("Wave Overrides")]
            public bool overrideWaveTiming;
            [Min(1)] public int startWave = 1;
            public int endWave = -1;
            [Min(1)] public int spawnInterval = 1;
            public bool spawnOnlyOnBossWaves;

            [Header("Spawn Chance")]
            public bool overrideSpawnChance;
            [Range(0f, 1f)] public float spawnChance = 1f;

            [Header("Spawn Count")]
            public bool overrideSpawnCurve;
            public AnimationCurve spawnCountByWave = AnimationCurve.Linear(1f, 1f, 20f, 10f);

            [Header("Scaling")]
            public bool overrideScaling;
            public WaveAttributeScaling attributeScalingOverride;
        }

        public readonly struct PrefabEntry
        {
            public GameObject Prefab { get; }
            public int StartWave { get; }
            public int EndWave { get; }
            public int SpawnInterval { get; }
            public bool SpawnOnlyOnBossWaves { get; }
            public bool IsBoss { get; }
            public float SpawnChance { get; }
            public AnimationCurve SpawnCurve { get; }
            public bool HasSpawnCountOverride { get; }
            public WaveAttributeScaling ScalingOverride { get; }
            public bool HasScalingOverride { get; }

            readonly EnemySpawnDefinition _definition;

            public PrefabEntry(EnemySpawnDefinition definition, GameObject prefab, PrefabSpawnOverride spawnOverride)
            {
                _definition = definition;
                Prefab = prefab;

                bool useWaveOverride = spawnOverride != null && spawnOverride.overrideWaveTiming;
                StartWave = useWaveOverride ? Mathf.Max(1, spawnOverride.startWave) : definition.StartWave;
                EndWave = useWaveOverride ? spawnOverride.endWave : definition.EndWave;
                SpawnInterval = useWaveOverride ? Mathf.Max(1, spawnOverride.spawnInterval) : definition.SpawnInterval;
                SpawnOnlyOnBossWaves = useWaveOverride ? spawnOverride.spawnOnlyOnBossWaves : definition.SpawnOnlyOnBossWaves;

                bool useChanceOverride = spawnOverride != null && spawnOverride.overrideSpawnChance;
                SpawnChance = useChanceOverride ? Mathf.Clamp01(spawnOverride.spawnChance) : definition.SpawnChance;

                bool useSpawnCountOverride = spawnOverride != null && spawnOverride.overrideSpawnCurve;
                SpawnCurve = useSpawnCountOverride ? spawnOverride.spawnCountByWave : definition.spawnCountByWave;
                HasSpawnCountOverride = useSpawnCountOverride;

                ScalingOverride = spawnOverride != null && spawnOverride.overrideScaling
                    ? spawnOverride.attributeScalingOverride
                    : definition.attributeScalingOverride;
                HasScalingOverride = spawnOverride != null && spawnOverride.overrideScaling;

                IsBoss = definition.IsBoss || definition.SpawnOnlyOnBossWaves || SpawnOnlyOnBossWaves;
            }

            public bool IsActive(int wave, bool isBossWave, bool allowNonBossDuringBossWave)
            {
                if (wave < StartWave)
                {
                    return false;
                }

                if (EndWave > 0 && wave > EndWave)
                {
                    return false;
                }

                if (SpawnOnlyOnBossWaves)
                {
                    if (!isBossWave)
                    {
                        return false;
                    }
                }
                else if (isBossWave && !allowNonBossDuringBossWave)
                {
                    return false;
                }

                int relativeWave = wave - StartWave;
                if (relativeWave < 0 || relativeWave % SpawnInterval != 0)
                {
                    return false;
                }

                if (SpawnChance < 1f && UnityEngine.Random.value > Mathf.Clamp01(SpawnChance))
                {
                    return false;
                }

                return true;
            }

            public int EvaluateSpawnCount(int wave, int sharedDefault)
            {
                int desired = HasSpawnCountOverride
                    ? _definition.EvaluateSpawnCount(wave, SpawnCurve)
                    : sharedDefault;

                return Mathf.Max(0, desired);
            }

            public EnemyWaveModifiers BuildWaveModifiers(int wave, WaveAttributeScaling fallback)
            {
                WaveAttributeScaling scaling = ScalingOverride ?? _definition.attributeScalingOverride ?? fallback;
                return scaling != null ? scaling.CreateModifiers(wave) : EnemyWaveModifiers.Identity;
            }
        }
    }
}
