using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FF
{
    [RequireComponent(typeof(Image))]
    public class ShockwaveUI : MonoBehaviour
    {
        private static ShockwaveUI _instance;

        [Header("Shockwave")]
        [SerializeField] private Image shockwaveImage;
        [SerializeField, Min(0.05f)] private float duration = 0.7f;
        [SerializeField] private AnimationCurve radiusCurve = AnimationCurve.EaseInOut(0f, 0.05f, 1f, 0.4f);
        [SerializeField] private AnimationCurve thicknessCurve = AnimationCurve.EaseInOut(0f, 0.15f, 1f, 0.04f);
        [SerializeField] private AnimationCurve strengthCurve = AnimationCurve.EaseInOut(0f, 0.5f, 1f, 0f);

        private Material _runtimeMaterial;
        private Coroutine _shockwaveRoutine;

        public static ShockwaveUI Instance => _instance;

        void Awake()
        {
            if (_instance && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            if (!shockwaveImage)
            {
                shockwaveImage = GetComponent<Image>();
            }

            if (shockwaveImage)
            {
                if (shockwaveImage.material)
                {
                    _runtimeMaterial = Instantiate(shockwaveImage.material);
                }
                else
                {
                    Shader shader = Shader.Find("FF/Effects/Shockwave2D");
                    if (shader)
                    {
                        _runtimeMaterial = new Material(shader);
                    }
                }

                if (_runtimeMaterial)
                {
                    shockwaveImage.material = _runtimeMaterial;
                    shockwaveImage.enabled = false;
                }
            }
        }

        public static void Trigger(Vector3 worldPosition, float radiusScale = 1f)
        {
            if (!_instance)
            {
                return;
            }

            _instance.Play(worldPosition, radiusScale);
        }

        public static void Trigger(Vector2 worldPosition, float radiusScale = 1f)
        {
            Trigger((Vector3)worldPosition, radiusScale);
        }

        private void Play(Vector3 worldPosition, float radiusScale)
        {
            if (!shockwaveImage || !_runtimeMaterial)
            {
                return;
            }

            Vector2 center = new Vector2(0.5f, 0.5f);
            Camera mainCamera = Camera.main;
            if (mainCamera)
            {
                Vector3 viewport = mainCamera.WorldToViewportPoint(worldPosition);
                center = new Vector2(viewport.x, viewport.y);
            }

            radiusScale = Mathf.Max(0.05f, radiusScale);

            if (_shockwaveRoutine != null)
            {
                StopCoroutine(_shockwaveRoutine);
            }

            _shockwaveRoutine = StartCoroutine(ShockwaveRoutine(center, radiusScale));
        }

        private IEnumerator ShockwaveRoutine(Vector2 center, float radiusScale)
        {
            shockwaveImage.enabled = true;
            _runtimeMaterial.SetVector("_Center", new Vector4(center.x, center.y, 0f, 0f));

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float normalized = Mathf.Clamp01(elapsed / duration);
                float radius = radiusCurve.Evaluate(normalized) * radiusScale;
                float thickness = thicknessCurve.Evaluate(normalized);
                float strength = strengthCurve.Evaluate(normalized);

                _runtimeMaterial.SetFloat("_Radius", radius);
                _runtimeMaterial.SetFloat("_Thickness", thickness);
                _runtimeMaterial.SetFloat("_Strength", strength);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _runtimeMaterial.SetFloat("_Strength", 0f);
            shockwaveImage.enabled = false;
            _shockwaveRoutine = null;
        }

        void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            if (_runtimeMaterial)
            {
                Destroy(_runtimeMaterial);
            }
        }
    }
}
