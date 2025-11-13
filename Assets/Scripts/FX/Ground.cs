using UnityEngine;

public class Ground : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Collider2D _boundsCollider;

    private Bounds _worldBounds;

    public static Ground Instance { get; private set; }

    public Bounds WorldBounds => _worldBounds;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple Ground instances detected in the scene.", this);
        }

        Instance = this;
        CacheBounds();
    }

    void OnEnable()
    {
        CacheBounds();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void OnValidate()
    {
        if (!_spriteRenderer) _spriteRenderer = GetComponent<SpriteRenderer>();
        if (!_boundsCollider) _boundsCollider = GetComponent<Collider2D>();

        CacheBounds();
    }

    public Vector2 ClampPoint(Vector2 worldPoint, Vector2 padding)
    {
        if (_worldBounds.size == Vector3.zero)
        {
            return worldPoint;
        }

        padding = new Vector2(Mathf.Max(padding.x, 0f), Mathf.Max(padding.y, 0f));

        Vector3 min3 = _worldBounds.min;
        Vector3 max3 = _worldBounds.max;

        Vector2 min = new Vector2(min3.x, min3.y);
        Vector2 max = new Vector2(max3.x, max3.y);

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
        {
            _worldBounds = _spriteRenderer.bounds;
        }
        else if (_boundsCollider)
        {
            _worldBounds = _boundsCollider.bounds;
        }
        else
        {
            _worldBounds = new Bounds(transform.position, Vector3.zero);
        }
    }
}
