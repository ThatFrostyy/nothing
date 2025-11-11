using UnityEngine;


namespace FF
{
    public class XPWallet : MonoBehaviour
    {
        public int Level { get; private set; } = 1;
        public int XP { get; private set; } = 0;
        public int Next => 5 + (Level * 3);

        public System.Action<int> OnLevelUp;


        public void Add(int v)
        {
            XP += v;
            if (XP >= Next)
            {
                XP -= Next; Level++;
                OnLevelUp?.Invoke(Level);
            }
        }
    }
}