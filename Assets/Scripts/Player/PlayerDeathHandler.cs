using System;
using UnityEngine;

namespace FF
{
    [RequireComponent(typeof(Health))]
    public class PlayerDeathHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Health _health;
        [SerializeField] private PlayerState _playerState;
        [SerializeField] private InputRouter _inputRouter;

        public event Action OnPlayerDied;

        private void Awake()
        {
            if (!ValidateDependencies())
            {
                Debug.LogError($"{nameof(PlayerDeathHandler)} on {name} disabled due to missing dependencies.", this);
                enabled = false;
            }
        }

        private void OnValidate()
        {
            if (!_health) _health = GetComponent<Health>();
            if (!_playerState) _playerState = GetComponent<PlayerState>();
            if (!_inputRouter) _inputRouter = GetComponent<InputRouter>();
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
            _playerState?.Kill();
            _inputRouter?.SetActionBlocked(true);
            OnPlayerDied?.Invoke();
        }

        private bool ValidateDependencies()
        {
            bool ok = true;

            if (!_health)
            {
                Debug.LogError("Missing Health reference.", this);
                ok = false;
            }

            if (!_playerState)
            {
                Debug.LogError("Missing PlayerState reference.", this);
                ok = false;
            }

            if (!_inputRouter)
            {
                Debug.LogError("Missing InputRouter reference.", this);
                ok = false;
            }

            return ok;
        }
    }
}
