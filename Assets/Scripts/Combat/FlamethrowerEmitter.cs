using System;
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
        private float _baseRange;
        private float _range;
        private float _coneAngle;
        private float _tickInterval;
        private LayerMask _hitMask;
        private string _ownerTag;
        private bool _isFiring;
        private Coroutine _fadeRoutine;
        private float _rangeMultiplier = 1f;

        public void Initialize(Weapon weapon, Transform followTarget, string ownerTag)
        {
            _sourceWeapon = weapon;
            _followTarget = followTarget;
            _ownerTag = ownerTag;
            _baseRange = weapon ? Mathf.Max(0.1f, weapon.flamethrowerRange) : defaultRange;
            _range = _baseRange;
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

        public void Tick(
            bool isFiring,
            int baseDamagePerSecond,
            float damageMultiplier,
            float critChance,
            float critDamageMultiplier,
            string ownerTag)
        {
            _ownerTag = ownerTag;
            FollowMuzzleImmediate();
            UpdateRange();

            if (isFiring)
            {
                // Ensure the VFX reference is valid on every tick while firing.
                // The pooled instance can be returned/reparented externally which
                // may leave _activeVfx referencing a stale object without the
                // actual particle systems — detect and recover from that.
                EnsureVfxActive();

                BeginFiring();
                ApplyDamage(baseDamagePerSecond, damageMultiplier, critChance, critDamageMultiplier);
            }
            else
            {
                EndFiring();
            }
        }

        public void SetRangeMultiplier(float multiplier)
        {
            _rangeMultiplier = Mathf.Max(0.1f, multiplier);
            UpdateRange();
        }

        void UpdateRange()
        {
            _range = Mathf.Max(0.1f, _baseRange * _rangeMultiplier);
            ApplyVfxLengthScale();
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
            if (_activeVfx == null || !_activeVfx.activeInHierarchy)
            {
                // defensive retry
                StopLoopingVfx();
                StartLoopingVfx();
            }
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

        private void ApplyDamage(int baseDamagePerSecond, float damageMultiplier, float critChance, float critDamageMultiplier)
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

            float scaledDamagePerSecond = Mathf.Max(1f, baseDamagePerSecond * Mathf.Max(0f, damageMultiplier));
            int tickDamage = Mathf.Max(1, Mathf.CeilToInt(scaledDamagePerSecond * _tickInterval));
            critChance = Mathf.Clamp01(critChance);
            bool isCritical = critChance > 0f && UnityEngine.Random.value < critChance;
            if (isCritical)
            {
                tickDamage = Mathf.Max(1, Mathf.CeilToInt(tickDamage * Mathf.Max(1f, critDamageMultiplier)));
            }
            foreach (var enemy in _targets)
            {
                if (enemy && enemy.TryGetComponent(out Health health))
                {
                    health.Damage(tickDamage, _sourceWeapon, isCritical);
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
                _sourceWeapon,
                _sourceWeapon.burnTargetVfxOffset);
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
                _activeVfx.SetActive(true);
                _activeVfx.transform.SetParent(transform, false);
                ApplyVfxLengthScale();
                _activeVfx.transform.SetPositionAndRotation(position, transform.rotation);
                _activeVfx.transform.localScale = Vector3.one;
                _activeVfx.transform.SetLocalPositionAndRotation(vfxOffset, Quaternion.identity);
                if (_activeVfx.TryGetComponent<PooledParticleSystem>(out var pooled))
                {
                    pooled.OnTakenFromPool();
                }
                else
                {
                    pooled = _activeVfx.AddComponent<PooledParticleSystem>();
                    pooled.OnTakenFromPool();
                }

                ApplyVfxLengthScale();
            }
            Debug.Log($"Flame VFX spawn: {_activeVfx?.name}, active={_activeVfx?.activeInHierarchy}");
        }

        // Defensive check to ensure the _activeVfx reference actually contains
        // a visible particle effect. Some pool implementations reparent or
        // deactivate the original root and spawn a separate runtime root which
        // contains the particle systems; this can leave our reference pointing
        // at an empty holder. If we detect that problem, try to recover by
        // searching children and reacquiring a valid instance or respawning.
        private void EnsureVfxActive()
        {
            if (_activeVfx == null)
            {
                return;
            }

            // If the referenced object is inactive in hierarchy, try to reacquire
            if (!_activeVfx.activeInHierarchy)
            {
                Debug.Log("Flame VFX reference inactive in hierarchy — clearing reference so it can be respawned.");
                _activeVfx = null;
                return;
            }

            // Check if this gameobject (or any child) contains at least one ParticleSystem//
            bool hasParticle = false;
            if (_activeVfx.GetComponentInChildren<ParticleSystem>() != null)
            {
                hasParticle = true;
            }

            // If no ParticleSystem found, it may have been reparented under a new root.
            if (!hasParticle)
            {
                // Try to find a child under the same transform with ParticleSystems
                Transform root = _activeVfx.transform.parent;
                if (root != null)
                {
                    ParticleSystem found = root.GetComponentInChildren<ParticleSystem>();
                    if (found != null)
                    {
                        // Adopt the root that actually contains the particles
                        GameObject candidate = found.gameObject;
                        while (candidate.transform.parent != null && candidate.transform.parent != root)
                        {
                            candidate = candidate.transform.parent.gameObject;
                        }

                        if (candidate != null)
                        {
                            Debug.Log($"Flame VFX moved under parent '{root.name}' — switching active vfx to '{candidate.name}'");
                            _activeVfx = candidate;
                            return;
                        }
                    }
                }

                // Fallback: clear reference so StartLoopingVfx can respawn it.
                Debug.Log("Flame VFX no longer contains particle systems — clearing reference to force respawn.");
                _activeVfx = null;
            }
        }

        private void StopLoopingVfx()
        {
            if (!_activeVfx)
            {
                return;
            }

            Debug.Log("Stopping flame VFX");
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

        private void ApplyVfxLengthScale()
        {
            if (!_activeVfx)
            {
                return;
            }

            float baseRange = Mathf.Max(0.0001f, _baseRange);
            float lengthScale = Mathf.Clamp(Mathf.Max(0.1f, _range) / baseRange, 0.1f, 100f);
            if (float.IsNaN(lengthScale) || float.IsInfinity(lengthScale)) lengthScale = 1f;
            Vector3 currentScale = _activeVfx.transform.localScale;
            _activeVfx.transform.localScale = new Vector3(currentScale.x, lengthScale, currentScale.z);
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
            float range = _range > 0f ? _range : (_sourceWeapon ? Mathf.Max(0.1f, _sourceWeapon.flamethrowerRange) : defaultRange);
            float cone = _coneAngle > 0f ? _coneAngle : (_sourceWeapon ? Mathf.Clamp(_sourceWeapon.flamethrowerConeAngle, 1f, 180f) : defaultConeAngle);

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
