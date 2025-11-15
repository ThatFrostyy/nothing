using System;
using UnityEngine;
using UnityEngine.UI;

namespace FF
{
    public class UpgradeUI : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] Button aBtn, bBtn, cBtn;
        [SerializeField] TMPro.TMP_Text aTxt, bTxt, cTxt;

        System.Action<Upgrade> callback;
        Upgrade a, b, c;

        public static event Action<bool> OnVisibilityChanged;
        public static bool IsShowing { get; private set; }

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
        void Pick(Upgrade u) => callback?.Invoke(u);

        public void Show(Upgrade A, Upgrade B, Upgrade C, System.Action<Upgrade> onPick)
        {
            a = A; b = B; c = C; callback = onPick;
            aTxt.text = $"{A.Title}\n{A.Description}";
            bTxt.text = $"{B.Title}\n{B.Description}";
            cTxt.text = $"{C.Title}\n{C.Description}";
            panel.SetActive(true);
            aBtn.onClick.RemoveAllListeners();
            bBtn.onClick.RemoveAllListeners();
            cBtn.onClick.RemoveAllListeners();
            aBtn.onClick.AddListener(() => Pick(a));
            bBtn.onClick.AddListener(() => Pick(b));
            cBtn.onClick.AddListener(() => Pick(c));
            Time.timeScale = 0f;
            if (!IsShowing)
            {
                IsShowing = true;
                OnVisibilityChanged?.Invoke(true);
            }
        }
    }
}
