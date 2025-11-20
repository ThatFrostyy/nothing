using UnityEngine;
using UnityEngine.InputSystem;

public class CursorFollowUI : MonoBehaviour
{
    RectTransform rect;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    void Update()
    {
        if (Mouse.current == null) return;

        Vector2 pos = Mouse.current.position.ReadValue();
        rect.position = pos;   // no camera involvement ? no shake
    }
}
