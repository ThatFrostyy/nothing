using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FF
{
    public class UpgradeUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Button aBtn;
        [SerializeField] private Button bBtn;
        [SerializeField] private Button cBtn;
        [SerializeField] private TMP_Text aTxt;
        [SerializeField] private TMP_Text bTxt;
        [SerializeField] private TMP_Text cTxt;

        private Action<Upgrade> callback;
        private Upgrade a;
        private Upgrade b;
        private Upgrade c;

        public static event Action<bool> OnVisibilityChanged;
        public static bool IsShowing { get; private set; }

        private UnityAction aListener;
        private UnityAction bListener;
        private UnityAction cListener;

        private bool cursorStateCaptured;
        private bool previousCursorVisible;
        private CursorLockMode previousCursorLockMode;

        private void Pick(Upgrade u)
        {
            callback?.Invoke(u);
        }

        public void Show(Upgrade A, Upgrade B, Upgrade C, Action<Upgrade> onPick)
        {
            if (panel == null)
            {
                Debug.LogWarning("UpgradeUI panel is not assigned.");
                return;
            }

            a = A;
            b = B;
            c = C;
            callback = onPick;

            if (aTxt != null)
            {
                aTxt.text = $"{A.Title}\n{A.Description}";
            }
            else
            {
                Debug.LogWarning("UpgradeUI option A text is not assigned.", this);
            }

            if (bTxt != null)
            {
                bTxt.text = $"{B.Title}\n{B.Description}";
            }
            else
            {
                Debug.LogWarning("UpgradeUI option B text is not assigned.", this);
            }

            if (cTxt != null)
            {
                cTxt.text = $"{C.Title}\n{C.Description}";
            }
            else
            {
                Debug.LogWarning("UpgradeUI option C text is not assigned.", this);
            }

            panel.SetActive(true);

            if (aBtn != null && aListener != null)
            {
                aBtn.onClick.RemoveListener(aListener);
            }

            if (bBtn != null && bListener != null)
            {
                bBtn.onClick.RemoveListener(bListener);
            }

            if (cBtn != null && cListener != null)
            {
                cBtn.onClick.RemoveListener(cListener);
            }

            aListener = () => Pick(a);
            bListener = () => Pick(b);
            cListener = () => Pick(c);

            if (aBtn != null)
            {
                aBtn.onClick.AddListener(aListener);
            }
            else
            {
                Debug.LogWarning("UpgradeUI option A button is not assigned.", this);
            }

            if (bBtn != null)
            {
                bBtn.onClick.AddListener(bListener);
            }
            else
            {
                Debug.LogWarning("UpgradeUI option B button is not assigned.", this);
            }

            if (cBtn != null)
            {
                cBtn.onClick.AddListener(cListener);
            }
            else
            {
                Debug.LogWarning("UpgradeUI option C button is not assigned.", this);
            }

            CaptureCursorState();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Time.timeScale = 0f;

            if (!IsShowing)
            {
                IsShowing = true;
                OnVisibilityChanged?.Invoke(true);
            }
        }

        public void Hide()
        {
            if (panel == null)
            {
                return;
            }

            panel.SetActive(false);
            Time.timeScale = 1f;
            RestoreCursorState();

            if (IsShowing)
            {
                IsShowing = false;
                OnVisibilityChanged?.Invoke(false);
            }
        }

        private void CaptureCursorState()
        {
            if (cursorStateCaptured)
            {
                return;
            }

            previousCursorVisible = Cursor.visible;
            previousCursorLockMode = Cursor.lockState;
            cursorStateCaptured = true;
        }

        private void RestoreCursorState()
        {
            if (!cursorStateCaptured)
            {
                return;
            }

            Cursor.lockState = previousCursorLockMode;
            Cursor.visible = previousCursorVisible;
            cursorStateCaptured = false;
        }
    }
}
