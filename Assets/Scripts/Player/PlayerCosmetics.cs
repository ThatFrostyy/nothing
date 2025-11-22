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

            Transform anchor = hatAnchor
                ? hatAnchor
                : bodyRenderer != null
                    ? bodyRenderer.transform
                    : transform;
            Transform parent = anchor.parent ? anchor.parent : anchor;

            Vector3 localPosition = parent.InverseTransformPoint(anchor.position);
            Quaternion prefabLocalRotation = hat.HatPrefab.transform.localRotation;
            Quaternion localRotation = Quaternion.Inverse(parent.rotation) * anchor.rotation * prefabLocalRotation;
            Vector3 localScale = anchor.localScale;

            _hatInstance = Instantiate(hat.HatPrefab, parent);
            _hatInstance.transform.localPosition = localPosition;
            _hatInstance.transform.localRotation = localRotation;
            _hatInstance.transform.localScale = localScale;
        }
    }
}
