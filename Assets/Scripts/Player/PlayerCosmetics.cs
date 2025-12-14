using UnityEngine;

namespace FF
{
    public class PlayerCosmetics : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private Transform hatAnchor;
        [SerializeField] private Transform backAnchor;

        private GameObject _hatInstance;
        private GameObject _backpackInstance;
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

        public void SetBackpackAnchor(Transform anchor)
        {
            if (anchor)
            {
                backAnchor = anchor;
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

        public void SetBackpack(GameObject backpackPrefab)
        {
            if (_backpackInstance)
            {
                Destroy(_backpackInstance);
                _backpackInstance = null;
            }

            if (!backpackPrefab)
            {
                return;
            }

            Transform parent = backAnchor ? backAnchor : transform;
            _backpackInstance = Instantiate(backpackPrefab, parent);
            _backpackInstance.transform.localPosition = backpackPrefab.transform.localPosition;
            _backpackInstance.transform.localRotation = backpackPrefab.transform.localRotation;
            _backpackInstance.transform.localScale = backpackPrefab.transform.localScale;
        }
    }
}
