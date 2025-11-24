using UnityEngine;

namespace FF
{
    public class RecoilPresenter : MonoBehaviour
    {
        [SerializeField] private GameSettings _gameSettings;
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

        private void OnValidate()
        {
            if (!_gameSettings)
            {
                _gameSettings = GameSettings.Instance;
            }
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

            GameSettings settings = Settings;
            if (settings && !settings.ScreenShakeEnabled)
            {
                return;
            }

            float recoilScale = settings ? settings.RecoilMultiplier : 1f;

            float shakeStrength = Mathf.Lerp(0.05f, maxSustainedShake, _sustainedFireTime) * recoilScale;
            CameraShake.Shake(shakeStrength, shakeStrength);
        }

        private GameSettings Settings => _gameSettings != null ? _gameSettings : GameSettings.Instance;
    }
}
