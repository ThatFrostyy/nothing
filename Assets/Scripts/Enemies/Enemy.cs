using System;
using UnityEngine;

namespace FF
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(EnemyStats))]
    [RequireComponent(typeof(Health))]
    public class Enemy : MonoBehaviour
    {
        public static event Action<Enemy> OnAnyEnemyKilled;

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

        [Header("Audio & FX")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip []hitSound;
        [SerializeField] private AudioClip []deathSound;
        [SerializeField] private GameObject deathFX;

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
        private Vector2 _desiredVelocity;
        private Vector2 _smoothedSeparation;
        private Vector3 _baseVisualLocalPosition;
        private Vector3 _baseVisualLocalScale = Vector3.one;
        private float _bobTimer;
        private bool _isFacingLeft;
        private Collider2D[] _avoidanceResults;
        private ContactFilter2D _avoidanceFilter;
        private AudioSource _audioSource;
        private const int AvoidanceBufferCeiling = 256;
        private int lastIndex = -1;
        private Vector3 _visualPositionVelocity;
        private Vector3 _visualScaleVelocity;
        private float _tiltVelocity;
        private float _bobStrength;
        private float _facingBlend = 1f;
        private float _facingVelocity;

        public void Initialize(Transform player)
        {
            _player = player;
        }

        public void ApplyWaveModifiers(EnemyWaveModifiers modifiers)
        {
            if (_health != null && modifiers.HealthMultiplier > 0f)
            {
                _health.ScaleMaxHP(modifiers.HealthMultiplier, true);
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
                _health.OnDamaged += HandleDamaged;
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.OnDeath -= HandleDeath;
                _health.OnDamaged -= HandleDamaged;
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
            Vector2 force = separationDirection * pushStrength * crowdingStrength;

            return Vector2.ClampMagnitude(force, pushStrength);
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

            float idleBlend = 1f - Mathf.Clamp01(normalizedSpeed * 3f);
            if (idleBlend > 0f)
            {
                targetTilt += Mathf.Sin(Time.time * idleSwayFrequency) * idleSwayAmplitude * idleBlend;
            }

            float newZ = Mathf.SmoothDampAngle(
                enemyVisual.localEulerAngles.z,
                targetTilt,
                ref _tiltVelocity,
                0.12f
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
            Vector3 targetLocalPosition = _baseVisualLocalPosition + new Vector3(0f, bobOffset, 0f);
            enemyVisual.localPosition = Vector3.SmoothDamp(
                enemyVisual.localPosition,
                targetLocalPosition,
                ref _visualPositionVelocity,
                0.08f,
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
                0.08f,
                Mathf.Infinity,
                Time.deltaTime
            );
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

            var handler = OnAnyEnemyKilled;
            if (handler != null)
            {
                handler(this);
            }

            PlayDeathSound();
            SpawnDeathFx();
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

            GameObject audioObject = new GameObject("EnemyDeathSound");
            audioObject.transform.position = transform.position;

            var tempSource = audioObject.AddComponent<AudioSource>();
            tempSource.clip = clip;
            tempSource.volume = volume;
            tempSource.pitch = pitch;
            tempSource.spatialBlend = spatialBlend;
            tempSource.outputAudioMixerGroup = mixerGroup;
            tempSource.Play();

            Destroy(audioObject, clip.length / Mathf.Max(tempSource.pitch, 0.01f));
        }


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
                    spawnPosition += new Vector3(offset.x, offset.y, 0f);
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
