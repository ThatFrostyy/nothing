using UnityEngine;
using UnityEngine.UIElements;

namespace FF
{
    [CreateAssetMenu(menuName = "FF/Weapon", fileName = "Weapon_")]
    public class Weapon : ScriptableObject
    {
        [Header("Name")]
        public string weaponName;

        [Header("Audio & Visual")]
        public AudioClip fireSFX;
        public GameObject muzzleFlash;
        public GameObject ejectParticles;
        public float recoilAmount = 6f;
        public float recoilRecoverySpeed = 10f;

        [Header("UI")]
        public Sprite weaponIcon;
        public bool isSpecial;

        [Header("Grenades")]
        public bool useGrenadeCharging = true;

        [Header("Prefabs & Assets")]
        public GameObject bulletPrefab;
        public GameObject weaponPrefab;

        [Header("Stats")]
        public float rpm = 420f;
        public int damage = 10;
        public float recoilKick = 4f;
        public bool isAuto = true;
        public float fireCooldown = 0.1f; // for semi-auto

        [Header("Impact")]
        public float knockbackStrength = 0f;
        public float knockbackDuration = 0.2f;

        [Header("Accuracy / Spread")]
        public float baseSpread = 1.5f;           // degrees
        public float maxSpread = 6f;              // degrees (auto increases up to this)
        public float spreadIncreasePerShot = 0.5f;
        public float spreadRecoverySpeed = 2f;    // how fast spread shrinks when not firing
    }
}
