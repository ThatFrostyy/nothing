using UnityEngine;

namespace FF
{
    public class CursorManager : MonoBehaviour
    {
        [Header("Cursor Behaviour")]
        [SerializeField] private bool lockDuringGameplay = true;
        [SerializeField] private bool hideDuringGameplay = true;
        [SerializeField] private bool unlockWhenUIVisible = true;

        private bool _uiVisible;

        private void OnEnable()
        {
            UpgradeUI.OnVisibilityChanged += HandleUpgradeVisibilityChanged;
            ApplyCursorState();
        }

        private void OnDisable()
        {
            UpgradeUI.OnVisibilityChanged -= HandleUpgradeVisibilityChanged;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void ApplyCursorState()
        {
            bool shouldUnlockForUi = unlockWhenUIVisible && _uiVisible;
            bool shouldLock = lockDuringGameplay && !shouldUnlockForUi;

            Cursor.lockState = shouldLock ? CursorLockMode.Confined : CursorLockMode.None;
            Cursor.visible = !hideDuringGameplay || shouldUnlockForUi;
        }

        public void SetUiVisibility(bool isVisible)
        {
            _uiVisible = isVisible;
            ApplyCursorState();
        }

        private void HandleUpgradeVisibilityChanged(bool isVisible)
        {
            SetUiVisibility(isVisible);
        }
    }
}
