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
        [SerializeField] private float walkBobFrequency = 6f;
        [SerializeField] private float walkBobAmplitude = 0.12f;
        [SerializeField] private float walkSquashAmount = 0.08f;
        [SerializeField] private float idleSwayFrequency = 1.5f;
        [SerializeField] private float idleSwayAmplitude = 3f;

        private Rigidbody2D _rigidbody;
        private EnemyStats _stats;
        private Health _health;
        private Transform _player;
        private Vector2 _desiredVelocity;
        private Vector3 _baseVisualLocalPosition;
        private Vector3 _baseVisualLocalScale = Vector3.one;
        private float _bobTimer;
        private bool _isFacingLeft;

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
            {
                weaponManager = GetComponentInChildren<WeaponManager>();
            }

            if (!autoShooter)
            {
                if (weaponManager && weaponManager.Shooter)
                {
                    autoShooter = weaponManager.Shooter;
                }
                else
                {
                    autoShooter = GetComponentInChildren<AutoShooter>();
                }
            }

            if (!gunPivot && weaponManager)
            {
                gunPivot = weaponManager.GunPivot;
            }

            if (!enemyVisual)
            {
                Transform visualTransform = transform.Find("Visual");
                if (visualTransform)
                {
                    enemyVisual = visualTransform;
                }
                else
                {
                    var spriteRenderer = GetComponentInChildren<SpriteRenderer>();
                    if (spriteRenderer)
                    {
                        enemyVisual = spriteRenderer.transform;
                    }
                }
            }

            if (enemyVisual)
            {
                _baseVisualLocalPosition = enemyVisual.localPosition;
                _baseVisualLocalScale = enemyVisual.localScale;
                _isFacingLeft = enemyVisual.localScale.x < 0f;
            }

            if (autoShooter)
            {
                autoShooter.SetStatsProvider(_stats);
                autoShooter.SetFireHeld(false);
                autoShooter.SetCameraShakeEnabled(false);
            }

            EnsurePlayerReference();
        }


        private void Start()
        {
            if (weaponManager && startingWeapon)
            {
                weaponManager.Equip(startingWeapon);
            }
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

        private void OnEnable()
        {
            if (_health != null)
            {
                _health.OnDeath += HandleDeath;
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.OnDeath -= HandleDeath;
            }
        }

        #region Movement
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

        private void AimAtPlayer()
        {
            if (!gunPivot || !_player) return;

            Vector2 direction = _player.position - gunPivot.position;
            if (direction.sqrMagnitude < 0.001f) return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            gunPivot.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

            bool isAimingLeft = direction.x < 0f;
            gunPivot.localScale = isAimingLeft ? new Vector3(1f, -1f, 1f) : Vector3.one;
            _isFacingLeft = isAimingLeft;
        }
#endregion Movement

        #region Animations
        private void UpdateBodyTilt()
        {
            if (!enemyVisual) return;

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

            if (normalizedSpeed < 0.05f)
            {
                targetTilt += Mathf.Sin(Time.time * idleSwayFrequency) * idleSwayAmplitude;
            }

            float currentZ = enemyVisual.localEulerAngles.z;
            if (currentZ > 180f)
            {
                currentZ -= 360f;
            }

            float newZ = Mathf.Lerp(currentZ, targetTilt, 0.15f);
            enemyVisual.localRotation = Quaternion.Euler(0f, 0f, newZ);

            UpdateWalkCycle(normalizedSpeed);
        }

        private void UpdateWalkCycle(float normalizedSpeed)
        {
            if (!enemyVisual)
            {
                return;
            }

            float absBaseScaleX = Mathf.Approximately(_baseVisualLocalScale.x, 0f) ? 1f : Mathf.Abs(_baseVisualLocalScale.x);
            float baseScaleY = Mathf.Approximately(_baseVisualLocalScale.y, 0f) ? 1f : _baseVisualLocalScale.y;
            float baseScaleZ = Mathf.Approximately(_baseVisualLocalScale.z, 0f) ? 1f : _baseVisualLocalScale.z;

            float bobSpeed = Mathf.Lerp(0.6f, 1.4f, Mathf.Clamp01(normalizedSpeed));
            _bobTimer += Time.deltaTime * walkBobFrequency * bobSpeed;

            float bobOffset = Mathf.Sin(_bobTimer) * walkBobAmplitude * normalizedSpeed;
            Vector3 targetLocalPosition = _baseVisualLocalPosition + new Vector3(0f, bobOffset, 0f);
            enemyVisual.localPosition = Vector3.Lerp(enemyVisual.localPosition, targetLocalPosition, 0.2f);

            float squashAmount = Mathf.Sin(_bobTimer) * walkSquashAmount * normalizedSpeed;

            Vector3 targetScale = new(
                absBaseScaleX * (_isFacingLeft ? -1f : 1f) * (1f - squashAmount),
                baseScaleY * (1f + squashAmount),
                baseScaleZ
            );

            enemyVisual.localScale = Vector3.Lerp(enemyVisual.localScale, targetScale, 0.25f);
        }
        #endregion Animations

        #region Handlers
        private void HandleFiring()
        {
            if (!autoShooter) return;

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

        private void HandleDeath()
        {
            if (autoShooter)
            {
                autoShooter.SetFireHeld(false);
            }
        }
        #endregion Handlers

        private void EnsurePlayerReference()
        {
            if (_player) return;

            var playerObject = GameObject.FindWithTag("Player");
            if (playerObject)
            {
                _player = playerObject.transform;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!_stats)
            {
                _stats = GetComponent<EnemyStats>();
            }

            float shootDistance = _stats ? _stats.ShootingDistance : 8f;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, shootDistance);
        }
    }
}
