using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace FF
{
    public class UpgradeUI : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] Button aBtn, bBtn, cBtn;
        [SerializeField] TMPro.TMP_Text aTxt, bTxt, cTxt;
        [SerializeField] TMPro.TMP_Text upgradesRemainingText;

        Action<Upgrade> callback;
        Upgrade a, b, c;

        public static event Action<bool> OnVisibilityChanged;
        public static bool IsShowing { get; private set; }

        UnityAction aListener, bListener, cListener;

        void Pick(Upgrade u)
        {
            callback?.Invoke(u);
        }

        public void Show(Upgrade A, Upgrade B, Upgrade C, Action<Upgrade> onPick, int pendingUpgrades)
        {
            a = A; b = B; c = C; callback = onPick;
            aTxt.text = $"{A.Title}\n{A.Description}";
            bTxt.text = $"{B.Title}\n{B.Description}";
            cTxt.text = $"{C.Title}\n{C.Description}";
            panel.SetActive(true);

            if (upgradesRemainingText)
            {
                upgradesRemainingText.text = $"Upgrades left: {Mathf.Max(0, pendingUpgrades)}";
            }

            if (aListener != null) aBtn.onClick.RemoveListener(aListener);
            if (bListener != null) bBtn.onClick.RemoveListener(bListener);
            if (cListener != null) cBtn.onClick.RemoveListener(cListener);

            aListener = () => Pick(a);
            bListener = () => Pick(b);
            cListener = () => Pick(c);

            aBtn.onClick.AddListener(aListener);
            bBtn.onClick.AddListener(bListener);
            cBtn.onClick.AddListener(cListener);


            Time.timeScale = 0f;

            if (!IsShowing)
            {
                IsShowing = true;
                OnVisibilityChanged?.Invoke(true);
            }
        }

        public void Hide()
        {
            panel.SetActive(false);
            Time.timeScale = 1f;
            if (IsShowing)
            {
                IsShowing = false;
                OnVisibilityChanged?.Invoke(false);
            }
        }
    }
}
