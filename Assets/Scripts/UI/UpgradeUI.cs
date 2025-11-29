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
        [SerializeField] TMPro.TMP_Text aTitle, bTitle, cTitle;
        [SerializeField] TMPro.TMP_Text aTxt, bTxt, cTxt;
        [SerializeField] TMPro.TMP_Text upgradesRemainingText;

        Action<Upgrade> callback;
        Upgrade a, b, c;

        public static event Action<bool> OnVisibilityChanged;
        public static bool IsShowing { get; private set; }

        UnityAction aListener, bListener, cListener;

        void Start()
        {
            if (UpgradeManager.I != null)
            {
                UpgradeManager.I.RegisterUI(this);
                Debug.Log("UpgradeUI registered with UpgradeManager.");
            }
            panel.SetActive(false);
        }

        void Pick(Upgrade u)
        {
            callback?.Invoke(u);
        }

        public void Show(Upgrade A, Upgrade B, Upgrade C, Action<Upgrade> onPick, int pendingUpgrades)
        {
            a = A; b = B; c = C; callback = onPick;

            aTitle.text = $"{A.Title}\n";
            bTitle.text = $"{B.Title}\n";
            cTitle.text = $"{C.Title}\n";

            aTxt.text = $"{A.Description}";
            bTxt.text = $"{B.Description}";
            cTxt.text = $"{C.Description}";

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
