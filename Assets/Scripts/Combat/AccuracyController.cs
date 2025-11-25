using UnityEngine;

namespace FF
{
    public class AccuracyController : MonoBehaviour
    {
        private float _currentSpread;

        public void SetWeapon(Weapon weapon)
        {
            _currentSpread = weapon ? weapon.baseSpread : 0f;
        }

        public void TickSpread(Weapon weapon, Rigidbody2D body, ICombatStats stats, float deltaTime)
        {
            if (weapon == null)
            {
                _currentSpread = 0f;
                return;
            }

            float movementSpeed = body ? body.linearVelocity.magnitude : 0f;
            bool isMoving = movementSpeed > 0.1f;

            float movementPenalty = stats != null ? stats.GetMovementAccuracyPenalty() : 1f;
            float targetSpread = weapon.baseSpread * (isMoving ? movementPenalty : 1f);
            _currentSpread = Mathf.Lerp(_currentSpread, targetSpread, deltaTime * weapon.spreadRecoverySpeed);
        }

        public void RegisterShot(Weapon weapon, Rigidbody2D body, ICombatStats stats)
        {
            if (weapon == null)
            {
                return;
            }

            bool isMoving = body && body.linearVelocity.magnitude > 0.1f;
            float movementPenalty = stats != null ? stats.GetMovementAccuracyPenalty() : 1f;
            float maxSpread = weapon.maxSpread * (isMoving ? movementPenalty : 1f);

            _currentSpread += weapon.spreadIncreasePerShot;
            _currentSpread = Mathf.Clamp(_currentSpread, weapon.baseSpread, maxSpread);
        }

        public Quaternion GetShotRotation(Weapon weapon, Quaternion muzzleRotation)
        {
            if (weapon == null)
            {
                return muzzleRotation;
            }

            float angleOffset = Random.Range(-_currentSpread, _currentSpread);
            return muzzleRotation * Quaternion.AngleAxis(angleOffset, Vector3.forward);
        }
    }
}
