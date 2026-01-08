using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FF
{
    public class MeleeAttacker : MonoBehaviour
    {
        private Weapon _weapon;
        private Transform _attackOrigin;
        private ICombatStats _stats;
        private Coroutine _swingRoutine;

        private void Awake()
        {
            _stats = GetComponentInParent<ICombatStats>();
        }

        public void SetWeapon(Weapon weapon, Transform attackOrigin)
        {
            _weapon = weapon;
            _attackOrigin = attackOrigin;
        }

        public void ClearWeapon()
        {
            _weapon = null;
            _attackOrigin = null;
        }

        public void PerformAttack()
        {
            if (_weapon == null || !_weapon.isMelee || _attackOrigin == null || _swingRoutine != null)
            {
                return;
            }

            _swingRoutine = StartCoroutine(SwingAnimation());
        }

        private IEnumerator SwingAnimation()
        {
            var alreadyHit = new List<Collider2D>();
            string ownerTag = transform.root ? transform.root.tag : gameObject.tag;

            float swingDuration = _weapon.attackArc / _weapon.swingSpeed;
            float timer = 0f;

            Quaternion initialRotation = _attackOrigin.localRotation;
            Quaternion startRotation = initialRotation * Quaternion.Euler(0, 0, _weapon.attackArc / 2f);
            Quaternion endRotation = initialRotation * Quaternion.Euler(0, 0, -_weapon.attackArc / 2f);

            while (timer < swingDuration)
            {
                timer += Time.deltaTime;
                float progress = timer / swingDuration;
                _attackOrigin.localRotation = Quaternion.Lerp(startRotation, endRotation, progress);

                Collider2D[] hits = Physics2D.OverlapCircleAll(_attackOrigin.position, _weapon.attackRange);
                foreach (Collider2D hit in hits)
                {
                    if (alreadyHit.Contains(hit) || hit.CompareTag(ownerTag)) continue;

                    Vector2 directionToHit = (hit.transform.position - _attackOrigin.position).normalized;
                    float angle = Vector2.Angle(_attackOrigin.right, directionToHit);

                    if (angle <= _weapon.attackArc / 2f)
                    {
                        Health health = hit.GetComponent<Health>();
                        if (health != null)
                        {
                            alreadyHit.Add(hit);
                            float damageMultiplier = _stats != null ? _stats.GetDamageMultiplier() : 1f;
                            int finalDamage = Mathf.RoundToInt(_weapon.damage * damageMultiplier);
                            health.Damage(finalDamage, _weapon, false);

                            if (_weapon.knockbackStrength > 0)
                            {
                                Rigidbody2D hitRb = hit.GetComponent<Rigidbody2D>();
                                if (hitRb != null)
                                {
                                    Vector2 knockbackDirection = (hit.transform.position - _attackOrigin.position).normalized;
                                    hitRb.AddForce(knockbackDirection * _weapon.knockbackStrength, ForceMode2D.Impulse);
                                }
                            }
                        }
                    }
                }
                yield return null;
            }

            _attackOrigin.localRotation = initialRotation;
            _swingRoutine = null;
        }
    }
}
