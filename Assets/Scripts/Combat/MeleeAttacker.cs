using Unity.Netcode;
using UnityEngine;

namespace FF
{
    public class MeleeAttacker : NetworkBehaviour
    {
        private Weapon _weapon;
        private Transform _attackOrigin;
        private ICombatStats _stats;

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
            if (!IsOwner) return;
            if (_weapon == null || !_weapon.isMelee || _attackOrigin == null)
            {
                return;
            }

            PerformAttackServerRpc();
        }

        [ServerRpc]
        private void PerformAttackServerRpc()
        {
            string ownerTag = transform.root ? transform.root.tag : gameObject.tag;

            Collider2D[] hits = Physics2D.OverlapCircleAll(_attackOrigin.position, _weapon.attackRange);

            foreach (Collider2D hit in hits)
            {
                if (hit.CompareTag(ownerTag)) continue;

                Vector2 directionToHit = (hit.transform.position - _attackOrigin.position).normalized;
                float angle = Vector2.Angle(_attackOrigin.right, directionToHit);

                if (angle <= _weapon.attackArc / 2f)
                {
                    Health health = hit.GetComponent<Health>();
                    if (health != null)
                    {
                        float damageMultiplier = _stats != null ? _stats.GetDamageMultiplier() : 1f;
                        int finalDamage = Mathf.RoundToInt(_weapon.damage * damageMultiplier);
                        health.Damage(finalDamage, gameObject);

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

            PerformAttackClientRpc();
        }

        [ClientRpc]
        private void PerformAttackClientRpc()
        {
            if (_weapon.swingPrefab != null)
            {
                Instantiate(_weapon.swingPrefab, _attackOrigin.position, _attackOrigin.rotation);
            }
        }
    }
}
