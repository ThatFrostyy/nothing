using UnityEngine;

public class YSorter : MonoBehaviour
{
    public int baseOrder = 1000;
    public int offsetBody = 0;
    public int offsetGun = 1;

    private SpriteRenderer bodyRenderer;
    private SpriteRenderer gunRenderer;

    void Awake()
    {
        bodyRenderer = transform.Find("Visual")?.GetComponent<SpriteRenderer>();
        gunRenderer = transform.Find("GunPivot")?.GetComponentInChildren<SpriteRenderer>();
    }

    void LateUpdate()
    {
        int order = baseOrder - Mathf.RoundToInt(transform.position.y * 10f);

        if (bodyRenderer) bodyRenderer.sortingOrder = order + offsetBody;
        if (gunRenderer) gunRenderer.sortingOrder = order + offsetGun;
    }
}
