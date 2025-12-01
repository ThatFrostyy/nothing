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
        [SerializeField] TMPro.TMP_Text aExtra, bExtra, cExtra; // NEW
        [SerializeField] TMPro.TMP_Text upgradesRemainingText;
        [SerializeField] TMPro.TMP_Text phaseTitleText;
        [SerializeField] RectTransform[] cardRoots;
        [SerializeField, Min(0f)] float cardPopDuration = 0.25f;
        [SerializeField] AnimationCurve cardPopCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        Action<Upgrade> callback;
        Upgrade a, b, c;
        Action<WeaponUpgradeOption> weaponCallback;
        WeaponUpgradeOption weaponA, weaponB, weaponC;

        public static event Action<bool> OnVisibilityChanged;
        public static bool IsShowing { get; private set; }

        UnityAction aListener, bListener, cListener;
        Coroutine popRoutine;
        bool showingWeaponCards;

        void Start()
        {
            RegisterWithManager();
            panel.SetActive(false);
        }

        void OnEnable()
        {
            RegisterWithManager();
        }

        void RegisterWithManager()
        {
            if (UpgradeManager.I != null)
            {
                UpgradeManager.I.RegisterUI(this);
            }
        }

        void OnDestroy()
        {
            ResetStaticState();
        }

        void Pick(Upgrade u)
        {
            callback?.Invoke(u);
        }

        void PickWeaponUpgrade(WeaponUpgradeOption option)
        {
            weaponCallback?.Invoke(option);
            showingWeaponCards = false;
        }

        public void Show(Upgrade A, Upgrade B, Upgrade C, Action<Upgrade> onPick, int pendingUpgrades)
        {
            aExtra.gameObject.SetActive(false);
            bExtra.gameObject.SetActive(false);
            cExtra.gameObject.SetActive(false);

            showingWeaponCards = false;
            a = A; b = B; c = C; callback = onPick;

            EnsureCardRoots();
            ResetCardScales();

            aTitle.text = $"{A.Title}\n";
            bTitle.text = $"{B.Title}\n";
            cTitle.text = $"{C.Title}\n";

            aTxt.text = $"{A.Description}";
            bTxt.text = $"{B.Description}";
            cTxt.text = $"{C.Description}";

            UpdatePhaseHeader(null);
            UpdateRemainingLabel(pendingUpgrades);

            ClearButtonListeners();

            aListener = () => Pick(a);
            bListener = () => Pick(b);
            cListener = () => Pick(c);

            aBtn.onClick.AddListener(aListener);
            bBtn.onClick.AddListener(bListener);
            cBtn.onClick.AddListener(cListener);

            OpenPanel(false);
        }

        public void ShowWeaponUpgrades(Weapon weapon, WeaponUpgradeOption A, WeaponUpgradeOption B, WeaponUpgradeOption C, Action<WeaponUpgradeOption> onPick, int pendingUpgrades, string phaseTitleOverride = null)
        {
            showingWeaponCards = true;
            weaponCallback = onPick;
            weaponA = A; weaponB = B; weaponC = C;

            EnsureCardRoots();
            ResetCardScales();

            string weaponName = weapon != null && !string.IsNullOrEmpty(weapon.weaponName) ? weapon.weaponName : weapon != null ? weapon.name : "Weapon";

            // Normal text (keeps user-made formatting)
            aTitle.text = A.BaseTitle;
            bTitle.text = B.BaseTitle;
            cTitle.text = C.BaseTitle;

            aTxt.text = A.BaseDescription;
            bTxt.text = B.BaseDescription;
            cTxt.text = C.BaseDescription;

            // Show only the bonus line in the new 'Extra' fields
            aExtra.gameObject.SetActive(true);
            bExtra.gameObject.SetActive(true);
            cExtra.gameObject.SetActive(true);

            aExtra.text = A.FinalDescription; // only % and kills
            bExtra.text = B.FinalDescription;
            cExtra.text = C.FinalDescription;


            UpdatePhaseHeader(string.IsNullOrEmpty(phaseTitleOverride) ? weaponName : phaseTitleOverride);
            UpdateRemainingLabel(pendingUpgrades);

            ClearButtonListeners();

            aListener = () => PickWeaponUpgrade(weaponA);
            bListener = () => PickWeaponUpgrade(weaponB);
            cListener = () => PickWeaponUpgrade(weaponC);

            aBtn.onClick.AddListener(aListener);
            bBtn.onClick.AddListener(bListener);
            cBtn.onClick.AddListener(cListener);

            OpenPanel(true);
        }

        void ClearButtonListeners()
        {
            if (aListener != null) aBtn.onClick.RemoveListener(aListener);
            if (bListener != null) bBtn.onClick.RemoveListener(bListener);
            if (cListener != null) cBtn.onClick.RemoveListener(cListener);
        }

        void UpdateRemainingLabel(int pendingUpgrades)
        {
            if (upgradesRemainingText)
            {
                if (showingWeaponCards)
                {
                    upgradesRemainingText.text = phaseTitleText != null
                        ? phaseTitleText.text
                        : "Weapon";
                }
                else
                {
                    upgradesRemainingText.text = $"Upgrades left: {Mathf.Max(0, pendingUpgrades)}";
                }

            }
        }

        void UpdatePhaseHeader(string weaponName)
        {
            if (!phaseTitleText)
            {
                return;
            }

            if (showingWeaponCards)
            {
                phaseTitleText.text = "Weapon mastery bonus";
            }
            else
            {
                if (string.IsNullOrEmpty(weaponName))
                    phaseTitleText.text = "Choose an upgrade";
                else
                    phaseTitleText.text = $"{weaponName}";
            }

        }

        void EnsureCardRoots()
        {
            if (cardRoots != null && cardRoots.Length > 0)
            {
                return;
            }

            cardRoots = new RectTransform[]
            {
                aBtn ? aBtn.transform as RectTransform : null,
                bBtn ? bBtn.transform as RectTransform : null,
                cBtn ? cBtn.transform as RectTransform : null
            };
        }

        void ResetCardScales(float targetScale = 1f)
        {
            if (cardRoots == null)
            {
                return;
            }

            for (int i = 0; i < cardRoots.Length; i++)
            {
                if (cardRoots[i])
                {
                    cardRoots[i].localScale = Vector3.one * targetScale;
                }
            }
        }

        void OpenPanel(bool animateCards)
        {
            panel.SetActive(true);

            if (animateCards)
            {
                PlayCardPopAnimation();
            }
            else
            {
                StopPopAnimation();
                ResetCardScales();
            }

            Time.timeScale = 0f;

            if (!IsShowing)
            {
                IsShowing = true;
                OnVisibilityChanged?.Invoke(true);
            }
        }

        void PlayCardPopAnimation()
        {
            StopPopAnimation();
            ResetCardScales(0f);

            if (cardRoots == null || cardRoots.Length == 0)
            {
                return;
            }

            popRoutine = StartCoroutine(PopRoutine());
        }

        System.Collections.IEnumerator PopRoutine()
        {
            float timer = 0f;
            float stepDelay = cardPopDuration * 0.35f;

            while (timer < cardPopDuration + stepDelay * (cardRoots.Length - 1))
            {
                timer += Time.unscaledDeltaTime;

                for (int i = 0; i < cardRoots.Length; i++)
                {
                    RectTransform root = cardRoots[i];
                    if (!root)
                    {
                        continue;
                    }

                    float progress = Mathf.Clamp01((timer - stepDelay * i) / Mathf.Max(0.001f, cardPopDuration));
                    float scale = cardPopCurve != null ? cardPopCurve.Evaluate(progress) : progress;
                    root.localScale = Vector3.one * scale;
                }

                yield return null;
            }

            ResetCardScales();
            popRoutine = null;
        }

        void StopPopAnimation()
        {
            if (popRoutine != null)
            {
                StopCoroutine(popRoutine);
                popRoutine = null;
            }
        }

        public void Hide()
        {
            StopPopAnimation();
            ResetCardScales();
            showingWeaponCards = false;
            callback = null;
            weaponCallback = null;
            panel.SetActive(false);
            Time.timeScale = 1f;
            if (IsShowing)
            {
                IsShowing = false;
                OnVisibilityChanged?.Invoke(false);
            }
        }

        internal static void ResetStaticState()
        {
            if (IsShowing)
            {
                IsShowing = false;
                OnVisibilityChanged?.Invoke(false);
            }

            if (Mathf.Abs(Time.timeScale) < Mathf.Epsilon)
            {
                Time.timeScale = 1f;
            }
        }
    }
}
