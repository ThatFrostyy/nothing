using UnityEngine;

namespace FF
{
    public class MortarShell : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private float explosionRadius = 2.5f;
        [SerializeField] private GameObject explosionVfx;
        [SerializeField] private float peakHeight = 8f;

        private Vector3 _startPosition;
        private Vector3 _targetPosition;
        private float _duration;
        private float _elapsedTime;
        private bool _isFalling;

        private int _damage;
        private string _ownerTag;
        private Weapon _sourceWeapon;
        private bool _isCrit;

        public void Launch(Vector3 target, float fallSpeed, int damage, string ownerTag, Weapon sourceWeapon, bool isCrit)
        {
            InitializeShared(target, damage, ownerTag, sourceWeapon, isCrit);

            if (fallSpeed <= 0) fallSpeed = 10f;
            _duration = 2f * peakHeight / fallSpeed;

            _isFalling = false;
        }

        public void Fall(Vector3 target, float fallSpeed, int damage, string ownerTag, Weapon sourceWeapon, bool isCrit)
        {
            InitializeShared(target, damage, ownerTag, sourceWeapon, isCrit);

            if (fallSpeed <= 0) fallSpeed = 10f;
            float distance = Vector3.Distance(_startPosition, _targetPosition);
            _duration = distance / fallSpeed;

            _isFalling = true;
        }

        private void InitializeShared(Vector3 target, int damage, string ownerTag, Weapon sourceWeapon, bool isCrit)
        {
            _startPosition = transform.position;
            _targetPosition = target;
            _damage = damage;
            _ownerTag = ownerTag;
            _sourceWeapon = sourceWeapon;
            _isCrit = isCrit;
            _elapsedTime = 0f;
        }

        private void Update()
        {
            if (_duration <= 0) return;

            _elapsedTime += Time.deltaTime;

            if (_elapsedTime >= _duration)
            {
                transform.position = _targetPosition;
                Explode();
                _duration = 0;
                return;
            }

            float t = _elapsedTime / _duration;
            if (_isFalling)
            {
                transform.position = Vector3.Lerp(_startPosition, _targetPosition, t);
            }
            else
            {
                Vector3 currentPos = Vector3.Lerp(_startPosition, _targetPosition, t);
                currentPos.y += Mathf.Sin(t * Mathf.PI) * peakHeight;
                transform.position = currentPos;
            }
        }

        private void Explode()
        {
            if (explosionVfx)
            {
                PoolManager.Get(explosionVfx, transform.position, Quaternion.identity);
            }

            Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
            foreach (var hit in hits)
            {
                if (hit.CompareTag(_ownerTag)) continue;

                if (hit.TryGetComponent<Health>(out var health))
                {
                    health.Damage(_damage, _sourceWeapon, _isCrit);
                }
            }

            Destroy(gameObject);
        }
    }
}
