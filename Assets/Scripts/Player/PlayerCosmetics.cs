using UnityEngine;

namespace FF
{
    public class PlayerCosmetics : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private Transform hatAnchor;

        private GameObject _hatInstance;

        public void Apply(HatDefinition hat, Sprite bodySprite)
        {
            UpdateBody(bodySprite);
            UpdateHatAnchor(bodySprite);
            UpdateHat(hat);
        }

        public void SetRenderTargets(SpriteRenderer body, Transform anchor)
        {
            if (body)
            {
                bodyRenderer = body;
            }

            if (anchor)
            {
                hatAnchor = anchor;
            }
        }

        private void UpdateBody(Sprite sprite)
        {
            if (!bodyRenderer)
            {
                return;
            }

            bodyRenderer.sprite = sprite != null ? sprite : bodyRenderer.sprite;
        }

        private void UpdateHatAnchor(Sprite spriteOverride)
        {
            if (!bodyRenderer)
            {
                return;
            }

            if (!hatAnchor)
            {
                GameObject anchorObject = new("HatAnchor");
                anchorObject.transform.SetParent(bodyRenderer.transform, false);
                hatAnchor = anchorObject.transform;
            }

            Sprite sprite = spriteOverride != null ? spriteOverride : bodyRenderer.sprite;
            if (!sprite)
            {
                hatAnchor.localPosition = Vector3.zero;
                return;
            }

            Vector3 offset = new(0f, sprite.bounds.extents.y, 0f);
            hatAnchor.localPosition = offset;
        }

        private void UpdateHat(HatDefinition hat)
        {
            if (_hatInstance)
            {
                Destroy(_hatInstance);
                _hatInstance = null;
            }

            if (hat == null || hat.HatPrefab == null)
            {
                return;
            }

            Transform parent = hatAnchor ? hatAnchor : transform;
            _hatInstance = Instantiate(hat.HatPrefab, parent);
            _hatInstance.transform.localPosition = hat.HatPrefab.transform.localPosition;
            _hatInstance.transform.localRotation = hat.HatPrefab.transform.localRotation;
            _hatInstance.transform.localScale = hat.HatPrefab.transform.localScale;
        }
    }
}
