using UnityEngine;
using UnityEngine.InputSystem;

namespace FF
{
    [RequireComponent(typeof(RectTransform))]
    public class CursorFollowUI : MonoBehaviour
    {
        private RectTransform _rectTransform;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        private void Update()
        {
            if (_rectTransform == null || Mouse.current == null)
            {
                return;
            }

            _rectTransform.position = Mouse.current.position.ReadValue();
        }
    }
}
