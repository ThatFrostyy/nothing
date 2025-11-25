using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FF
{
    public class InputRouter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UpgradeManager _upgradeManager;
        [SerializeField] private WeaponManager _weaponManager;

        [Header("State")]
        [SerializeField] private bool _blockInputFromUI;

        public event Action OnFireStart;
        public event Action OnFireStop;
        public event Action OnUpgradeRequested;
        public event Action OnNextWeaponRequested;
        public event Action OnPreviousWeaponRequested;
        public event Action<Vector2> OnLookInput;

        public Vector2 MoveInput { get; private set; }
        public bool FireHeld { get; private set; }
        public Vector2 LookInput { get; private set; }

        private bool _actionBlocked;

        private void Awake()
        {
            if (!_upgradeManager && UpgradeManager.I != null)
            {
                _upgradeManager = UpgradeManager.I;
            }

            if (!ValidateDependencies())
            {
                Debug.LogError($"{nameof(InputRouter)} on {name} disabled due to missing dependencies.", this);
                enabled = false;
                return;
            }
        }

        private void OnEnable()
        {
            UpgradeUI.OnVisibilityChanged += HandleUpgradeVisibilityChanged;
        }

        private void OnDisable()
        {
            UpgradeUI.OnVisibilityChanged -= HandleUpgradeVisibilityChanged;
            ResetInputState();
        }

        private void OnValidate()
        {
            if (!_upgradeManager) _upgradeManager = GetComponent<UpgradeManager>();
            if (!_weaponManager) _weaponManager = GetComponentInChildren<WeaponManager>();
        }

        public void BlockInputFromUI(bool shouldBlock)
        {
            _blockInputFromUI = shouldBlock;
            if (shouldBlock)
            {
                ResetInputState();
            }
        }

        public void SetActionBlocked(bool blocked)
        {
            _actionBlocked = blocked;
            if (blocked)
            {
                ResetInputState();
            }
        }

        public void OnMove(InputValue value)
        {
            MoveInput = CanProcessGameplayInput ? value.Get<Vector2>() : Vector2.zero;
        }

        public void OnFire(InputValue value)
        {
            bool wantsToFire = value.Get<float>() > 0.5f;

            if (!CanProcessGameplayInput)
            {
                if (FireHeld)
                {
                    ResetFireState();
                }
                return;
            }

            if (FireHeld == wantsToFire)
            {
                return;
            }

            FireHeld = wantsToFire;
            if (FireHeld)
            {
                OnFireStart?.Invoke();
            }
            else
            {
                OnFireStop?.Invoke();
            }
        }

        public void OnUpgrade(InputValue value)
        {
            if (!value.isPressed || !CanProcessGameplayInput)
            {
                return;
            }

            OnUpgradeRequested?.Invoke();
            _upgradeManager?.TryOpenUpgradeMenu();
        }

        public void OnPrevious(InputValue value)
        {
            if (!value.isPressed || !CanProcessGameplayInput)
            {
                return;
            }

            OnPreviousWeaponRequested?.Invoke();
            _weaponManager?.SelectPreviousSlot();
        }

        public void OnNext(InputValue value)
        {
            if (!value.isPressed || !CanProcessGameplayInput)
            {
                return;
            }

            OnNextWeaponRequested?.Invoke();
            _weaponManager?.SelectNextSlot();
        }

        public void OnLook(InputValue value)
        {
            if (!CanProcessGameplayInput)
            {
                ResetLookState();
                return;
            }

            LookInput = value.Get<Vector2>();
            OnLookInput?.Invoke(LookInput);
        }

        private bool ValidateDependencies()
        {
            bool ok = true;

            if (!_upgradeManager)
            {
                Debug.LogError("Missing UpgradeManager reference.", this);
                ok = false;
            }

            if (!_weaponManager)
            {
                Debug.LogError("Missing WeaponManager reference.", this);
                ok = false;
            }

            return ok;
        }

        public bool CanProcessGameplayInput => isActiveAndEnabled && !_blockInputFromUI && !_actionBlocked;

        private void ResetInputState()
        {
            MoveInput = Vector2.zero;
            ResetFireState();
            ResetLookState();
        }

        private void ResetFireState()
        {
            if (!FireHeld)
            {
                return;
            }

            FireHeld = false;
            OnFireStop?.Invoke();
        }

        private void ResetLookState()
        {
            LookInput = Vector2.zero;
            OnLookInput?.Invoke(LookInput);
        }

        private void HandleUpgradeVisibilityChanged(bool isVisible)
        {
            BlockInputFromUI(isVisible);
        }
    }
}
