using UnityEngine;

namespace FF
{
    public class RecoilPresenter : MonoBehaviour
    {
        [SerializeField] private bool _cameraShakeEnabled = true;
        [SerializeField] private float maxSustainedShake = 0.35f;

        private Weapon _weapon;
        private float _sustainedFireTime;

        public void SetWeapon(Weapon weapon)
        {
            _weapon = weapon;
            _sustainedFireTime = 0f;
        }

        public void SetCameraShakeEnabled(bool enabled)
        {
            _cameraShakeEnabled = enabled;
        }

        public void UpdateHold(bool isFireHeld, bool isAuto, float deltaTime)
        {
            if (isFireHeld && isAuto)
            {
                _sustainedFireTime += deltaTime;
                _sustainedFireTime = Mathf.Clamp(_sustainedFireTime, 0f, 1f);
            }
            else
            {
                _sustainedFireTime -= deltaTime * 1.5f;
                _sustainedFireTime = Mathf.Clamp(_sustainedFireTime, 0f, 1f);
            }
        }

        public void HandleShot(Weapon weapon)
        {
            if (!_cameraShakeEnabled || weapon == null)
            {
                return;
            }

            float shakeStrength = Mathf.Lerp(0.05f, maxSustainedShake, _sustainedFireTime);
            CameraShake.Shake(shakeStrength, shakeStrength);
        }
    }
}
