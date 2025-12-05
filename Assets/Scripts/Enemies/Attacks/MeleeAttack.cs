using UnityEngine;

namespace FF
{
    [AddComponentMenu("FF/Enemies/Attacks/Melee Attack")]
    public class MeleeAttack : MonoBehaviour, IEnemyAttack
    {
        [Header("Attack Settings")]
        [SerializeField, Min(0f)] private float attackRange = 1.25f;
        [SerializeField, Min(0.05f)] private float cooldown = 1f;
        [SerializeField, Min(0)] private int damage = 8;

        [Header("Audio & Visuals")]
        [SerializeField] private AudioClip attackClip;
        [SerializeField] private AnimationCurve leapHeightByTime = AnimationCurve.EaseInOut(0f, 0f, 1f, 0.4f);
        [SerializeField] private Transform visualRoot;

        private float _cooldownTimer;
        private AudioSource _audioSource;

        void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
        }

        public void TickAttack(Enemy enemy, Transform player, EnemyStats stats, AutoShooter shooter, float deltaTime)
        {
            if (!player)
            {
                return;
            }

            _cooldownTimer = Mathf.Max(0f, _cooldownTimer - deltaTime);
            if (_cooldownTimer > 0f)
            {
                return;
            }

            float distance = Vector2.Distance(enemy.transform.position, player.position);
            if (distance > attackRange)
            {
                return;
            }

            Health health = player.GetComponentInParent<Health>();
            if (!health)
            {
                health = player.GetComponentInChildren<Health>();
            }

            if (health && damage > 0)
            {
                float multiplier = stats ? stats.GetDamageMultiplier() : 1f;
                int finalDamage = Mathf.Max(0, Mathf.RoundToInt(damage * Mathf.Max(multiplier, 0f)));
                health.Damage(finalDamage);
            }

            if (attackClip && _audioSource)
            {
                _audioSource.PlayOneShot(attackClip, GameAudioSettings.SfxVolume);
            }

            _cooldownTimer = cooldown;
            if (enemy.isActiveAndEnabled)
            {
                enemy.StartCoroutine(LeapVisual(enemy));
            }
        }

        private System.Collections.IEnumerator LeapVisual(Enemy enemy)
        {
            Transform visual = visualRoot ? visualRoot : (enemy ? enemy.transform : null);
            if (!visual)
            {
                yield break;
            }

            float duration = Mathf.Max(0.01f, cooldown * 0.5f);
            float timer = 0f;
            Vector3 basePos = visual.position;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / duration);
                float height = leapHeightByTime.Evaluate(t);
                visual.position = basePos + Vector3.up * height;
                yield return null;
            }

            visual.position = basePos;
        }
    }
}
