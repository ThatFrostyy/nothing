using UnityEngine;

namespace FF
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(EnemyStats))]
    [RequireComponent(typeof(Health))]
    public class Enemy : MonoBehaviour
    {
        [Header("Combat Setup")]
        [SerializeField] private Weapon startingWeapon;
        [SerializeField] private WeaponManager weaponManager;
        [SerializeField] private AutoShooter autoShooter;

        [Header("Visuals")]
        [SerializeField] private Transform gunPivot;
        [SerializeField] private Transform enemyVisual;

        private Rigidbody2D _rigidbody;
        private EnemyStats _stats;
        private Health _health;
        private Transform _player;
        private Vector2 _desiredVelocity;

        public void Initialize(Transform player)
        {
            _player = player;
        }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _stats = GetComponent<EnemyStats>();
            _health = GetComponent<Health>();

            if (!weaponManager)
                weaponManager = GetComponentInChildren<WeaponManager>();

            if (!autoShooter)
            {
                if (weaponManager && weaponManager.Shooter)
                    autoShooter = weaponManager.Shooter;
                else
                    autoShooter = GetComponentInChildren<AutoShooter>();
            }

            if (!gunPivot && weaponManager)
                gunPivot = weaponManager.GunPivot;

            if (autoShooter)
            {
                autoShooter.SetStatsProvider(_stats);
                autoShooter.SetFireHeld(false);
            }

            EnsurePlayerReference();
        }

        private void OnEnable()
        {
            if (_health != null)
                _health.OnDeath += HandleDeath;
        }

        private void Start()
        {
            if (weaponManager && startingWeapon)
                weaponManager.Equip(startingWeapon);
        }

        private void OnDisable()
        {
            if (_health != null)
                _health.OnDeath -= HandleDeath;
        }

        private void Update()
        {
            EnsurePlayerReference();
            AimAtPlayer();
            HandleFiring();
        }

        private void FixedUpdate()
        {
            UpdateMovement();
            UpdateBodyTilt();
        }

        private void UpdateMovement()
        {
            Vector2 targetVelocity = Vector2.zero;

            if (_player)
            {
                Vector2 toPlayer = _player.position - transform.position;
                float distance = toPlayer.magnitude;
                Vector2 direction = distance > 0.001f ? toPlayer / distance : Vector2.zero;

                float buffer = _stats ? _stats.DistanceBuffer : 1f;
                float shootDistance = _stats ? _stats.ShootingDistance : 8f;
                float moveSpeed = _stats ? _stats.MoveSpeed : 3f;
                float retreatMultiplier = _stats ? _stats.RetreatSpeedMultiplier : 0.6f;

                if (distance > shootDistance + buffer)
                {
                    targetVelocity = direction * moveSpeed;
                }
                else if (distance < shootDistance - buffer)
                {
                    targetVelocity = -direction * moveSpeed * retreatMultiplier;
                }
            }

            _desiredVelocity = targetVelocity;

            float acceleration = _stats ? _stats.Acceleration : 0.2f;
            _rigidbody.linearVelocity = Vector2.Lerp(_rigidbody.linearVelocity, targetVelocity, acceleration);
        }

        private void UpdateBodyTilt()
        {
            if (!enemyVisual)
                return;

            float bodyTiltDegrees = _stats ? _stats.BodyTiltDegrees : 12f;
            float speed = _rigidbody.linearVelocity.magnitude;
            float maxSpeed = Mathf.Max(_stats ? _stats.MoveSpeed : 1f, Mathf.Epsilon);
            float normalizedSpeed = speed / maxSpeed;

            float targetTilt = speed > 0.1f ? -bodyTiltDegrees * normalizedSpeed : bodyTiltDegrees * 0.3f;

            if (_desiredVelocity.sqrMagnitude > 0.01f)
            {
                float side = Mathf.Clamp(_desiredVelocity.normalized.x, -1f, 1f);
                targetTilt += side * (bodyTiltDegrees * 0.5f);
            }

            float currentZ = enemyVisual.localEulerAngles.z;
            if (currentZ > 180f)
                currentZ -= 360f;

            float newZ = Mathf.Lerp(currentZ, targetTilt, 0.15f);
            enemyVisual.localRotation = Quaternion.Euler(0f, 0f, newZ);
        }

        private void AimAtPlayer()
        {
            if (!gunPivot || !_player)
                return;

            Vector2 direction = _player.position - gunPivot.position;
            if (direction.sqrMagnitude < 0.001f)
                return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            gunPivot.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

            bool isAimingLeft = direction.x < 0f;
            gunPivot.localScale = isAimingLeft ? new Vector3(1f, -1f, 1f) : Vector3.one;

            if (enemyVisual)
                enemyVisual.localScale = isAimingLeft ? new Vector3(-1f, 1f, 1f) : Vector3.one;
        }

        private void HandleFiring()
        {
            if (!autoShooter)
                return;

            if (!_player)
            {
                autoShooter.SetFireHeld(false);
                return;
            }

            float shootDistance = _stats ? _stats.ShootingDistance : 8f;
            float buffer = _stats ? _stats.DistanceBuffer : 1f;
            float distance = Vector2.Distance(transform.position, _player.position);

            bool inRange = distance <= shootDistance + buffer;
            autoShooter.SetFireHeld(inRange);
        }

        private void EnsurePlayerReference()
        {
            if (_player)
                return;

            var playerObject = GameObject.FindWithTag("Player");
            if (playerObject)
                _player = playerObject.transform;
        }

        private void HandleDeath()
        {
            if (autoShooter)
                autoShooter.SetFireHeld(false);
        }

        private void OnDrawGizmosSelected()
        {
            if (!_stats)
                _stats = GetComponent<EnemyStats>();

            float shootDistance = _stats ? _stats.ShootingDistance : 8f;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, shootDistance);
        }
    }
}
