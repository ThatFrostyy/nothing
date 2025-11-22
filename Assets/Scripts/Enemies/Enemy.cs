using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace FF
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EnemyStats))]
    [RequireComponent(typeof(Health))]
    public class Enemy : MonoBehaviour, IPoolable
    {
        public static event Action<Enemy> OnAnyEnemyKilled;

        [Header("References")]
        [SerializeField] private Weapon startingWeapon;
        [SerializeField] private WeaponManager weaponManager;
        [SerializeField] private AutoShooter autoShooter;

        [Header("Visuals")]
        [SerializeField] private Transform gunPivot;
        [SerializeField] private Vector3 gunOffsetRight = new Vector3(0.35f, -0.1f, 0f);
        [SerializeField] private Vector3 gunOffsetLeft = new Vector3(-0.35f, -0.1f, 0f);
        [SerializeField] private Transform enemyVisual;
        [SerializeField] private float walkBobFrequency = 6f;
        [SerializeField] private float walkBobAmplitude = 0.12f;
        [SerializeField] private float walkSquashAmount = 0.08f;
        [SerializeField] private float idleSwayFrequency = 1.5f;
        [SerializeField] private float idleSwayAmplitude = 3f;

        [Header("Movement Variation")]
        [SerializeField, Range(0f, 1f)] private float moveWhileShootingChance = 0.4f;
        [SerializeField, Range(0f, 1f)] private float moveWhileShootingSpeedMultiplier = 0.55f;
        [SerializeField, Min(0f)] private float weaveFrequency = 0.9f;
        [SerializeField, Range(0f, 1f)] private float weaveStrength = 0.35f;

        [Header("Helmets")]
        [SerializeField] private Transform helmetAnchor;
        [SerializeField] private GameObject[] helmetPrefabs;
        [SerializeField, Range(0f, 1f)] private float chanceForNoHelmet = 0.25f;

        [Header("Audio & FX")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] hitSound;
        [SerializeField] private AudioClip[] deathSound;
        [SerializeField] private GameObject deathFX;

        [Header("Behaviour Flags")]
        [SerializeField] private bool isDog;
        [SerializeField] private bool isBoss;
        [Space]
        [Header("Dog Behaviour")]
        [SerializeField, Min(0f)] private float dogAttackRange = 1.25f;
        [SerializeField, Min(0f)] private float dogAttackCooldown = 1.1f;
        [SerializeField, Min(0)] private int dogAttackDamage = 8;
        [SerializeField, Min(0f)] private float dogLeapHeight = 0.4f;
        [SerializeField, Min(0f)] private float dogLeapDuration = 0.3f;
        [SerializeField] private AudioClip dogAttackSound;

        [Header("Rewards")]
        [SerializeField] private XPOrb xpOrbPrefab;
        [SerializeField, Min(0)] private int xpOrbValue = 1;
        [SerializeField, Min(1)] private int xpOrbCount = 1;
        [SerializeField, Range(0f, 2f)] private float xpOrbSpreadRadius = 0.35f;

        private int _baseXpOrbValue;

        // NavMesh / movement
        [Header("NavMesh")]
        [SerializeField, Min(0.01f)] private float knockbackRecoveryTime = 0.25f;

        private NavMeshAgent _agent;
        private EnemyStats _stats;
        private Health _health;
        private Transform _player;
        private Health _playerHealth;

        private Vector2 _desiredVelocity;
        private Vector2 _currentVelocity;

        private IEnemyMovement _movementBehaviour;
        private IEnemyAttack _attackBehaviour;

        private Vector3 _baseVisualLocalPosition;
        private Vector3 _baseVisualLocalScale = Vector3.one;
        private float _bobTimer;
        private bool _isFacingLeft;

        private AudioSource _audioSource;
        private int lastIndex = -1;
        private Vector3 _visualPositionVelocity;
        private Vector3 _visualScaleVelocity;
        private float _tiltVelocity;
        private float _bobStrength;
        private float _facingBlend = 1f;
        private float _facingVelocity;
        private float _dogAttackCooldownTimer;
        private Coroutine _dogJumpRoutine;
        private Vector3 _dogAttackOffset = Vector3.zero;
        private GameObject _helmetInstance;
        private float _weaveOffset;
        private float _moveWhileShootingTimer;
        private bool _shouldMoveWhileShooting;
        private bool wasInShootZone = false;

        private float knockbackTimer = 0f;
        private Vector2 knockbackVelocity;

        private Vector2 _lastAimDirection = Vector2.right;

        private static readonly List<Enemy> ActiveEnemies = new List<Enemy>();

        private const float FacingDeadZone = 0.05f;

        public bool IsBoss => isBoss;

        public void Initialize(Transform player)
        {
            _player = player;
            CachePlayerHealth();
        }

        public void SetIsBoss(bool value)
        {
            isBoss = value;
        }

        public void ApplyWaveModifiers(EnemyWaveModifiers modifiers)
        {
            if (_health != null && modifiers.HealthMultiplier > 0f)
            {
                _health.ScaleMaxHP(modifiers.HealthMultiplier, false);
            }

            if (_stats != null)
            {
                _stats.ApplyWaveMultipliers(
                    modifiers.MoveSpeedMultiplier,
                    modifiers.FireRateMultiplier,
                    modifiers.DamageMultiplier,
                    1f);
            }

            if (modifiers.XpValueMultiplier > 0f)
            {
                int scaledValue = Mathf.Max(1, Mathf.RoundToInt(_baseXpOrbValue * modifiers.XpValueMultiplier));
                xpOrbValue = scaledValue;
            }
        }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (!_agent)
            {
                _agent = gameObject.AddComponent<NavMeshAgent>();
            }

            // 2D setup
            _agent.updateRotation = false;
            _agent.updateUpAxis = false;
            _agent.autoBraking = false;

            _stats = GetComponent<EnemyStats>();
            _health = GetComponent<Health>();

            _baseXpOrbValue = Mathf.Max(1, xpOrbValue);

            _audioSource = audioSource ? audioSource : GetComponent<AudioSource>();
            if (!_audioSource)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }

            if (_audioSource)
            {
                _audioSource.playOnAwake = false;
                _audioSource.loop = false;
                _audioSource.spatialBlend = 0f;
            }

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

            if (isDog && autoShooter)
            {
                autoShooter.SetFireHeld(false);
                autoShooter.enabled = false;
                autoShooter = null;
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
                _facingBlend = Mathf.Approximately(enemyVisual.localScale.x, 0f)
                    ? 1f
                    : Mathf.Sign(enemyVisual.localScale.x);
            }

            SpawnHelmet();

            _weaveOffset = UnityEngine.Random.Range(0f, 32f);
            _moveWhileShootingTimer = UnityEngine.Random.Range(0.75f, 1.5f);
            _shouldMoveWhileShooting = UnityEngine.Random.value < moveWhileShootingChance;

            if (autoShooter)
            {
                autoShooter.SetStatsProvider(_stats);
                autoShooter.SetFireHeld(false);
                autoShooter.SetCameraShakeEnabled(false);
            }

            _movementBehaviour = GetComponent<IEnemyMovement>();
            _attackBehaviour = GetComponent<IEnemyAttack>();

            EnsurePlayerReference();
            TryWarpAgent();
        }

        private void SpawnHelmet()
        {
            if (_helmetInstance)
            {
                Destroy(_helmetInstance);
                _helmetInstance = null;
            }

            Transform anchor = helmetAnchor ? helmetAnchor : enemyVisual;
            if (!anchor && !enemyVisual)
            {
                return;
            }

            if (helmetPrefabs == null || helmetPrefabs.Length == 0)
            {
                return;
            }

            if (chanceForNoHelmet > 0f && UnityEngine.Random.value < Mathf.Clamp01(chanceForNoHelmet))
            {
                return;
            }

            GameObject prefab = helmetPrefabs[UnityEngine.Random.Range(0, helmetPrefabs.Length)];
            if (!prefab)
            {
                return;
            }

            Transform parent = enemyVisual ? enemyVisual : anchor;
            Vector3 localPosition = anchor ? parent.InverseTransformPoint(anchor.position) : Vector3.zero;
            Quaternion prefabLocalRotation = prefab.transform.localRotation;
            Quaternion localRotation = anchor
                ? Quaternion.Inverse(parent.rotation) * anchor.rotation * prefabLocalRotation
                : prefabLocalRotation;
            Vector3 localScale = anchor ? anchor.localScale : Vector3.one;

            _helmetInstance = Instantiate(prefab, parent);
            _helmetInstance.transform.localPosition = localPosition;
            _helmetInstance.transform.localRotation = localRotation;
            _helmetInstance.transform.localScale = localScale;
        }

        private void Start()
        {
            if (!isDog && weaponManager && startingWeapon)
            {
                weaponManager.Equip(startingWeapon);
            }
        }

        private void Update()
        {
            EnsurePlayerReference();
            AimAtPlayer();

            float deltaTime = Time.deltaTime;
            if (_attackBehaviour != null)
            {
                _attackBehaviour.TickAttack(this, _player, _stats, autoShooter, deltaTime);
            }
            else if (isDog)
            {
                UpdateDogBehaviour(deltaTime);
            }
            else
            {
                HandleFiring();
            }
        }

        private void FixedUpdate()
        {
            if (knockbackTimer > 0f)
            {
                knockbackTimer -= Time.fixedDeltaTime;
                ApplyKnockbackDisplacement(Time.fixedDeltaTime);
                return;
            }

            ResumeAgentIfNeeded();
            UpdateMovement();
            UpdateBodyTilt();
        }

        private void OnEnable()
        {
            if (!ActiveEnemies.Contains(this))
            {
                ActiveEnemies.Add(this);
            }

            if (_health != null)
            {
                _health.OnDeath += HandleDeath;
                _health.OnDamaged += HandleDamaged;
            }

            if (_dogJumpRoutine != null)
            {
                StopCoroutine(_dogJumpRoutine);
                _dogJumpRoutine = null;
            }

            _dogAttackCooldownTimer = 0f;
            _dogAttackOffset = Vector3.zero;
        }

        private void OnDisable()
        {
            ActiveEnemies.Remove(this);

            if (_health != null)
            {
                _health.OnDeath -= HandleDeath;
                _health.OnDamaged -= HandleDamaged;
            }

            if (_dogJumpRoutine != null)
            {
                StopCoroutine(_dogJumpRoutine);
                _dogJumpRoutine = null;
            }

            _dogAttackOffset = Vector3.zero;
        }

        #region Movement

        private void UpdateMovement()
        {
            if (isDog && _movementBehaviour == null)
            {
                UpdateDogMovement();
                return;
            }

            float moveSpeed = _stats ? _stats.MoveSpeed : 3f;
            float retreatMultiplier = _stats ? _stats.RetreatSpeedMultiplier : 0.6f;

            Vector2 targetVelocity = _movementBehaviour != null
                ? _movementBehaviour.GetDesiredVelocity(this, _player, _stats, _agent, Time.fixedDeltaTime)
                : CalculateDefaultDesiredVelocity(moveSpeed, retreatMultiplier);

            ApplyDesiredVelocity(targetVelocity, moveSpeed);
        }

        private void UpdateDogMovement()
        {
            Vector2 targetVelocity = Vector2.zero;
            float moveSpeed = _stats ? _stats.MoveSpeed : 3f;

            if (_player)
            {
                Vector2 toPlayer = _player.position - transform.position;
                float distance = toPlayer.magnitude;
                if (distance > 0.001f)
                {
                    Vector2 direction = toPlayer / distance;
                    targetVelocity = direction * moveSpeed;
                    _isFacingLeft = direction.x < 0f;
                }
            }

            _desiredVelocity = Vector2.ClampMagnitude(targetVelocity, moveSpeed);
            MoveAgent(_desiredVelocity, moveSpeed);
        }

        private Vector2 CalculateDefaultDesiredVelocity(float moveSpeed, float retreatMultiplier)
        {
            Vector2 targetVelocity = Vector2.zero;

            if (_player)
            {
                Vector2 toPlayer = _player.position - transform.position;
                float distance = toPlayer.magnitude;
                Vector2 direction = distance > 0.001f ? toPlayer / distance : Vector2.zero;
                Vector2 strafeDirection = direction.sqrMagnitude > 0f
                    ? new Vector2(-direction.y, direction.x)
                    : Vector2.zero;
                float weaveOffset = Mathf.Sin((Time.time + _weaveOffset) * weaveFrequency) * weaveStrength;

                float buffer = _stats ? _stats.DistanceBuffer : 1f;
                float shootDistance = _stats ? _stats.ShootingDistance : 8f;

                bool inShootZone = Mathf.Abs(distance - shootDistance) <= buffer;

                if (inShootZone && !wasInShootZone)
                {
                    _shouldMoveWhileShooting = UnityEngine.Random.value < moveWhileShootingChance;
                }

                wasInShootZone = inShootZone;

                if (distance > shootDistance + buffer)
                {
                    targetVelocity = direction * moveSpeed;
                }
                else if (distance < shootDistance - buffer)
                {
                    targetVelocity = moveSpeed * retreatMultiplier * -direction;
                }
                else
                {
                    UpdateMoveWhileShootingTimer();

                    if (_shouldMoveWhileShooting && strafeDirection.sqrMagnitude > 0f)
                    {
                        targetVelocity = strafeDirection * (moveSpeed * moveWhileShootingSpeedMultiplier * weaveOffset);
                    }
                }

                if (strafeDirection.sqrMagnitude > 0f && Mathf.Abs(weaveOffset) > 0.001f)
                {
                    targetVelocity += strafeDirection * (moveSpeed * weaveOffset * 0.5f);
                }
            }

            return targetVelocity;
        }

        private void UpdateMoveWhileShootingTimer()
        {
            _moveWhileShootingTimer -= Time.fixedDeltaTime;
            if (_moveWhileShootingTimer <= 0f)
            {
                _shouldMoveWhileShooting = UnityEngine.Random.value < moveWhileShootingChance;
                _moveWhileShootingTimer = UnityEngine.Random.Range(1.25f, 2.25f);
            }
        }

        private void ApplyDesiredVelocity(Vector2 targetVelocity, float moveSpeed)
        {
            _desiredVelocity = Vector2.ClampMagnitude(targetVelocity, moveSpeed);
            MoveAgent(_desiredVelocity, moveSpeed);
        }

        private void MoveAgent(Vector2 targetVelocity, float moveSpeed)
        {
            _currentVelocity = targetVelocity;

            if (_agent == null || !_agent.enabled)
            {
                transform.position += (Vector3)(targetVelocity * Time.fixedDeltaTime);
                return;
            }

            _agent.speed = moveSpeed;
            _agent.acceleration = Mathf.Max(moveSpeed / Mathf.Max(Time.fixedDeltaTime, Mathf.Epsilon), 8f);

            if (!_agent.isOnNavMesh)
            {
                TryWarpAgent();
            }

            Vector3 displacement = (Vector3)(targetVelocity * Time.fixedDeltaTime);

            if (_agent.isOnNavMesh)
            {
                _agent.Move(displacement);
                _agent.velocity = targetVelocity;
            }
            else
            {
                transform.position += displacement;
            }
        }

        private void TryWarpAgent()
        {
            if (_agent == null || !_agent.enabled)
                return;

            if (_agent.isOnNavMesh)
                return;

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            {
                _agent.Warp(hit.position);
            }
        }

        private void ApplyKnockbackDisplacement(float deltaTime)
        {
            Vector3 displacement = (Vector3)(knockbackVelocity * deltaTime);
            transform.position += displacement;
            _currentVelocity = knockbackVelocity;

            if (_agent && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.Warp(transform.position);
                _agent.velocity = knockbackVelocity;
            }
        }

        private void ResumeAgentIfNeeded()
        {
            if (_agent && _agent.enabled && knockbackTimer <= 0f && _agent.isOnNavMesh && _agent.isStopped)
            {
                _agent.isStopped = false;
            }
        }

        private void UpdateDogBehaviour(float deltaTime)
        {
            if (_dogAttackCooldownTimer > 0f)
            {
                _dogAttackCooldownTimer = Mathf.Max(0f, _dogAttackCooldownTimer - deltaTime);
            }

            if (!_player)
            {
                return;
            }

            if (dogAttackRange <= 0f)
            {
                return;
            }

            float distance = Vector2.Distance(transform.position, _player.position);
            if (distance <= dogAttackRange && _dogAttackCooldownTimer <= 0f)
            {
                PerformDogAttack();
            }
        }

        private void PerformDogAttack()
        {
            _dogAttackCooldownTimer = Mathf.Max(0f, dogAttackCooldown);

            int damage = GetDogAttackDamage();
            if (damage > 0 && _playerHealth)
            {
                _playerHealth.Damage(damage);
            }

            PlayDogAttackSound();
            StartDogJumpAnimation();
        }

        private int GetDogAttackDamage()
        {
            float baseDamage = Mathf.Max(0, dogAttackDamage);
            float multiplier = _stats ? _stats.GetDamageMultiplier() : 1f;
            int scaled = Mathf.RoundToInt(baseDamage * Mathf.Max(0f, multiplier));
            return Mathf.Max(0, scaled);
        }

        private IEnumerator DogJumpRoutine()
        {
            float duration = Mathf.Max(0.05f, dogLeapDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float normalized = duration > 0f ? elapsed / duration : 1f;
                float height = Mathf.Sin(normalized * Mathf.PI) * dogLeapHeight;
                _dogAttackOffset = new Vector3(0f, height, 0f);
                elapsed += Time.deltaTime;
                yield return null;
            }

            _dogAttackOffset = Vector3.zero;
            _dogJumpRoutine = null;
        }

        private void AimAtPlayer()
        {
            if (!gunPivot || !_player) return;

            Vector2 dir = _player.position - gunPivot.position;
            if (dir.sqrMagnitude < 0.001f) return;

            Vector2 aimDirection = dir.normalized;
            if (Mathf.Abs(aimDirection.x) <= FacingDeadZone * 0.5f)
            {
                float preservedSign = Mathf.Approximately(_lastAimDirection.x, 0f)
                    ? Mathf.Sign(Mathf.Approximately(aimDirection.y, 0f) ? 1f : aimDirection.y)
                    : Mathf.Sign(_lastAimDirection.x);
                aimDirection.x = preservedSign * FacingDeadZone;
            }

            _lastAimDirection = aimDirection;

            float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;

            gunPivot.rotation = Quaternion.Euler(0f, 0f, angle);

            bool facingLeft = _isFacingLeft;
            if (Mathf.Abs(aimDirection.x) > FacingDeadZone)
            {
                facingLeft = aimDirection.x < 0f;
            }

            _isFacingLeft = facingLeft;

            weaponManager.transform.localPosition = facingLeft ? gunOffsetLeft : gunOffsetRight;

            Vector3 scale = gunPivot.localScale;
            scale.y = facingLeft ? -1f : 1f;
            gunPivot.localScale = scale;
        }

        public void ApplyKnockback(Vector2 force, float duration = 0.25f)
        {
            knockbackTimer = duration;
            knockbackVelocity = force;

            if (_agent && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
            }
        }

        #endregion Movement

        #region Animations

        private void UpdateBodyTilt()
        {
            if (!enemyVisual) return;

            float bodyTiltDegrees = _stats ? _stats.BodyTiltDegrees : 12f;
            float speed = _currentVelocity.magnitude;
            float maxSpeed = Mathf.Max(_stats ? _stats.MoveSpeed : 1f, Mathf.Epsilon);
            float normalizedSpeed = speed / maxSpeed;

            float targetTilt = speed > 0.1f ? -bodyTiltDegrees * normalizedSpeed : bodyTiltDegrees * 0.3f;

            if (_desiredVelocity.sqrMagnitude > 0.01f)
            {
                float side = Mathf.Clamp(_desiredVelocity.normalized.x, -1f, 1f);
                targetTilt += side * (bodyTiltDegrees * 0.5f);
            }

            float idleBlend = 1f - Mathf.Clamp01(normalizedSpeed * 3f);
            if (idleBlend > 0f)
            {
                targetTilt += Mathf.Sin(Time.time * idleSwayFrequency) * idleSwayAmplitude * idleBlend;
            }

            float newZ = Mathf.SmoothDampAngle(
                enemyVisual.localEulerAngles.z,
                targetTilt,
                ref _tiltVelocity,
                0.07f
            );
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

            float targetStrength = Mathf.Clamp01(normalizedSpeed);
            _bobStrength = Mathf.Lerp(_bobStrength, targetStrength, Time.deltaTime * 10f);

            float bobSpeed = Mathf.Lerp(0.6f, 1.4f, _bobStrength);
            _bobTimer += Time.deltaTime * walkBobFrequency * bobSpeed;

            float bobOffset = Mathf.Sin(_bobTimer) * walkBobAmplitude * _bobStrength;
            Vector3 targetLocalPosition = _baseVisualLocalPosition + _dogAttackOffset + new Vector3(0f, bobOffset, 0f);
            enemyVisual.localPosition = Vector3.SmoothDamp(
                enemyVisual.localPosition,
                targetLocalPosition,
                ref _visualPositionVelocity,
                0.045f,
                Mathf.Infinity,
                Time.deltaTime
            );

            float squashAmount = Mathf.Sin(_bobTimer) * walkSquashAmount * _bobStrength;

            float desiredFacing = _isFacingLeft ? -1f : 1f;
            _facingBlend = Mathf.SmoothDamp(_facingBlend, desiredFacing, ref _facingVelocity, 0.1f, Mathf.Infinity, Time.deltaTime);

            Vector3 targetScale = new(
                absBaseScaleX * Mathf.Clamp(_facingBlend, -1f, 1f) * (1f - squashAmount),
                baseScaleY * (1f + squashAmount * 0.75f),
                baseScaleZ
            );

            enemyVisual.localScale = Vector3.SmoothDamp(
                enemyVisual.localScale,
                targetScale,
                ref _visualScaleVelocity,
                0.055f,
                Mathf.Infinity,
                Time.deltaTime
            );
        }

        private void StartDogJumpAnimation()
        {
            if (!enemyVisual)
            {
                return;
            }

            if (_dogJumpRoutine != null)
            {
                StopCoroutine(_dogJumpRoutine);
            }

            _dogJumpRoutine = StartCoroutine(DogJumpRoutine());
        }

        #endregion Animations

        #region Handlers

        private void HandleFiring()
        {
            if (isDog)
            {
                if (autoShooter)
                {
                    autoShooter.SetFireHeld(false);
                }
                return;
            }

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

        private void HandleDamaged(int damage)
        {
            if (damage <= 0)
            {
                return;
            }

            PlayHitSound();
        }

        private void HandleDeath()
        {
            if (autoShooter)
            {
                autoShooter.SetFireHeld(false);
            }

            SpawnXPOrbs();

            _dogAttackOffset = Vector3.zero;

            var handler = OnAnyEnemyKilled;
            if (handler != null)
            {
                handler(this);
            }

            PlayDeathSound();
            SpawnDeathFx();
        }

        #endregion Handlers

        #region Pooling

        public void OnTakenFromPool()
        {
            if (_health != null)
            {
                _health.ResetToBase();
            }

            if (_stats != null)
            {
                _stats.ResetRuntimeValues();
            }

            xpOrbValue = _baseXpOrbValue;
            isBoss = false;
            knockbackTimer = 0f;
            knockbackVelocity = Vector2.zero;
            _bobTimer = 0f;
            _facingBlend = 1f;
            _facingVelocity = 0f;
            _visualPositionVelocity = Vector3.zero;
            _visualScaleVelocity = Vector3.zero;
            _tiltVelocity = 0f;
            _dogAttackCooldownTimer = 0f;
            _dogAttackOffset = Vector3.zero;
            _moveWhileShootingTimer = UnityEngine.Random.Range(0.75f, 1.5f);
            _shouldMoveWhileShooting = UnityEngine.Random.value < moveWhileShootingChance;
            _weaveOffset = UnityEngine.Random.Range(0f, 32f);
            wasInShootZone = false;
            _desiredVelocity = Vector2.zero;
            _currentVelocity = Vector2.zero;

            if (_agent && _agent.enabled)
            {
                TryWarpAgent();
                if (_agent.isOnNavMesh)
                {
                    _agent.velocity = Vector3.zero;
                    if (_agent.isStopped)
                        _agent.isStopped = false;
                }
            }

            if (autoShooter)
            {
                autoShooter.SetFireHeld(false);
            }
        }

        public void OnReturnedToPool()
        {
            if (_dogJumpRoutine != null)
            {
                StopCoroutine(_dogJumpRoutine);
                _dogJumpRoutine = null;
            }

            _dogAttackOffset = Vector3.zero;
            _dogAttackCooldownTimer = 0f;
            _desiredVelocity = Vector2.zero;
            _currentVelocity = Vector2.zero;
            knockbackTimer = 0f;
            knockbackVelocity = Vector2.zero;
            wasInShootZone = false;

            if (_agent && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.velocity = Vector3.zero;
                if (_agent.isStopped)
                    _agent.isStopped = false;
            }

            if (autoShooter)
            {
                autoShooter.SetFireHeld(false);
            }
        }

        #endregion Pooling

        private void EnsurePlayerReference()
        {
            if (_player)
            {
                if (!_playerHealth)
                {
                    CachePlayerHealth();
                }
                return;
            }

            var playerObject = GameObject.FindWithTag("Player");
            if (playerObject)
            {
                _player = playerObject.transform;
                CachePlayerHealth();
            }
        }

        private void CachePlayerHealth()
        {
            _playerHealth = null;

            if (!_player)
            {
                return;
            }

            if (_player.TryGetComponent(out Health directHealth))
            {
                _playerHealth = directHealth;
                return;
            }

            _playerHealth = _player.GetComponentInParent<Health>();
            if (!_playerHealth)
            {
                _playerHealth = _player.GetComponentInChildren<Health>();
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

        void OnValidate()
        {
            dogAttackRange = Mathf.Max(0f, dogAttackRange);
            dogAttackCooldown = Mathf.Max(0f, dogAttackCooldown);
            dogAttackDamage = Mathf.Max(0, dogAttackDamage);
            dogLeapHeight = Mathf.Max(0f, dogLeapHeight);
            dogLeapDuration = Mathf.Max(0f, dogLeapDuration);
        }

        #region Audio

        private AudioClip GetRandomClip(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;

            int index;
            do { index = UnityEngine.Random.Range(0, clips.Length); }
            while (index == lastIndex && clips.Length > 1);
            lastIndex = index;

            return clips[index];
        }

        private void PlayHitSound()
        {
            if (!_audioSource) return;

            AudioClip clip = GetRandomClip(hitSound);
            if (clip)
            {
                _audioSource.PlayOneShot(clip);
            }
        }

        private void PlayDeathSound()
        {
            AudioClip clip = GetRandomClip(deathSound);
            if (!clip) return;

            float volume = _audioSource ? _audioSource.volume : 1f;
            float pitch = _audioSource ? _audioSource.pitch : 1f;
            float spatialBlend = _audioSource ? _audioSource.spatialBlend : 0f;
            var mixerGroup = _audioSource ? _audioSource.outputAudioMixerGroup : null;

            AudioPlaybackPool.PlayOneShot(clip, transform.position, mixerGroup, spatialBlend, volume, pitch);
        }

        private void PlayDogAttackSound()
        {
            if (!dogAttackSound)
            {
                return;
            }

            if (_audioSource)
            {
                _audioSource.PlayOneShot(dogAttackSound);
                return;
            }

            AudioSource.PlayClipAtPoint(dogAttackSound, transform.position);
        }

        #endregion Audio

        private void SpawnDeathFx()
        {
            if (!deathFX)
            {
                return;
            }

            GameObject spawned = PoolManager.Get(deathFX, transform.position, Quaternion.identity);
            if (spawned && !spawned.TryGetComponent<PooledParticleSystem>(out var pooled))
            {
                pooled = spawned.AddComponent<PooledParticleSystem>();
                pooled.OnTakenFromPool();
            }
        }

        private void SpawnXPOrbs()
        {
            if (!xpOrbPrefab)
            {
                return;
            }

            if (xpOrbValue <= 0 || xpOrbCount <= 0)
            {
                return;
            }

            GameObjectPool orbPool = PoolManager.GetPool(xpOrbPrefab.gameObject, xpOrbCount);
            for (int i = 0; i < xpOrbCount; i++)
            {
                Vector3 spawnPosition = transform.position;
                if (xpOrbSpreadRadius > 0f)
                {
                    Vector2 offset = UnityEngine.Random.insideUnitCircle * xpOrbSpreadRadius;
                    spawnPosition += (Vector3)offset;
                }

                XPOrb orb = orbPool.GetComponent<XPOrb>(spawnPosition, Quaternion.identity);
                if (orb)
                {
                    orb.SetValue(xpOrbValue);
                }
            }
        }
    }
}