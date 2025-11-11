using UnityEngine;
using UnityEngine.InputSystem;

namespace FF
{
    public class AutoShooter : MonoBehaviour
    {
        Weapon weaponSO;
        Transform muzzle;
        PlayerStats stats;
        AudioSource audioSource;
        Rigidbody2D playerBody;

        float timer;
        bool fireHeld;     
        bool firePressed;  // For semi-auto detecting click

        float recoilTimer;
        float currentSpread;
        float currentRecoil;  // visual recoil
        Vector3 baseLocalPos;
        Transform gunPivot;

        void Awake()
        {
            stats = GetComponentInParent<PlayerStats>();
            audioSource = GetComponent<AudioSource>();
            playerBody = GetComponentInParent<Rigidbody2D>();
        }

        public void InitializeRecoil(Transform gunPivotTransform)
        {
            gunPivot = gunPivotTransform;
        }

        public void SetWeapon(Weapon so, Transform muzzleTransform)
        {
            weaponSO = so;
            muzzle = muzzleTransform;
            currentSpread = weaponSO.baseSpread;

            baseLocalPos = gunPivot.localPosition;  // good
            currentRecoil = 0f;                     // reset recoil
            recoilTimer = 0f;                       // reset recovery curve
            gunPivot.localPosition = baseLocalPos;  // reset drift
        }

        // Input System call from PlayerController
        public void OnFire(InputValue v)
        {
            float val = v.Get<float>();

            firePressed = val > 0.5f && !fireHeld; // true ONLY on first press
            fireHeld = val > 0.5f;
        }

        void Update()
        {
            if (weaponSO == null || muzzle == null) return;

            timer += Time.deltaTime;

            float interval = 60f / (weaponSO.rpm * stats.FireRateMult);

            bool canShoot = timer >= interval;

            if (canShoot)
            {
                // Semi-auto: shoot only on press
                if (!weaponSO.isAuto && firePressed)
                {
                    timer = 0f;
                    firePressed = false; 
                    Shoot();
                }

                // Auto: shoot while holding
                if (weaponSO.isAuto && fireHeld)
                {
                    timer = 0f;
                    Shoot();
                }
            }

            float movementSpeed = playerBody ? playerBody.linearVelocity.magnitude : 0f;
            bool isMoving = movementSpeed > 0.1f;

            float targetSpread = weaponSO.baseSpread * (isMoving ? stats.MovementAccuracyPenalty : 1f);
            currentSpread = Mathf.Lerp(currentSpread, targetSpread, Time.deltaTime * weaponSO.spreadRecoverySpeed);

            UpdateRecoil();
        }

        void Shoot()
        {
            currentSpread += weaponSO.spreadIncreasePerShot;
            float maxSpread = weaponSO.maxSpread * (playerBody && playerBody.linearVelocity.magnitude > 0.1f ? stats.MovementAccuracyPenalty : 1f);
            currentSpread = Mathf.Clamp(currentSpread, weaponSO.baseSpread, maxSpread);

            // Get random angle inside cone
            float angleOffset = Random.Range(-currentSpread, currentSpread);

            // Convert to quaternion
            Quaternion spreadRot = muzzle.rotation * Quaternion.AngleAxis(angleOffset, Vector3.forward);

            GameObject b = Instantiate(weaponSO.bulletPrefab, muzzle.position, spreadRot);

            if (b.TryGetComponent<Bullet>(out var bullet))
                bullet.SetDamage(Mathf.RoundToInt(weaponSO.damage * stats.DamageMult));

            if (weaponSO.fireSFX)
                audioSource.PlayOneShot(weaponSO.fireSFX);

            if (weaponSO.muzzleFlash)
                Instantiate(weaponSO.muzzleFlash, muzzle.position, muzzle.rotation);

            currentRecoil = weaponSO.recoilAmount;
            recoilTimer = 0f;
        }


        void UpdateRecoil()
        {
            if (!gunPivot) return;

            // Always reset to base position
            gunPivot.localPosition = baseLocalPos;

            // Smooth recoil push
            recoilTimer += Time.deltaTime * 10f;
            float kick = Mathf.Lerp(currentRecoil, 0f, recoilTimer);

            // Recoil in world direction
            Vector3 recoilDir = -gunPivot.right * (kick * 0.1f);

            // Convert recoil direction to local space so it works even when flipping left/right
            Vector3 localRecoil = gunPivot.parent.InverseTransformDirection(recoilDir);

            // Apply full local offset
            gunPivot.localPosition = baseLocalPos + localRecoil;

            // Smoothly return recoil to zero
            currentRecoil = Mathf.Lerp(currentRecoil, 0f, Time.deltaTime * weaponSO.recoilRecoverySpeed);
        }

    }
}
