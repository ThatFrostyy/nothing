using System;
using System.Collections;
using UnityEngine;

namespace FF
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(EnemyStats))]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(DamageNumberListener))]
    public class Enemy : MonoBehaviour, IPoolable
    {
        public static event Action<Enemy> OnAnyEnemyKilled;

        [Header("Debug")]
        [SerializeField] private bool simpleFollowTest = false;

        [Header("References")]
        [SerializeField] private Weapon startingWeapon;
        [SerializeField] private WeaponManager weaponManager;
        [SerializeField] private AutoShooter autoShooter;
        [SerializeField] private EnemyPresentation presentation;

        [Header("Movement Variation")]
        [SerializeField, Range(0f, 1f)] private float moveWhileShootingChance = 0.4f;
        [SerializeField, Range(0f, 1f)] private float moveWhileShootingSpeedMultiplier = 0.55f;
        [SerializeField, Min(0f)] private float weaveFrequency = 0.9f;
        [SerializeField, Range(0f, 1f)] private float weaveStrength = 0.35f;

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

        [Header("Avoidance")]
        [SerializeField] private LayerMask avoidanceLayers = ~0;
        [SerializeField, Range(4, 128)] private int maxAvoidanceChecks = 32;

        private Rigidbody2D _rigidbody;
        private EnemyStats _stats;
        private Health _health;
        private Transform _player;
        private Health _playerHealth;
        private Vector2 _desiredVelocity;
        private IEnemyMovement _movementBehaviour;
        private IEnemyAttack _attackBehaviour;
        private Vector2 _smoothedSeparation;
        private Collider2D[] _avoidanceResults;
        private ContactFilter2D _avoidanceFilter;
        private AudioSource _audioSource;
        private const int AvoidanceBufferCeiling = 256;
        private int lastIndex = -1;
        private float _dogAttackCooldownTimer;
        private float _weaveOffset;
        private float _moveWhileShootingTimer;
        private bool _shouldMoveWhileShooting;
        private bool wasInShootZone = false;
        private float knockbackTimer = 0f;
        private Vector2 knockbackVelocity;

        private const float FacingDeadZone = 0.05f;

        public bool IsBoss => isBoss;
        public AutoShooter AutoShooter => autoShooter;
        public AudioClip[] DeathSounds => deathSound;
        public GameObject DeathFx => deathFX;
        public XPOrb XpOrbPrefab => xpOrbPrefab;
        public int XpOrbValue => xpOrbValue;
        public int XpOrbCount => xpOrbCount;
        public float XpOrbSpreadRadius => xpOrbSpreadRadius;
        public AudioSource AudioSource => _audioSource;

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
            _rigidbody = GetComponent<Rigidbody2D>();
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

            if (!presentation)
            {
                presentation = GetComponent<EnemyPresentation>();
            }

            presentation?.Initialize(this, _rigidbody, _stats, weaponManager);

            _weaveOffset = UnityEngine.Random.Range(0f, 32f);
            _moveWhileShootingTimer = UnityEngine.Random.Range(0.75f, 1.5f);
            _shouldMoveWhileShooting = UnityEngine.Random.value < moveWhileShootingChance;

            if (autoShooter)
            {
                autoShooter.SetStatsProvider(_stats);
                autoShooter.SetFireHeld(false);
                autoShooter.SetCameraShakeEnabled(false);
            }

            int bufferSize = Mathf.Clamp(maxAvoidanceChecks, 4, AvoidanceBufferCeiling);
            _avoidanceResults = new Collider2D[bufferSize];
            _avoidanceFilter = new ContactFilter2D
            {
                useLayerMask = true,
                useTriggers = true
            };
            _avoidanceFilter.SetLayerMask(avoidanceLayers);

            _movementBehaviour = GetComponent<IEnemyMovement>();
            _attackBehaviour = GetComponent<IEnemyAttack>();

            EnsurePlayerReference();
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
            presentation?.UpdateAim(_player);
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
            if (simpleFollowTest)
            {
                SimpleFollowMovement();
                return; 
            }

            if (knockbackTimer > 0f)
            {
                knockbackTimer -= Time.fixedDeltaTime;
                _rigidbody.linearVelocity = knockbackVelocity;
                return;
            }

            UpdateMovement();
            presentation?.UpdateVisuals(_desiredVelocity, _rigidbody.linearVelocity);
        }

        private void SimpleFollowMovement()
        {
            if (!_player)
                return;

            float moveSpeed = 3f; 
            Vector2 direction = ((Vector2)_player.position - _rigidbody.position).normalized;

            _rigidbody.linearVelocity = direction * moveSpeed;
        }

        private void OnEnable()
        {
            if (_health != null)
            {
                _health.OnDamaged += HandleDamaged;
            }

            _dogAttackCooldownTimer = 0f;
            presentation?.HandleEnable();
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.OnDamaged -= HandleDamaged;
            }

            presentation?.HandleDisable();
        }

        public void RaiseKilled()
        {
            OnAnyEnemyKilled?.Invoke(this);
        }

        private void UpdateMovement()
        {
            if (isDog && _movementBehaviour == null)
            {
                UpdateDogMovement();
                return;
            }
            float moveSpeed = _stats ? _stats.MoveSpeed : 3f;
            float retreatMultiplier = _stats ? _stats.RetreatSpeedMultiplier : 0.6f;
            bool clampToStats = _movementBehaviour == null;
            Vector2 targetVelocity = _movementBehaviour != null
                ? _movementBehaviour.GetDesiredVelocity(this, _player, _stats, _rigidbody, Time.fixedDeltaTime)
                : CalculateDefaultDesiredVelocity(moveSpeed, retreatMultiplier);

            ApplyDesiredVelocity(targetVelocity, moveSpeed, clampToStats);
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
                }
            }

            targetVelocity = ApplySeparationAndClamp(targetVelocity, moveSpeed, true);
            _desiredVelocity = targetVelocity;

            float acceleration = _stats ? _stats.Acceleration : 0.2f;
            _rigidbody.linearVelocity = Vector2.Lerp(_rigidbody.linearVelocity, targetVelocity, acceleration);
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

        private void ApplyDesiredVelocity(Vector2 targetVelocity, float moveSpeed, bool clampToStats)
        {
            targetVelocity = ApplySeparationAndClamp(targetVelocity, moveSpeed, clampToStats);
            _desiredVelocity = targetVelocity;

            //float acceleration = _stats ? _stats.Acceleration : 0.2f;
            _rigidbody.linearVelocity = targetVelocity;

        }

        private Vector2 ApplySeparationAndClamp(Vector2 targetVelocity, float moveSpeed, bool clampToStats)
        {
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

            return clampToStats ? Vector2.ClampMagnitude(targetVelocity, moveSpeed) : targetVelocity;
        }

        private Vector2 CalculateSeparationForce(float radius, float pushStrength)
        {
            if (radius <= 0f || pushStrength <= 0f)
            {
                return Vector2.zero;
            }

            Vector2 origin = _rigidbody ? _rigidbody.position : (Vector2)transform.position;
            if (_avoidanceResults == null || _avoidanceResults.Length == 0)
            {
                int initialSize = Mathf.Clamp(maxAvoidanceChecks, 4, AvoidanceBufferCeiling);
                _avoidanceResults = new Collider2D[initialSize];
            }

            int hitCount = Physics2D.OverlapCircle(origin, radius, _avoidanceFilter, _avoidanceResults);
            int currentCapacity = _avoidanceResults.Length;

            while (hitCount >= currentCapacity && currentCapacity < AvoidanceBufferCeiling)
            {
                int newCapacity = Mathf.Min(currentCapacity * 2, AvoidanceBufferCeiling);
                System.Array.Resize(ref _avoidanceResults, newCapacity);
                currentCapacity = _avoidanceResults.Length;
                hitCount = Physics2D.OverlapCircle(origin, radius, _avoidanceFilter, _avoidanceResults);
            }

            if (hitCount <= 0)
            {
                return Vector2.zero;
            }

            Vector2 separation = Vector2.zero;
            float totalWeight = 0f;
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
                        int hash = neighbor.GetInstanceID() ^ GetInstanceID();
                        float angle = (hash & 1023) * Mathf.Deg2Rad;
                        offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.1f;
                        sqrMagnitude = offset.sqrMagnitude;
                        if (sqrMagnitude < 0.0001f)
                        {
                            continue;
                        }
                    }
                }

                float distance = Mathf.Sqrt(sqrMagnitude);
                float weight = Mathf.InverseLerp(radius, 0f, distance);
                Vector2 direction = offset / distance;

                separation += direction * weight;
                totalWeight += weight;
                contributions++;
            }

            if (contributions == 0 || totalWeight <= 0f)
            {
                return Vector2.zero;
            }

            if (separation.sqrMagnitude < 0.0001f)
            {
                return Vector2.zero;
            }

            Vector2 separationDirection = separation.normalized;
            float crowdingStrength = Mathf.Clamp(totalWeight, 0.25f, 1f);
            Vector2 force = crowdingStrength * pushStrength * separationDirection;

            return Vector2.ClampMagnitude(force, pushStrength);
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
            presentation?.TriggerDogJump(dogLeapHeight, dogLeapDuration);
        }

        private int GetDogAttackDamage()
        {
            float baseDamage = Mathf.Max(0, dogAttackDamage);
            float multiplier = _stats ? _stats.GetDamageMultiplier() : 1f;
            int scaled = Mathf.RoundToInt(baseDamage * Mathf.Max(0f, multiplier));
            return Mathf.Max(0, scaled);
        }

        public void ApplyKnockback(Vector2 force, float duration = 0.25f)
        {
            knockbackTimer = duration;
            knockbackVelocity = force;
        }

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
            _dogAttackCooldownTimer = 0f;
            _moveWhileShootingTimer = UnityEngine.Random.Range(0.75f, 1.5f);
            _shouldMoveWhileShooting = UnityEngine.Random.value < moveWhileShootingChance;
            _weaveOffset = UnityEngine.Random.Range(0f, 32f);
            wasInShootZone = false;
            _desiredVelocity = Vector2.zero;

            presentation?.ResetVisualState();

            if (_rigidbody)
            {
                _rigidbody.linearVelocity = Vector2.zero;
                _rigidbody.angularVelocity = 0f;
            }

            if (autoShooter)
            {
                autoShooter.SetFireHeld(false);
            }
        }

        public void OnReturnedToPool()
        {
            presentation?.HandleDisable();

            _dogAttackCooldownTimer = 0f;
            _desiredVelocity = Vector2.zero;
            knockbackTimer = 0f;
            knockbackVelocity = Vector2.zero;
            wasInShootZone = false;

            if (_rigidbody)
            {
                _rigidbody.linearVelocity = Vector2.zero;
                _rigidbody.angularVelocity = 0f;
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
            if (!presentation) presentation = GetComponent<EnemyPresentation>();
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

    }
}
