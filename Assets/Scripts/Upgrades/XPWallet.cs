using UnityEngine;


namespace FF
{
    public class XPWallet : MonoBehaviour
    {
        [Header("Level Progression")]
        [SerializeField, Min(1)] int startingLevel = 1;
        [SerializeField, Min(0)] int startingXP = 0;
        [SerializeField, Min(1)] int baseXPRequirement = 5;
        [SerializeField, Min(0f)] float xpPerLevel = 3f;
        [SerializeField] AnimationCurve xpRequirementCurve;

        public int Level { get; private set; } = 1;
        public int XP { get; private set; } = 0;
        public int Next => GetXPRequirementForLevel(Level);

        public System.Action<int> OnLevelUp;
        public System.Action<int, int, int> OnXPChanged;

        void Awake()
        {
            ResetLevels(startingLevel, startingXP, false);
        }

        private void Start()
        {

            if (UpgradeManager.I != null)
                UpgradeManager.I.RegisterWallet(this);
        }

        public void Add(int v)
        {
            if (v <= 0)
            {
                return;
            }

            XP += v;

            ProcessPendingLevels(true);

            NotifyXPChanged();
        }

        public int GetXPRequirementForLevel(int level)
        {
            level = Mathf.Max(1, level);

            if (xpRequirementCurve != null && xpRequirementCurve.length > 0)
            {
                float evaluated = xpRequirementCurve.Evaluate(level);
                if (evaluated > 0f)
                {
                    return Mathf.Max(1, Mathf.RoundToInt(evaluated));
                }
            }

            float requirement = baseXPRequirement + (level - 1) * xpPerLevel;
            return Mathf.Max(1, Mathf.RoundToInt(requirement));
        }

        public void ResetLevels(int level = 1, int xp = 0, bool clampXP = true)
        {
            Level = Mathf.Max(1, level);
            if (clampXP)
            {
                XP = Mathf.Clamp(xp, 0, Mathf.Max(0, Next - 1));
            }
            else
            {
                XP = Mathf.Max(0, xp);
                ProcessPendingLevels(false);
            }

            NotifyXPChanged();
        }

        void ProcessPendingLevels(bool raiseEvents)
        {
            while (XP >= Next)
            {
                XP -= Next;
                Level++;

                if (raiseEvents)
                {
                    OnLevelUp?.Invoke(Level);
                }
            }
        }

        void NotifyXPChanged()
        {
            OnXPChanged?.Invoke(Level, XP, Next);
        }

        void OnValidate()
        {
            startingLevel = Mathf.Max(1, startingLevel);
            startingXP = Mathf.Max(0, startingXP);
            baseXPRequirement = Mathf.Max(1, baseXPRequirement);
            xpPerLevel = Mathf.Max(0f, xpPerLevel);
        }
    }
}