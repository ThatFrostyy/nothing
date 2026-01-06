using System;
using UnityEngine;


namespace FF
{
    public class Health : MonoBehaviour
    {
        [Header("Health Settings")]
        [SerializeField] int maxHP = 50;

        int hp;
        int baseMaxHP;
        Weapon lastDamageSourceWeapon;
        readonly System.Collections.Generic.List<TimedDamageModifier> _damageModifiers = new();

        // New: persistent flat bonus to max HP that survives ScaleMaxHP calls.
        int permanentFlatMaxHP = 0;

        public System.Action<int> OnDamaged;
        public System.Action OnDeath;
        public System.Action<int, int> OnHealthChanged;
        public event Func<Health, bool> OnBeforeDeath;
        public static System.Action<Health, int, Weapon> OnAnyDamaged;
        public static System.Action<Health, int> OnAnyHealed;

        public int MaxHP => maxHP;
        public int CurrentHP => hp;

        void Awake()
        {
            CacheBaseValues();
            ResetHealth(true);
        }

        public Weapon LastDamageSourceWeapon => lastDamageSourceWeapon;

        public void Damage(int amount, Weapon sourceWeapon = null, bool isCritical = false)
        {
            if (amount <= 0) return;

            int adjustedAmount = ApplyDamageModifiers(amount);
            if (adjustedAmount <= 0)
            {
                return;
            }

            int previousHp = hp;
            hp = Mathf.Max(0, hp - adjustedAmount);

            if (sourceWeapon)
            {
                lastDamageSourceWeapon = sourceWeapon;
            }

            int damageApplied = Mathf.Min(adjustedAmount, previousHp);
            if (damageApplied > 0)
            {
                OnDamaged?.Invoke(damageApplied);

                OnAnyDamaged?.Invoke(this, damageApplied, sourceWeapon);

                if (TryGetComponent<Enemy>(out var enemy))
                {
                    bool emphasize = isCritical || enemy.IsBoss || damageApplied >= Mathf.Max(10, maxHP * 0.35f);
                    DamageNumberManager.ShowDamage(transform.position, adjustedAmount, emphasize);
                }
            }

            OnHealthChanged?.Invoke(hp, maxHP);

            if (hp <= 0) Die();
        }

        public void Heal(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            int previousHp = hp;
            hp = Mathf.Min(maxHP, hp + amount);

            if (hp != previousHp)
            {
                int healedAmount = hp - previousHp;
                OnHealthChanged?.Invoke(hp, maxHP);
                OnAnyHealed?.Invoke(this, healedAmount);
            }
        }

        public void ApplyTemporaryDamageMultiplier(float multiplier, float duration)
        {
            if (multiplier <= 0f)
            {
                return;
            }

            float expiry = duration > 0f ? Time.time + duration : float.PositiveInfinity;
            _damageModifiers.Add(new TimedDamageModifier(multiplier, expiry));
        }

        private void Die()
        {
            if (TryPreventDeath())
            {
                return;
            }

            OnDeath?.Invoke();

            if (TryGetComponent(out PoolToken token) && token.Owner != null)
            {
                token.Release();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private bool TryPreventDeath()
        {
            if (OnBeforeDeath == null)
            {
                return false;
            }

            foreach (Func<Health, bool> handler in OnBeforeDeath.GetInvocationList())
            {
                if (handler != null && handler(this))
                {
                    return true;
                }
            }

            return false;
        }

        private void Update()
        {
            UpdateDamageModifiers();
        }

        private void UpdateDamageModifiers()
        {
            if (_damageModifiers.Count == 0)
            {
                return;
            }

            float now = Time.time;
            for (int i = _damageModifiers.Count - 1; i >= 0; i--)
            {
                if (_damageModifiers[i].Expiry <= now)
                {
                    _damageModifiers.RemoveAt(i);
                }
            }
        }

        private float GetDamageMultiplier()
        {
            if (_damageModifiers.Count == 0)
            {
                return 1f;
            }

            float value = 1f;
            for (int i = 0; i < _damageModifiers.Count; i++)
            {
                value *= _damageModifiers[i].Multiplier;
            }

            return value;
        }

        private int ApplyDamageModifiers(int amount)
        {
            float multiplier = GetDamageMultiplier();
            if (Mathf.Approximately(multiplier, 1f))
            {
                return amount;
            }

            int adjusted = Mathf.RoundToInt(amount * multiplier);
            return Mathf.Max(0, adjusted);
        }

        private readonly struct TimedDamageModifier
        {
            public readonly float Multiplier;
            public readonly float Expiry;

            public TimedDamageModifier(float multiplier, float expiry)
            {
                Multiplier = multiplier;
                Expiry = expiry;
            }
        }

        #region Max HP Management
        public void SetMaxHP(int amount, bool refill = true)
        {
            amount = Mathf.Max(1, amount);
            maxHP = amount;
            if (refill)
            {
                hp = maxHP;
            }
            else
            {
                hp = Mathf.Clamp(hp, 0, maxHP);
            }
            OnHealthChanged?.Invoke(hp, maxHP);
        }

        // New: add a permanent flat max HP bonus that survives subsequent ScaleMaxHP calls.
        public void AddPermanentMaxHP(int amount, bool refill = true)
        {
            if (amount <= 0)
            {
                return;
            }

            permanentFlatMaxHP = Mathf.Max(0, permanentFlatMaxHP + amount);

            // Use baseMaxHP as the canonical base to apply the flat bonus on top of.
            if (baseMaxHP <= 0)
            {
                CacheBaseValues();
            }

            SetMaxHP(baseMaxHP + permanentFlatMaxHP, refill);
        }

        public void ScaleMaxHP(float multiplier, bool refill = true)
        {
            if (multiplier <= 0f)
            {
                return;
            }

            if (baseMaxHP <= 0)
            {
                CacheBaseValues();
            }

            // Include permanent flat bonus when scaling so flat increases aren't lost.
            int scaled = Mathf.Max(1, Mathf.RoundToInt((baseMaxHP + permanentFlatMaxHP) * multiplier));
            SetMaxHP(scaled, refill);
        }
        #endregion Max HP Management

        #region Resetting
        void CacheBaseValues()
        {
            baseMaxHP = Mathf.Max(1, maxHP);
        }

        public void ResetToBase()
        {
            SetMaxHP(baseMaxHP, true);
        }

        void ResetHealth(bool refill)
        {
            if (baseMaxHP <= 0)
            {
                CacheBaseValues();
            }

            lastDamageSourceWeapon = null;
            _damageModifiers.Clear();

            if (refill)
            {
                hp = Mathf.Max(1, baseMaxHP);
            }
            else
            {
                hp = Mathf.Clamp(hp, 0, baseMaxHP);
            }
            OnHealthChanged?.Invoke(hp, maxHP);
        }
        #endregion Resetting

        void OnValidate()
        {
            maxHP = Mathf.Max(1, maxHP);
            if (!Application.isPlaying)
            {
                baseMaxHP = maxHP;
            }
        }
    }
}
