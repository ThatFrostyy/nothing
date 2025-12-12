using System.Collections.Generic;
using UnityEngine;

namespace FF
{
    public class FlamethrowerEmitter : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float defaultTickInterval = 0.1f;
        [SerializeField, Min(0.1f)] private float defaultRange = 5f;
        [SerializeField, Range(1f, 180f)] private float defaultConeAngle = 45f;
        [SerializeField] private LayerMask defaultHitMask = ~0;
        [SerializeField, Min(0.01f)] private float stopFadeDuration = 0.2f;
        [SerializeField] private Vector3 vfxOffset = Vector3.zero;
        [SerializeField] private Vector3 rotationOffsetEuler = new(0f, 0f, 90f);

        private readonly Collider2D[] _overlapResults = new Collider2D[24];
        private readonly List<Enemy> _targets = new();

        private Transform _followTarget;
        private Weapon _sourceWeapon;
        private GameObject _activeVfx;
        private AudioSource _loopSource;
        private float _tickTimer;
        private float _range;
        private float _coneAngle;
        private float _tickInterval;
        private LayerMask _hitMask;
        private string _ownerTag;
        private bool _isFiring;
        private Coroutine _fadeRoutine;

        public void Initialize(Weapon weapon, Transform followTarget, string ownerTag)
        {
            _sourceWeapon = weapon;
            _followTarget = followTarget;
            _ownerTag = ownerTag;
            _range = weapon ? Mathf.Max(0.1f, weapon.flamethrowerRange) : defaultRange;
            _coneAngle = weapon ? Mathf.Clamp(weapon.flamethrowerConeAngle, 1f, 180f) : defaultConeAngle;
            _tickInterval = weapon ? Mathf.Max(0.05f, weapon.flamethrowerTickInterval) : defaultTickInterval;
            _hitMask = weapon && weapon.flamethrowerHitMask != 0 ? weapon.flamethrowerHitMask : defaultHitMask;
            _tickTimer = 0f;

            if (_loopSource == null)
            {
                _loopSource = gameObject.AddComponent<AudioSource>();
                _loopSource.playOnAwake = false;
                _loopSource.loop = true;
                _loopSource.spatialBlend = 0f;
            }

            if (_sourceWeapon != null && _sourceWeapon.fireLoopSFX)
            {
                _loopSource.clip = _sourceWeapon.fireLoopSFX;
            }

            ResetLoopVolume();
            FollowMuzzleImmediate();
        }

        public void Tick(bool isFiring, int damagePerSecond, string ownerTag)
        {
            _ownerTag = ownerTag;
            FollowMuzzleImmediate();

            if (isFiring)
            {
                BeginFiring();
                ApplyDamage(damagePerSecond);
            }
            else
            {
                EndFiring();
            }
        }

        private void BeginFiring()
        {
            if (_isFiring)
            {
                return;
            }

            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
                ResetLoopVolume();
            }

