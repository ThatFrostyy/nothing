using UnityEngine;


namespace FF
{
    public class XPWallet : MonoBehaviour
    {
        public int Level { get; private set; } = 1;
        public int XP { get; private set; } = 0;
        public int Next => 5 + (Level * 3);

        public System.Action<int> OnLevelUp;
        public System.Action<int, int, int> OnXPChanged;

        public void Add(int v)
        {
            if (v <= 0)
            {
                return;
            }

            XP += v;

            while (XP >= Next)
            {
                XP -= Next;
                Level++;
                var levelUpHandler = OnLevelUp;
                if (levelUpHandler != null)
                {
                    levelUpHandler(Level);
                }
            }

            var xpChangedHandler = OnXPChanged;
            if (xpChangedHandler != null)
            {
                xpChangedHandler(Level, XP, Next);
            }
        }

        public void ResetLevels(int level = 1)
        {
            Level = Mathf.Max(1, level);
            XP = 0;
            var xpChangedHandler = OnXPChanged;
            if (xpChangedHandler != null)
            {
                xpChangedHandler(Level, XP, Next);
            }
        }
    }
}