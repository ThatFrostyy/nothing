using UnityEngine;

namespace FF
{
    [DisallowMultipleComponent]
    public class YSorter : MonoBehaviour
    {
        [Header("Sorting Order")]
        [SerializeField] private int baseOrder = 1000;
        [SerializeField] private int offsetBody = 0;
        [SerializeField] private int offsetGun = 1;

        private SpriteRenderer _bodyRenderer;
        private SpriteRenderer _gunRenderer;

        private void Awake()
        {
            _bodyRenderer = transform.Find("Visual")?.GetComponent<SpriteRenderer>();
            _gunRenderer = transform.Find("GunPivot")?.GetComponentInChildren<SpriteRenderer>();
        }

        private void LateUpdate()
        {
            int order = baseOrder - Mathf.RoundToInt(transform.position.y * 10f);

            if (_bodyRenderer)
            {
                _bodyRenderer.sortingOrder = order + offsetBody;
            }

            if (_gunRenderer)
            {
                _gunRenderer.sortingOrder = order + offsetGun;
            }
        }
    }
}
