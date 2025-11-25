using UnityEngine;
using UnityEngine.Audio;

namespace FF
{
    public class ProjectileSpawner : MonoBehaviour
    {
        private Weapon _weapon;
        private Transform _muzzle;
        private Transform _eject;
        private bool _isGrenadeWeapon;

        public void ConfigureWeapon(Weapon weapon, Transform muzzle, Transform eject, bool isGrenadeWeapon)
        {
            _weapon = weapon;
            _muzzle = muzzle;
            _eject = eject;
            _isGrenadeWeapon = isGrenadeWeapon;
        }

        public void ClearWeapon()
        {
            _weapon = null;
            _muzzle = null;
            _eject = null;
            _isGrenadeWeapon = false;
        }

        public void SpawnShot(Weapon weapon, Quaternion spreadRotation, float? grenadeSpeedOverride, ICombatStats stats, string ownerTag)
        {
            if (weapon == null || _muzzle == null)
            {
                return;
            }

            SpawnEjectParticles();

            if (_isGrenadeWeapon && TryLaunchGrenade(spreadRotation, grenadeSpeedOverride, stats, ownerTag))
            {
                SpawnMuzzleFlash();
                return;
            }

            SpawnStandardBullet(spreadRotation, stats, ownerTag);
            SpawnMuzzleFlash();
        }

        private void SpawnEjectParticles()
        {
            if (_weapon == null || _weapon.ejectParticles == null || _eject == null)
            {
                return;
            }

            GameObject ejectInstance = PoolManager.Get(_weapon.ejectParticles, _eject.position, _eject.rotation);
            if (ejectInstance && !ejectInstance.TryGetComponent<PooledParticleSystem>(out var ejectPooled))
            {
                ejectPooled = ejectInstance.AddComponent<PooledParticleSystem>();
                ejectPooled.OnTakenFromPool();
            }
        }

        private void SpawnStandardBullet(Quaternion spreadRotation, ICombatStats stats, string ownerTag)
        {
            if (_weapon == null || !_weapon.bulletPrefab)
            {
                return;
            }

            GameObject bulletInstance = PoolManager.Get(_weapon.bulletPrefab, _muzzle.position, spreadRotation);

            if (bulletInstance.TryGetComponent<Bullet>(out var bullet))
            {
                float damageMultiplier = stats != null ? stats.GetDamageMultiplier() : 1f;
                float critChance = stats != null ? stats.GetCritChance() : 0f;
                float critDamageMultiplier = stats != null ? stats.GetCritDamageMultiplier() : 1f;
                bool didCrit = critChance > 0f && Random.value < critChance;
                float finalDamageMultiplier = didCrit ? damageMultiplier * Mathf.Max(1f, critDamageMultiplier) : damageMultiplier;

                float projectileSpeedMultiplier = stats != null ? stats.GetProjectileSpeedMultiplier() : 1f;
                bullet.SetDamage(Mathf.RoundToInt(_weapon.damage * finalDamageMultiplier));
                bullet.SetOwner(ownerTag);
                bullet.SetSpeed(bullet.BaseSpeed * Mathf.Max(0.01f, projectileSpeedMultiplier));
            }
        }

        private bool TryLaunchGrenade(Quaternion spreadRotation, float? speedOverride, ICombatStats stats, string ownerTag)
        {
            if (_weapon == null || !_weapon.bulletPrefab)
            {
                return false;
            }

            if (!_weapon.bulletPrefab.TryGetComponent<GrenadeProjectile>(out _))
            {
                return false;
            }

            GameObject grenadeInstance = PoolManager.Get(_weapon.bulletPrefab, _muzzle.position, spreadRotation);
            if (!grenadeInstance.TryGetComponent<GrenadeProjectile>(out var grenade))
            {
                return false;
            }

            float damageMultiplier = stats != null ? stats.GetDamageMultiplier() : 1f;
            float projectileSpeedMultiplier = stats != null ? stats.GetProjectileSpeedMultiplier() : 1f;

            Vector2 direction = spreadRotation * Vector3.right;
            AudioMixerGroup mixer = null;
            float spatialBlend = 0f;
            float volume = 1f;
            float pitch = 1f;

            float baseLaunchSpeed = grenade.BaseLaunchSpeed;
            float finalLaunchSpeed = speedOverride.HasValue
                ? Mathf.Max(0.1f, speedOverride.Value * projectileSpeedMultiplier)
                : Mathf.Max(0.1f, baseLaunchSpeed * projectileSpeedMultiplier);

            grenade.Launch(direction, _weapon.damage, damageMultiplier, ownerTag, mixer, spatialBlend, volume, pitch, null, finalLaunchSpeed);
            return true;
        }

        private void SpawnMuzzleFlash()
        {
            if (_weapon == null || _weapon.muzzleFlash == null)
            {
                return;
            }

            GameObject flashInstance = PoolManager.Get(_weapon.muzzleFlash, _muzzle.position, _muzzle.rotation);
            if (flashInstance && !flashInstance.TryGetComponent<PooledParticleSystem>(out var flashPooled))
            {
                flashPooled = flashInstance.AddComponent<PooledParticleSystem>();
                flashPooled.OnTakenFromPool();
            }
        }
    }
}
