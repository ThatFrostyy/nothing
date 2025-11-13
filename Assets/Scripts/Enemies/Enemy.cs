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

        [Header("Avoidance")]
        [SerializeField] private LayerMask avoidanceLayers = ~0;
        [SerializeField, Range(4, 64)] private int maxAvoidanceChecks = 16;

        private Rigidbody2D _rigidbody;
        private EnemyStats _stats;
        private Health _health;
        private Transform _player;
        private Vector2 _desiredVelocity;
        private Vector2 _smoothedSeparation;
        private Vector3 _baseVisualLocalPosition;
        private Vector3 _baseVisualLocalScale = Vector3.one;
        private float _bobTimer;
        private bool _isFacingLeft;
        private Collider2D[] _avoidanceResults;
        private ContactFilter2D _avoidanceFilter;

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

            int bufferSize = Mathf.Clamp(maxAvoidanceChecks, 4, 64);
            _avoidanceResults = new Collider2D[bufferSize];
            _avoidanceFilter = new ContactFilter2D
            {
                useLayerMask = true,
                useTriggers = true
            };
            _avoidanceFilter.SetLayerMask(avoidanceLayers);

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
            float moveSpeed = _stats ? _stats.MoveSpeed : 3f;
            float retreatMultiplier = _stats ? _stats.RetreatSpeedMultiplier : 0.6f;

            if (_player)
            {
                Vector2 toPlayer = _player.position - transform.position;
                float distance = toPlayer.magnitude;
                Vector2 direction = distance > 0.001f ? toPlayer / distance : Vector2.zero;

                float buffer = _stats ? _stats.DistanceBuffer : 1f;
                float shootDistance = _stats ? _stats.ShootingDistance : 8f;

                if (distance > shootDistance + buffer)
                {
                    targetVelocity = direction * moveSpeed;
                }
                else if (distance < shootDistance - buffer)
                {
                    targetVelocity = -direction * moveSpeed * retreatMultiplier;
                }
            }

            if (_stats)
            {
                Vector2 separationForce = CalculateSeparationForce(_stats.AvoidanceRadius, _stats.AvoidancePush);
                float responsiveness = _stats.AvoidanceResponsiveness;
                _smoothedSeparation = responsiveness > 0f
                    ? Vector2.Lerp(_smoothedSeparation, separationForce, responsiveness)
                    : separationForce;

                targetVelocity += _smoothedSeparation * _stats.AvoidanceWeight;
            }
            else
            {
                _smoothedSeparation = Vector2.zero;
            }

            targetVelocity = Vector2.ClampMagnitude(targetVelocity, moveSpeed);
            _desiredVelocity = targetVelocity;

            float acceleration = _stats ? _stats.Acceleration : 0.2f;
            _rigidbody.linearVelocity = Vector2.Lerp(_rigidbody.linearVelocity, targetVelocity, acceleration);
        }

        private Vector2 CalculateSeparationForce(float radius, float pushStrength)
        {
            if (radius <= 0f || pushStrength <= 0f || _avoidanceResults == null)
            {
                return Vector2.zero;
            }

            Vector2 origin = _rigidbody ? _rigidbody.position : (Vector2)transform.position;
            int hitCount = Physics2D.OverlapCircle(origin, radius, _avoidanceFilter, _avoidanceResults);

            if (hitCount <= 0)
            {
                return Vector2.zero;
            }

            Vector2 separation = Vector2.zero;
            int contributions = 0;

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D neighbor = _avoidanceResults[i];
                if (!neighbor)
                {
                    continue;
                }

                if (neighbor.attachedRigidbody == _rigidbody)
                {
                    continue;
                }

                Vector2 neighborPoint = neighbor.ClosestPoint(origin);
                Vector2 offset = origin - neighborPoint;
                float sqrMagnitude = offset.sqrMagnitude;

                if (sqrMagnitude < 0.0001f)
                {
                    offset = origin - (Vector2)neighbor.transform.position;
                    sqrMagnitude = offset.sqrMagnitude;
                    if (sqrMagnitude < 0.0001f)
                    {
                        continue;
                    }
                }

                float distance = Mathf.Sqrt(sqrMagnitude);
                float weight = Mathf.InverseLerp(radius, 0f, distance);
                Vector2 direction = offset / distance;

                separation += direction * weight;
                contributions++;
            }

            if (contributions == 0)
            {
                return Vector2.zero;
            }

            separation /= contributions;
            separation *= pushStrength;

            return Vector2.ClampMagnitude(separation, pushStrength);
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
