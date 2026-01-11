using System.Collections;
using UnityEngine;

namespace FF
{
    public class ArtilleryStrikeController : MonoBehaviour
    {
        private Weapon _sourceWeapon;
        private Vector3 _strikePosition;

        public void BeginStrike(Weapon weapon, Vector3 position)
        {
            _sourceWeapon = weapon;
            _strikePosition = position;
            StartCoroutine(StrikeRoutine());
        }

        private IEnumerator StrikeRoutine()
        {
            if (_sourceWeapon.artilleryCircleVFX)
            {
                GameObject circle = Instantiate(_sourceWeapon.artilleryCircleVFX, _strikePosition, Quaternion.identity);
                Destroy(circle, _sourceWeapon.artilleryShellAmount * _sourceWeapon.artilleryShellDelay + 2f); // a little extra time
            }

            if (_sourceWeapon.artillerySFX)
            {
                AudioPlaybackPool.PlayOneShot(_sourceWeapon.artillerySFX, _strikePosition, null, 1f, 1f, 1f);
            }

            for (int i = 0; i < _sourceWeapon.artilleryShellAmount; i++)
            {
                yield return new WaitForSeconds(_sourceWeapon.artilleryShellDelay);
                SpawnShell();
            }

            Destroy(gameObject, 1f);
        }

        private void SpawnShell()
        {
            if (!_sourceWeapon.bulletPrefab) return;

            Vector2 randomOffset = Random.insideUnitCircle * _sourceWeapon.artilleryCircleSize;
            Vector3 shellTargetPosition = _strikePosition + new Vector3(randomOffset.x, 0, randomOffset.y);

            Vector3 spawnPosition = shellTargetPosition + Vector3.up * 20f;

            GameObject shellInstance = Instantiate(_sourceWeapon.bulletPrefab, spawnPosition, Quaternion.identity);
            if (shellInstance.TryGetComponent<MortarShell>(out var mortarShell))
            {
                // We'll reuse the mortar shell logic for the falling shells.
                // We'll give it a slightly randomized fall speed for a more natural look.
                float fallSpeed = _sourceWeapon.mortarShellFallSpeed * Random.Range(0.8f, 1.2f);
                mortarShell.Fall(shellTargetPosition, fallSpeed, _sourceWeapon.damage, "Player", _sourceWeapon, false);
            }
        }
    }
}
