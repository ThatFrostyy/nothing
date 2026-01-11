using UnityEngine;
using UnityEngine.UIElements;

namespace FF
{
    [CreateAssetMenu(menuName = "FF/Weapon", fileName = "Weapon_")]
    public class Weapon : ScriptableObject
    {
        public enum WeaponClass
        {
            General,
            SemiRifle,
            MG,
            SMG,
            Special,
            Shotgun,
            Flamethrower,
            Melee
        }

        [Header("Name")]
        public string weaponName;

        [Header("Audio & Visual")]
        public AudioClip fireSFX;
        public GameObject muzzleFlash;
        public GameObject ejectParticles;
        public float recoilAmount = 6f;
        public float recoilRecoverySpeed = 10f;

        [Header("Looping Audio & VFX")]
        public AudioClip attackLoopSFX;
        public AudioClip fireLoopSFX;
        public GameObject loopingFireVfx;
        public Vector3 loopingVfxOffset = Vector3.zero;

        [Header("Classification")]
        public WeaponClass weaponClass = WeaponClass.General;
        public bool isShotgun = false;

        [Header("UI")]
        public Sprite weaponIcon;
        public bool isSpecial;

        [Header("Grenades")]
        public bool useGrenadeCharging = true;

        [Header("Prefabs & Assets")]
        public GameObject bulletPrefab;
        public GameObject weaponPrefab;

        [Header("Stats")]
        [Min(0)] public int maxUses = 0; // 0 = Infinite
        public float rpm = 420f;
        public int damage = 10;
        public float recoilKick = 4f;
        public bool isAuto = true;
        public float fireCooldown = 0.1f; // for semi-auto

        [Header("Melee")]
        public bool isMelee = false;
        public float attackRange = 1.5f;
        public float attackArc = 90f;
        public float swingSpeed = 360f;

        [Header("Impact")]
        public float knockbackStrength = 0f;
        public float knockbackDuration = 0.2f;

        [Header("Damage Over Time")]
        public bool appliesBurn = false;
        [Min(0f)] public float burnDuration = 3f;
        [Min(0)] public int burnDamagePerSecond = 5;
        [Min(0.05f)] public float burnTickInterval = 0.35f;
        public Vector3 burnTargetVfxOffset = Vector3.zero;
        public GameObject burnTargetVfx;
        public GameObject burnImpactVfx;

        [Header("Flamethrower")]
        public bool isFlamethrower = false;
        [Min(0.1f)] public float flamethrowerRange = 5f;
        [Range(1f, 180f)] public float flamethrowerConeAngle = 45f;
        [Min(0.05f)] public float flamethrowerTickInterval = 0.1f;
        [Min(1)] public int flamethrowerDamagePerSecond = 18;
        public LayerMask flamethrowerHitMask;
        public GameObject flamethrowerEmitterPrefab;
        public bool useFlamethrowerBurst = false;
        [Min(0.1f)] public float flamethrowerBurstDuration = 3f;
        [Min(0.1f)] public float flamethrowerOverheatCooldown = 2.5f;
        [Tooltip("Optional backpack cosmetic to show when this flamethrower is equipped.")]
        public GameObject flamethrowerBackpackPrefab;

        [Header("Accuracy / Spread")]
        public float baseSpread = 1.5f;           // degrees
        public float maxSpread = 6f;              // degrees (auto increases up to this)
        public float spreadIncreasePerShot = 0.5f;
        public float spreadRecoverySpeed = 2f;    // how fast spread shrinks when not firing

        [Header("Shotgun")]
        [Min(1)] public int pelletsPerShot = 6;

        [Header("Mortar")]
        public bool isMortar = false;
        public float mortarMinRange = 5f;
        public float mortarMaxRange = 20f;
        public float mortarShellFallSpeed = 10f;

        [Header("Artillery Strike")]
        public bool isArtilleryStrike = false;
        public float artilleryCircleSize = 5f;
        public int artilleryShellAmount = 10;
        public float artilleryShellDelay = 0.2f;
        public AudioClip artillerySFX;
        public GameObject artilleryCircleVFX;
        public LayerMask groundLayerMask;
    }
}
