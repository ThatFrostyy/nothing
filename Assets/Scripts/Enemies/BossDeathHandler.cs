using UnityEngine;
using UnityEngine.Events;

namespace FF
{
    [RequireComponent(typeof(Health))]
    public class BossDeathHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Health _health;
        [SerializeField] private Animator _animator;
        [SerializeField] private string _deathTrigger = "Die";
        [SerializeField] private Transform _lootSpawnPoint;
        [SerializeField] private GameObject _lootChestPrefab;

        [Header("Events")]
        [SerializeField] private UnityEvent _onBossDeath;

        private void Awake()
        {
            if (!ValidateDependencies())
            {
                Debug.LogError($"{nameof(BossDeathHandler)} on {name} disabled due to missing dependencies.", this);
                enabled = false;
            }
        }

        private void OnValidate()
        {
            if (!_health) _health = GetComponent<Health>();
            if (!_animator) _animator = GetComponentInChildren<Animator>();
            if (!_lootSpawnPoint) _lootSpawnPoint = transform;
        }

        private void OnEnable()
        {
            if (_health != null)
            {
                _health.OnDeath += HandleDeath;
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.OnDeath -= HandleDeath;
            }
        }

        private void HandleDeath()
        {
            TriggerAnimation();
            SpawnLootChest();
            _onBossDeath?.Invoke();
        }

        private void TriggerAnimation()
        {
            if (_animator != null && !string.IsNullOrEmpty(_deathTrigger))
            {
                _animator.SetTrigger(_deathTrigger);
            }
        }

        private void SpawnLootChest()
        {
            if (!_lootChestPrefab)
            {
                return;
            }

            Vector3 position = _lootSpawnPoint ? _lootSpawnPoint.position : transform.position;
            Instantiate(_lootChestPrefab, position, Quaternion.identity);
        }

        private bool ValidateDependencies()
        {
            bool ok = true;

            if (!_health)
            {
                Debug.LogError("Missing Health reference.", this);
                ok = false;
            }

            return ok;
        }
    }
}
