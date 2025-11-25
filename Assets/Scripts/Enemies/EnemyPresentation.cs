using System.Collections;
using UnityEngine;

namespace FF
{
    public class EnemyPresentation : MonoBehaviour
    {
        private const float FacingDeadZone = 0.05f;

        [Header("Visual References")]
        [SerializeField] private Transform gunPivot;
        [SerializeField] private Vector3 gunOffsetRight = new(0.35f, -0.1f, 0f);
        [SerializeField] private Vector3 gunOffsetLeft = new(-0.35f, -0.1f, 0f);
        [SerializeField] private Transform enemyVisual;

        [Header("Animation Settings")]
        [SerializeField] private float walkBobFrequency = 6f;
        [SerializeField] private float walkBobAmplitude = 0.12f;
        [SerializeField] private float walkSquashAmount = 0.08f;
        [SerializeField] private float idleSwayFrequency = 1.5f;
        [SerializeField] private float idleSwayAmplitude = 3f;

        [Header("Helmets")]
        [SerializeField] private Transform helmetAnchor;
        [SerializeField] private GameObject[] helmetPrefabs;
        [SerializeField, Range(0f, 1f)] private float chanceForNoHelmet = 0.25f;

        private Rigidbody2D _rigidbody;
        private EnemyStats _stats;
        private WeaponManager _weaponManager;
        private Vector3 _baseVisualLocalPosition;
        private Vector3 _baseVisualLocalScale = Vector3.one;
        private float _bobTimer;
        private bool _isFacingLeft;
        private Vector3 _visualPositionVelocity;
        private Vector3 _visualScaleVelocity;
        private float _tiltVelocity;
        private float _bobStrength;
        private float _facingBlend = 1f;
        private float _facingVelocity;
        private Coroutine _dogJumpRoutine;
        private Vector3 _dogAttackOffset = Vector3.zero;
        private GameObject _helmetInstance;
        private Vector2 _lastAimDirection = Vector2.right;

        public void Initialize(Enemy enemy, Rigidbody2D body, EnemyStats stats, WeaponManager weaponManager)
        {
            _rigidbody = body;
            _stats = stats;
            _weaponManager = weaponManager;

            if (!gunPivot && _weaponManager)
            {
                gunPivot = _weaponManager.GunPivot;
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
                    SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
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
            ResetVisualState();
        }

        public void HandleEnable()
        {
            if (_dogJumpRoutine != null)
            {
                StopCoroutine(_dogJumpRoutine);
                _dogJumpRoutine = null;
            }

            ResetVisualState();
        }

        public void HandleDisable()
        {
            if (_dogJumpRoutine != null)
            {
                StopCoroutine(_dogJumpRoutine);
                _dogJumpRoutine = null;
            }

            _dogAttackOffset = Vector3.zero;
        }

        public void ResetVisualState()
        {
            _bobTimer = 0f;
            _facingBlend = 1f;
            _facingVelocity = 0f;
            _visualPositionVelocity = Vector3.zero;
            _visualScaleVelocity = Vector3.zero;
            _tiltVelocity = 0f;
            _bobStrength = 0f;
            _dogAttackOffset = Vector3.zero;
        }

        public void UpdateAim(Transform target)
        {
            if (!gunPivot || !target)
            {
                return;
            }

            Vector2 dir = target.position - gunPivot.position;
            if (dir.sqrMagnitude < 0.001f)
            {
                return;
            }

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

            if (_weaponManager)
            {
                _weaponManager.transform.localPosition = facingLeft ? gunOffsetLeft : gunOffsetRight;
            }

            Vector3 scale = gunPivot.localScale;
            scale.y = facingLeft ? -1f : 1f;
            gunPivot.localScale = scale;
        }

        public void UpdateVisuals(Vector2 desiredVelocity, Vector2 currentVelocity)
        {
            if (!enemyVisual)
            {
                return;
            }

            float bodyTiltDegrees = _stats ? _stats.BodyTiltDegrees : 12f;
            float speed = currentVelocity.magnitude;
            float maxSpeed = Mathf.Max(_stats ? _stats.MoveSpeed : 1f, Mathf.Epsilon);
            float normalizedSpeed = speed / maxSpeed;

            float targetTilt = speed > 0.1f ? -bodyTiltDegrees * normalizedSpeed : bodyTiltDegrees * 0.3f;

            if (desiredVelocity.sqrMagnitude > 0.01f)
            {
                float side = Mathf.Clamp(desiredVelocity.normalized.x, -1f, 1f);
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

        public void TriggerDogJump(float leapHeight, float leapDuration)
        {
            if (!enemyVisual)
            {
                return;
            }

            if (_dogJumpRoutine != null)
            {
                StopCoroutine(_dogJumpRoutine);
            }

            _dogJumpRoutine = StartCoroutine(DogJumpRoutine(leapHeight, leapDuration));
        }

        private void UpdateWalkCycle(float normalizedSpeed)
        {
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

        private IEnumerator DogJumpRoutine(float dogLeapHeight, float dogLeapDuration)
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

        private void OnValidate()
        {
            walkBobFrequency = Mathf.Max(0f, walkBobFrequency);
            walkBobAmplitude = Mathf.Max(0f, walkBobAmplitude);
            walkSquashAmount = Mathf.Max(0f, walkSquashAmount);
            idleSwayFrequency = Mathf.Max(0f, idleSwayFrequency);
            idleSwayAmplitude = Mathf.Max(0f, idleSwayAmplitude);
        }
    }
}
