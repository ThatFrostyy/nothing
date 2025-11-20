using TMPro;
using UnityEngine;

namespace FF
{
    [RequireComponent(typeof(PoolToken))]
    public class DamageNumber : MonoBehaviour, IPoolable
    {
        [Header("Movement")]
        [SerializeField] private float lifetime = 1.2f;
        [SerializeField] private Vector2 driftRange = new(0.15f, 1.35f);
        [SerializeField] private Vector2 spawnJitter = new(0.3f, 0.25f);
        [SerializeField] private float gravity = -1.5f;

        [Header("Visuals")]
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0.35f, 1f, 0f);
        [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        [SerializeField] private float baseFontSize = 3.5f;

        private TextMeshPro _text;
        private PoolToken _poolToken;
        private float _timer;
        private Vector3 _velocity;
        private Color _color;
        private float _scaleMultiplier = 1f;

        private void Awake()
        {
            _text = GetComponent<TextMeshPro>();
            if (!_text)
            {
                _text = gameObject.AddComponent<TextMeshPro>();
            }

            _text.alignment = TextAlignmentOptions.Center;
            _text.enableKerning = true;
            _text.sortingOrder = 32000;
            _text.fontSize = baseFontSize;
            _text.outlineWidth = 0.12f;
            _text.outlineColor = new Color(0f, 0f, 0f, 0.65f);

            _poolToken = GetComponent<PoolToken>();
            if (!_poolToken)
            {
                _poolToken = gameObject.AddComponent<PoolToken>();
            }
        }

        public void Play(Vector3 worldPosition, int amount, Color color, float sizeMultiplier = 1f)
        {
            transform.position = worldPosition + new Vector3(
                Random.Range(-spawnJitter.x, spawnJitter.x),
                Random.Range(0f, spawnJitter.y),
                0f);

            _text.text = amount.ToString();
            _text.fontSize = baseFontSize * Mathf.Max(0.05f, sizeMultiplier);
            _color = color;
            _scaleMultiplier = Mathf.Max(0.05f, sizeMultiplier);

            _velocity = new Vector3(
                Random.Range(-driftRange.x, driftRange.x),
                Random.Range(driftRange.y * 0.6f, driftRange.y),
                0f);

            _timer = 0f;
            UpdateVisual(0f);
        }

        private void Update()
        {
            _timer += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(_timer / Mathf.Max(0.01f, lifetime));

            _velocity.y += gravity * Time.unscaledDeltaTime;
            transform.position += _velocity * Time.unscaledDeltaTime;
            UpdateVisual(normalized);

            if (_timer >= lifetime && _poolToken != null)
            {
                _poolToken.Release();
            }
        }

        private void UpdateVisual(float normalized)
        {
            float scale = scaleCurve != null ? Mathf.Max(0f, scaleCurve.Evaluate(normalized)) : 1f - normalized;
            float alpha = alphaCurve != null ? Mathf.Clamp01(alphaCurve.Evaluate(normalized)) : 1f - normalized;

            if (_text)
            {
                Color c = _color;
                c.a *= alpha;
                _text.color = c;
                transform.localScale = Vector3.one * scale * _scaleMultiplier;
            }
        }

        public void OnTakenFromPool()
        {
            _timer = 0f;
        }

        public void OnReturnedToPool()
        {
            _timer = 0f;
            _velocity = Vector3.zero;
            _scaleMultiplier = 1f;
        }
    }
}
