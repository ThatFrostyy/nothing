using System;
using UnityEngine;

namespace FF
{
    [RequireComponent(typeof(InputRouter))]
    public class PlayerState : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InputRouter _inputRouter;
        [SerializeField] private WeaponManager _weaponManager;

        [Header("State")]
        [SerializeField] private bool _isAlive = true;
        [SerializeField] private bool _inputLocked;
        [SerializeField] private Weapon _startingWeapon;

        public event Action<bool> OnLifeStateChanged;

        public bool IsAlive => _isAlive;
        public bool CanAct => _isAlive && !_inputLocked;
        public bool CanMove => CanAct;

        private void Awake()
        {
            if (!ValidateDependencies())
            {
                Debug.LogError($"{nameof(PlayerState)} on {name} disabled due to missing dependencies.", this);
                enabled = false;
                return;
            }
        }

        private void OnEnable()
        {
            ApplyInputLocks();
        }

        private void Start()
        {
            if (_startingWeapon && _weaponManager)
            {
                _weaponManager.Equip(_startingWeapon);
            }
        }

        private void OnDisable()
        {
            _inputRouter?.SetActionBlocked(true);
        }

        private void OnValidate()
        {
            if (!_inputRouter) _inputRouter = GetComponent<InputRouter>();
            if (!_weaponManager) _weaponManager = GetComponentInChildren<WeaponManager>();
        }

        public void Kill()
        {
            if (!_isAlive)
            {
                return;
            }

            _isAlive = false;
            ApplyInputLocks();
            OnLifeStateChanged?.Invoke(false);
        }

        public void Revive()
        {
            if (_isAlive)
            {
                return;
            }

            _isAlive = true;
            ApplyInputLocks();
            OnLifeStateChanged?.Invoke(true);
        }

        public void SetInputLocked(bool locked)
        {
            _inputLocked = locked;
            ApplyInputLocks();
        }

        public void OverrideStartingWeapon(Weapon weapon)
        {
            _startingWeapon = weapon;
        }

        private void ApplyInputLocks()
        {
            bool shouldBlock = !_isAlive || _inputLocked;
            _inputRouter?.SetActionBlocked(shouldBlock);
        }

        private bool ValidateDependencies()
        {
            bool ok = true;

            if (!_inputRouter)
            {
                Debug.LogError("Missing InputRouter reference.", this);
                ok = false;
            }

            if (!_weaponManager)
            {
                Debug.LogError("Missing WeaponManager reference.", this);
                ok = false;
            }

            return ok;
        }
    }
}
