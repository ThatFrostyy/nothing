using System.Collections.Generic;
using UnityEngine;

namespace FF
{
    [AddComponentMenu("FF/Enemies/Attacks/Medic Heal Attack")]
    public class MedicHealAttack : MonoBehaviour, IEnemyAttack
    {
        [Header("Healing")]
        [SerializeField, Min(0.5f)] private float healRadius = 3.5f;
        [SerializeField, Min(0.1f)] private float healPerSecond = 4f;
        [SerializeField, Min(0.1f)] private float targetSearchInterval = 0.35f;

        [Header("VFX")]
        [SerializeField] private GameObject areaVfxPrefab;
        [SerializeField] private GameObject healingVfxPrefab;
        [SerializeField] private float yOffset = 0.5f;
        [SerializeField] private Transform vfxAnchor;

        private readonly Collider2D[] _overlapBuffer = new Collider2D[24];
        private readonly List<Health> _targets = new();
        private readonly Dictionary<Health, GameObject> _healingVfxInstances = new();
        private float _healAccumulator;
        private float _searchTimer;
        private GameObject _areaInstance;

        public bool IsHealing { get; private set; }
        public bool HasNearbyAllies { get; private set; }

        void OnEnable()
        {
            SpawnAreaVfx();
            UpdateHealingVfx(false);
            _searchTimer = 0f;
            _healAccumulator = 0f;
        }

        void OnDisable()
        {
            UpdateHealingVfx(false);
        }

        public void TickAttack(Enemy enemy, Transform player, EnemyStats stats, AutoShooter shooter, float deltaTime)
        {
            _searchTimer -= deltaTime;
            if (_searchTimer <= 0f)
            {
                RefreshTargets(enemy);
                _searchTimer = targetSearchInterval;
            }

            if (_targets.Count == 0)
            {
                IsHealing = false;
                UpdateHealingVfx(false);
                return;
            }

            float scaledHeal = healPerSecond * deltaTime;
            if (scaledHeal <= 0f)
            {
                IsHealing = false;
                UpdateHealingVfx(false);
                return;
            }

            _healAccumulator += scaledHeal;
            int healAmount = Mathf.FloorToInt(_healAccumulator);
            if (healAmount > 0)
            {
                for (int i = 0; i < _targets.Count; i++)
                {
                    Health target = _targets[i];
                    if (target != null)
                    {
                        target.Heal(healAmount);
                    }
                }
                _healAccumulator -= healAmount;
            }

            IsHealing = true;
            UpdateHealingVfx(true);
        }

        private void RefreshTargets(Enemy enemy)
        {
            _targets.Clear();
            HasNearbyAllies = false;

            if (!enemy)
            {
                return;
            }

            Vector2 origin = enemy.transform.position;
            int hits = Physics2D.OverlapCircleNonAlloc(origin, healRadius, _overlapBuffer);
            for (int i = 0; i < hits; i++)
            {
                Collider2D col = _overlapBuffer[i];
                if (!col)
                {
                    continue;
                }

                Enemy otherEnemy = col.GetComponentInParent<Enemy>();
                if (!otherEnemy || otherEnemy == enemy)
                {
                    continue;
                }

                HasNearbyAllies = true;
                Health health = col.GetComponentInParent<Health>();
                if (health != null && health.CurrentHP < health.MaxHP)
                {
                    if (!_targets.Contains(health))
                    {
                        _targets.Add(health);
                    }
                }
            }
        }

        private void SpawnAreaVfx()
        {
            if (_areaInstance || !areaVfxPrefab)
            {
                return;
            }

            Transform parent = vfxAnchor ? vfxAnchor : transform;
            _areaInstance = Instantiate(areaVfxPrefab, parent.position, parent.rotation, parent);
        }

        private void UpdateHealingVfx(bool shouldBeActive)
        {
            if (!shouldBeActive)
            {
                // destroy all per-target healing VFX
                foreach (var kvp in _healingVfxInstances)
                {
                    if (kvp.Value)
                    {
                        FadeOutHealingVfx(kvp.Value);
                    }
                }
                _healingVfxInstances.Clear();
                return;
            }

            if (_targets.Count == 0)
            {
                // nothing to spawn
                return;
            }

            // Remove instances for targets that are no longer present or were destroyed
            var toRemove = new List<Health>();
            foreach (var kvp in _healingVfxInstances)
            {
                if (kvp.Key == null || !_targets.Contains(kvp.Key))
                {
                    if (kvp.Value)
                    {
                        FadeOutHealingVfx(kvp.Value);
                    }
                    toRemove.Add(kvp.Key);
                }
            }
            for (int i = 0; i < toRemove.Count; i++)
            {
                _healingVfxInstances.Remove(toRemove[i]);
            }

            // Create instances for targets that don't have one yet
            for (int i = 0; i < _targets.Count; i++)
            {
                var target = _targets[i];
                if (target == null)
                {
                    continue;
                }

                if (!_healingVfxInstances.ContainsKey(target))
                {
                    if (!healingVfxPrefab)
                    {
                        continue;
                    }

                    // Instantiate the healing VFX above the target and parent it so it follows the target.
                    Transform targetTransform = target.transform;
                    var instance = Instantiate(healingVfxPrefab, targetTransform.position, targetTransform.rotation, targetTransform);

                    // Move it slightly above the target so it's visible above the troop (tweak Y value if needed).
                    instance.transform.localPosition = new Vector3(0f, yOffset, 0f);

                    _healingVfxInstances[target] = instance;
                }
                else
                {
                    // Ensure instance is active
                    var inst = _healingVfxInstances[target];
                    if (inst && !inst.activeSelf)
                    {
                        inst.SetActive(true);
                    }
                }
            }
        }

        private void FadeOutHealingVfx(GameObject instance)
        {
            if (!instance)
            {
                return;
            }

            ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>();
            if (particleSystems.Length == 0)
            {
                Destroy(instance);
                return;
            }

            float maxLifetime = 0.1f;
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem.MainModule main = particleSystems[i].main;
                float delay = main.startDelay.constantMax;
                float lifetime = main.startLifetime.constantMax;
                maxLifetime = Mathf.Max(maxLifetime, delay + lifetime);
                particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            Destroy(instance, Mathf.Max(0.01f, maxLifetime));
        }


        void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, healRadius);
        }
    }
}
