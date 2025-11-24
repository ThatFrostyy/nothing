using TMPro;
using UnityEngine;

namespace FF
{
    public class DamageNumberManager : MonoBehaviour
    {
        private static DamageNumberManager _instance;

        [Header("Setup")]
        [SerializeField] private DamageNumber damageNumberPrefab;
        [SerializeField] private Color normalColor = new(1f, 0.92f, 0.84f);
        [SerializeField] private Color highlightColor = new(1f, 0.55f, 0.2f);
        [SerializeField] private float emphasizedScale = 1.2f;

        private DamageNumber _runtimePrefab;

        private static void CreateInstance()
        {
            if (_instance)
            {
                return;
            }

            var go = new GameObject("DamageNumberManager");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<DamageNumberManager>();
        }

        private void Awake()
        {
            if (_instance && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        public static void ShowDamage(Vector3 position, int amount, bool emphasized)
        {
            if (amount <= 0)
            {
                return;
            }

            if (!_instance)
            {
                CreateInstance();
            }

            _instance?.SpawnDamageNumber(position, amount, emphasized);
        }

        private void SpawnDamageNumber(Vector3 position, int amount, bool emphasized)
        {
            DamageNumber prefab = ResolvePrefab();
            if (!prefab)
            {
                return;
            }

            float scale = emphasized ? emphasizedScale : 1f;
            Color color = emphasized ? highlightColor : normalColor;

            DamageNumber instance = PoolManager.GetComponent(prefab, position, Quaternion.identity);
            if (instance)
            {
                instance.Play(position, amount, color, scale);
            }
        }

        private DamageNumber ResolvePrefab()
        {
            if (damageNumberPrefab)
            {
                return damageNumberPrefab;
            }

            if (!_runtimePrefab)
            {
                _runtimePrefab = CreateRuntimePrefab();
            }

            return _runtimePrefab;
        }

        private DamageNumber CreateRuntimePrefab()
        {
            var go = new GameObject("DamageNumberPrefab");
            var text = go.AddComponent<TextMeshPro>();
            text.text = "0";
            text.alignment = TextAlignmentOptions.Center;
            text.sortingOrder = 32000;
            text.fontSize = 3.5f;
            text.outlineWidth = 0.12f;
            text.outlineColor = new Color(0f, 0f, 0f, 0.65f);

            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Vanilla Caramel SDF");
            if (!font)
            {
                font = Resources.Load<TMP_FontAsset>("Vanilla Caramel SDF 2");
            }
            if (font)
            {
                text.font = font;
            }

            var number = go.AddComponent<DamageNumber>();
            go.SetActive(false);
            return number;
        }
    }
}
