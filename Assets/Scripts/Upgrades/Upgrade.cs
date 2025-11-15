using UnityEngine;


namespace FF
{
    [CreateAssetMenu(menuName = "FF/Upgrade", fileName = "Upgrade_")]
    public class Upgrade : ScriptableObject
    {
        public string Title;
        [TextArea] public string Description;
        public enum Kind { DamageMult, FireRateMult, MoveMult }
        public Kind Type;
        public float Magnitude = 0.15f; // +15%


        public void Apply(PlayerStats stats)
        {
            switch (Type)
            {
                case Kind.DamageMult: stats.DamageMult += Magnitude; break;
                case Kind.FireRateMult: stats.FireRateMult += Magnitude; break;
                case Kind.MoveMult: stats.MoveMult += Magnitude; break;
            }
        }
    }
}