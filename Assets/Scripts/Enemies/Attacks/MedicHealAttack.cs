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
        [SerializeField] private Transform vfxAnchor;

        private readonly Collider2D[] _overlapBuffer = new Collider2D[24];
        private readonly List<Health> _targets = new();
        private float _healAccumulator;
        private float _searchTimer;
        private GameObject _areaInstance;
        private GameObject _activeHealingVfx;

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
            int hits = Physics2D.OverlapCircle(origin, healRadius, _overlapBuffer);
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
            if (!_activeHealingVfx && shouldBeActive && healingVfxPrefab)
            {
                Transform parent = vfxAnchor ? vfxAnchor : transform;
                _activeHealingVfx = Instantiate(healingVfxPrefab, parent.position, parent.rotation, parent);
            }

            if (_activeHealingVfx)
            {
                if (shouldBeActive && !_activeHealingVfx.activeSelf)
                {
                    _activeHealingVfx.SetActive(true);
                }
                else if (!shouldBeActive && _activeHealingVfx.activeSelf)
                {
                    _activeHealingVfx.SetActive(false);
                }
            }
        }
    }
}