            _isFiring = true;
            _tickTimer = 0f;
            StartLoopingVfx();
            StartLoopingAudio();
        }

        private void EndFiring()
        {
            if (!_isFiring)
            {
                return;
            }

            _isFiring = false;
            StopLoopingVfx();
            FadeOutAudio();
        }

        private void ApplyDamage(int damagePerSecond)
        {
            _tickTimer -= Time.deltaTime;
            if (_tickTimer > 0f)
            {
                return;
            }

            _tickTimer += _tickInterval;

            int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, _range, _overlapResults, _hitMask);
            if (hitCount <= 0)
            {
                return;
            }

            Vector2 forward = (Vector2)transform.right;
            float halfAngle = _coneAngle * 0.5f;
            _targets.Clear();

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D col = _overlapResults[i];
                if (!col || col.CompareTag(_ownerTag))
                {
                    continue;
                }

                Vector2 toTarget = (Vector2)(col.transform.position - transform.position);
                if (Vector2.Angle(forward, toTarget) > halfAngle)
                {
                    continue;
                }

                if (col.TryGetComponent(out Enemy enemy) && !_targets.Contains(enemy))
                {
                    _targets.Add(enemy);
                }
            }

            if (_targets.Count == 0)
            {
                return;
            }

            int tickDamage = Mathf.Max(1, Mathf.CeilToInt(damagePerSecond * _tickInterval));
            foreach (var enemy in _targets)
            {
                if (enemy && enemy.TryGetComponent(out Health health))
                {
                    health.Damage(tickDamage, _sourceWeapon);
                }

                TryApplyBurn(enemy);
            }
        }

        private void TryApplyBurn(Enemy enemy)
        {
            if (!_sourceWeapon || !_sourceWeapon.appliesBurn || enemy == null)
            {
                return;
            }

            if (_sourceWeapon.burnDamagePerSecond <= 0 || _sourceWeapon.burnDuration <= 0f)
            {
                return;
            }

            enemy.ApplyBurn(
                Mathf.Max(0f, _sourceWeapon.burnDuration),
                Mathf.Max(0, _sourceWeapon.burnDamagePerSecond),
                Mathf.Max(0.05f, _sourceWeapon.burnTickInterval),
                _sourceWeapon.burnTargetVfx,
                _sourceWeapon);
        }

        private void StartLoopingVfx()
        {
            if (_activeVfx || _sourceWeapon == null || !_sourceWeapon.loopingFireVfx)
            {
                return;
            }

            Vector3 position = transform.position + transform.TransformVector(vfxOffset);
            _activeVfx = PoolManager.Get(_sourceWeapon.loopingFireVfx, position, transform.rotation);
            if (_activeVfx)
            {
                _activeVfx.transform.SetParent(transform, true);
                _activeVfx.transform.position = position;
                _activeVfx.transform.rotation = transform.rotation;
                _activeVfx.transform.localScale = Vector3.one;
                _activeVfx.transform.localPosition = vfxOffset;
                _activeVfx.transform.localRotation = Quaternion.identity;

                if (_activeVfx.TryGetComponent<PooledParticleSystem>(out var pooled))
                {
                    pooled.OnTakenFromPool();
                }
                else
                {
                    pooled = _activeVfx.AddComponent<PooledParticleSystem>();
                    pooled.OnTakenFromPool();
                }
            }
        }

        private void StopLoopingVfx()
        {
            if (!_activeVfx)
            {
                return;
            }

            _activeVfx.transform.SetParent(null, true);

            if (_activeVfx.TryGetComponent<PooledParticleSystem>(out var pooled))
            {
                pooled.StopEmitting();
            }
            else
            {
                var particleSystems = _activeVfx.GetComponentsInChildren<ParticleSystem>();
                foreach (var system in particleSystems)
                {
                    system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }

                Destroy(_activeVfx, Mathf.Max(0.01f, stopFadeDuration));
            }

            _activeVfx = null;
        }

        private void StartLoopingAudio()
        {
            if (!_loopSource || !_loopSource.clip)
            {
                return;
            }

            if (!_loopSource.isPlaying)
            {
                _loopSource.volume = GameAudioSettings.SfxVolume;
                _loopSource.Play();
            }
            else
            {
                _loopSource.volume = GameAudioSettings.SfxVolume;
            }
        }

        private void FadeOutAudio()
        {
            if (_loopSource == null)
            {
                return;
            }

            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
            }

            _fadeRoutine = StartCoroutine(FadeOutRoutine());
        }

        private System.Collections.IEnumerator FadeOutRoutine()
        {
            float duration = Mathf.Max(0.01f, stopFadeDuration);
            float startVolume = _loopSource.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _loopSource.volume = Mathf.Lerp(startVolume, 0f, t);
                yield return null;
            }

            _loopSource.Stop();
            ResetLoopVolume();
            _fadeRoutine = null;
        }

        private void ResetLoopVolume()
        {
            if (_loopSource != null)
            {
                _loopSource.volume = GameAudioSettings.SfxVolume;
            }
        }

        private void FollowMuzzleImmediate()
        {
            if (!_followTarget)
            {
                return;
            }

            transform.position = _followTarget.position;
            transform.rotation = _followTarget.rotation * Quaternion.Euler(rotationOffsetEuler);
        }

        private void OnDisable()
        {
            StopLoopingVfx();
            if (_loopSource && _loopSource.isPlaying)
            {
                _loopSource.Stop();
            }
        }

        private void OnDrawGizmosSelected()
        {
            float range = _sourceWeapon ? Mathf.Max(0.1f, _sourceWeapon.flamethrowerRange) : defaultRange;
            float cone = _sourceWeapon ? Mathf.Clamp(_sourceWeapon.flamethrowerConeAngle, 1f, 180f) : defaultConeAngle;

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);

            Vector3 origin = transform.position;
            Vector3 forward = transform.right;
            float halfAngle = cone * 0.5f;
            Quaternion leftRot = Quaternion.AngleAxis(-halfAngle, Vector3.forward);
            Quaternion rightRot = Quaternion.AngleAxis(halfAngle, Vector3.forward);

            Gizmos.DrawRay(origin, leftRot * forward * range);
            Gizmos.DrawRay(origin, rightRot * forward * range);

            int segments = 16;
            Vector3 prevPoint = origin + leftRot * forward * range;
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                Quaternion stepRot = Quaternion.AngleAxis(Mathf.Lerp(-halfAngle, halfAngle, t), Vector3.forward);
                Vector3 nextPoint = origin + stepRot * forward * range;
                Gizmos.DrawLine(prevPoint, nextPoint);
                prevPoint = nextPoint;
            }

            Gizmos.DrawWireSphere(origin, 0.05f);
        }
    }
}
