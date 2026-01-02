using UnityEngine;
using System.Collections.Generic;

public class Ground : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Collider2D _boundsCollider;

    [Header("Environment Spawning")]
    [SerializeField] private List<GameObject> environmentPrefabs; // bushes, trees, etc.
    [SerializeField, Min(0)] private int spawnCount = 25;
    [SerializeField, Min(0f)] private float spawnPadding = 1f;

    private Bounds _worldBounds;

    public static Ground Instance { get; private set; }

    public Bounds WorldBounds => _worldBounds;

    void Awake()
    {
        Instance = this;

        if (!_spriteRenderer) _spriteRenderer = GetComponent<SpriteRenderer>();
        if (!_boundsCollider) _boundsCollider = GetComponent<Collider2D>();
        CacheBounds();
    }

    void OnEnable() => CacheBounds();

    void Start()
    {
        SpawnEnvironment();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // --- NEW: Random spawn inside bounds ---
    private void SpawnEnvironment()
    {
        if (environmentPrefabs == null || environmentPrefabs.Count == 0) return;

        for (int i = 0; i < spawnCount; i++)
        {
            GameObject prefab = environmentPrefabs[Random.Range(0, environmentPrefabs.Count)];
            Vector2 spawnPos = GetRandomPointInsideBounds(spawnPadding);

            Instantiate(prefab, spawnPos, Quaternion.identity, transform);
        }
    }

    private Vector2 GetRandomPointInsideBounds(float padding)
    {
        Vector2 min = (Vector2)_worldBounds.min;
        Vector2 max = (Vector2)_worldBounds.max;

        min += Vector2.one * padding;
        max -= Vector2.one * padding;

        return new Vector2(
            Random.Range(min.x, max.x),
            Random.Range(min.y, max.y)
        );
    }

    // --- Your existing clamp logic ---
    public Vector2 ClampPoint(Vector2 worldPoint, Vector2 padding)
    {
        if (_worldBounds.size == Vector3.zero)
            return worldPoint;

        padding = new Vector2(Mathf.Max(padding.x, 0f), Mathf.Max(padding.y, 0f));

        Vector3 min3 = _worldBounds.min;
        Vector3 max3 = _worldBounds.max;

        Vector2 min = (Vector2)min3;
        Vector2 max = (Vector2)max3;

        min.x += padding.x;
        min.y += padding.y;
        max.x -= padding.x;
        max.y -= padding.y;

        if (min.x > max.x)
        {
            float mid = (min.x + max.x) * 0.5f;
            min.x = max.x = mid;
        }

        if (min.y > max.y)
        {
            float mid = (min.y + max.y) * 0.5f;
            min.y = max.y = mid;
        }

        return new Vector2(
            Mathf.Clamp(worldPoint.x, min.x, max.x),
            Mathf.Clamp(worldPoint.y, min.y, max.y)
        );
    }

    public Vector3 ClampPoint(Vector3 worldPoint, Vector2 padding)
    {
        Vector2 clamped = ClampPoint((Vector2)worldPoint, padding);
        worldPoint.x = clamped.x;
        worldPoint.y = clamped.y;
        return worldPoint;
    }

    private void CacheBounds()
    {
        if (_spriteRenderer && _spriteRenderer.sprite)
            _worldBounds = _spriteRenderer.bounds;
        else if (_boundsCollider)
            _worldBounds = _boundsCollider.bounds;
        else
            _worldBounds = new Bounds(transform.position, Vector3.zero);
    }
}
