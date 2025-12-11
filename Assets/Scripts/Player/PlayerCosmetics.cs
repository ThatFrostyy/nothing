using UnityEngine;

namespace FF
{
    public class PlayerCosmetics : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private Transform hatAnchor;

        private GameObject _hatInstance;
        private HatDefinition _currentHat;

        public static event System.Action<HatDefinition> OnHatEquipped;

        public void Apply(HatDefinition hat, Sprite bodySprite)
        {
            UpdateBody(bodySprite);
            UpdateHat(hat);

            if (hat != null && hat != _currentHat)
            {
                _currentHat = hat;
                OnHatEquipped?.Invoke(hat);
            }
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

            Transform parent = hatAnchor ? hatAnchor : transform;
            _hatInstance = Instantiate(hat.HatPrefab, parent);
            _hatInstance.transform.localPosition = hat.HatPrefab.transform.localPosition;
            _hatInstance.transform.localRotation = hat.HatPrefab.transform.localRotation;
            _hatInstance.transform.localScale = hat.HatPrefab.transform.localScale;
        }
    }
}
