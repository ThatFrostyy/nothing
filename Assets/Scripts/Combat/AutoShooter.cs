using UnityEngine;
using UnityEngine.InputSystem;

namespace FF
{
    public class AutoShooter : MonoBehaviour
    {
        private Weapon _weapon;
        private Transform _muzzle;
        private PlayerStats _stats;
        private AudioSource _audioSource;
        private Rigidbody2D _playerBody;

        private float _fireTimer;
        private bool _isFireHeld;
        private bool _isFirePressed;

        private float _recoilTimer;
        private float _currentSpread;
        private float _currentRecoil;
        private Vector3 _baseLocalPosition;
        private Transform _gunPivot;

        private void Awake()
        {
            _stats = GetComponentInParent<PlayerStats>();
            _audioSource = GetComponent<AudioSource>();
            _playerBody = GetComponentInParent<Rigidbody2D>();
        }

        public void InitializeRecoil(Transform gunPivotTransform)
        {
            _gunPivot = gunPivotTransform;
        }

        public void SetWeapon(Weapon weapon, Transform muzzleTransform)
        {
            _weapon = weapon;
            _muzzle = muzzleTransform;
            _currentSpread = _weapon.baseSpread;

            if (_gunPivot)
            {
                _baseLocalPosition = _gunPivot.localPosition;
                _currentRecoil = 0f;
                _recoilTimer = 0f;
                _gunPivot.localPosition = _baseLocalPosition;
            }
        }

        public void OnFire(InputValue value)
        {
            float inputValue = value.Get<float>();

            _isFirePressed = inputValue > 0.5f && !_isFireHeld;
            _isFireHeld = inputValue > 0.5f;
        }

        private void Update()
        {
            if (_weapon == null || _muzzle == null || _stats == null)
                return;

            _fireTimer += Time.deltaTime;

            float rpm = Mathf.Max(_weapon.rpm * _stats.FireRateMult, 0.01f);
            float interval = 60f / rpm;

            if (_fireTimer >= interval)
            {
                if (!_weapon.isAuto && _isFirePressed)
                {
                    _fireTimer = 0f;
                    _isFirePressed = false;
                    Shoot();
                }

                if (_weapon.isAuto && _isFireHeld)
                {
                    _fireTimer = 0f;
                    Shoot();
                }
            }

            float movementSpeed = _playerBody ? _playerBody.velocity.magnitude : 0f;
            bool isMoving = movementSpeed > 0.1f;

            float targetSpread = _weapon.baseSpread * (isMoving ? _stats.MovementAccuracyPenalty : 1f);
            _currentSpread = Mathf.Lerp(_currentSpread, targetSpread, Time.deltaTime * _weapon.spreadRecoverySpeed);

            UpdateRecoil();
        }

        private void Shoot()
        {
            _currentSpread += _weapon.spreadIncreasePerShot;

            bool isMoving = _playerBody && _playerBody.velocity.magnitude > 0.1f;
            float maxSpread = _weapon.maxSpread * (isMoving ? _stats.MovementAccuracyPenalty : 1f);
            _currentSpread = Mathf.Clamp(_currentSpread, _weapon.baseSpread, maxSpread);

            float angleOffset = Random.Range(-_currentSpread, _currentSpread);
            Quaternion spreadRotation = _muzzle.rotation * Quaternion.AngleAxis(angleOffset, Vector3.forward);

            GameObject bulletInstance = Instantiate(_weapon.bulletPrefab, _muzzle.position, spreadRotation);

            if (bulletInstance.TryGetComponent<Bullet>(out var bullet))
                bullet.SetDamage(Mathf.RoundToInt(_weapon.damage * _stats.DamageMult));

            if (_weapon.fireSFX && _audioSource)
                _audioSource.PlayOneShot(_weapon.fireSFX);

            if (_weapon.muzzleFlash)
                Instantiate(_weapon.muzzleFlash, _muzzle.position, _muzzle.rotation);

            _currentRecoil = _weapon.recoilAmount;
            _recoilTimer = 0f;
        }

        private void UpdateRecoil()
        {
            if (!_gunPivot)
                return;

            _gunPivot.localPosition = _baseLocalPosition;

            _recoilTimer += Time.deltaTime * 10f;
            float kick = Mathf.Lerp(_currentRecoil, 0f, _recoilTimer);

            Vector3 recoilDirection = -_gunPivot.right * (kick * 0.1f);
            Transform pivotParent = _gunPivot.parent;
            Vector3 localRecoil = pivotParent ? pivotParent.InverseTransformDirection(recoilDirection) : recoilDirection;

            _gunPivot.localPosition = _baseLocalPosition + localRecoil;

            _currentRecoil = Mathf.Lerp(_currentRecoil, 0f, Time.deltaTime * _weapon.recoilRecoverySpeed);
        }
    }
}
