using UnityEngine;
using UnityEngine.UI; // Required if applying this to a UI Image

public class Backgroud : MonoBehaviour
{
    [Header("Scaling (Pulse) Settings")]
    [Tooltip("The base scale of the image (e.g., 1.0)")]
    public float baseScale = 1.0f;
    [Tooltip("The maximum amount the scale will increase/decrease (e.g., 0.1 for 10%)")]
    public float scaleAmplitude = 0.1f;
    [Tooltip("How fast the pulsing happens (e.g., 2.0 for 2 cycles per second)")]
    public float pulseSpeed = 2.0f;
    public bool scale = true;
    [Tooltip("Check this if your sprite is flipped horizontally (e.g., base X scale is -1)")]
    public bool flip = false; // <-- NEW BOOLEAN ADDED HERE

    [Header("Rotation (Sway) Settings")]
    [Tooltip("The maximum angle the image will rotate left/right (e.g., 5 degrees)")]
    public float maxSwayAngle = 5.0f;
    [Tooltip("How fast the swaying happens (e.g., 1.5 for 1.5 cycles per second)")]
    public float swaySpeed = 1.5f;
    public bool sway = true;

    void Update()
    {
        if (scale)
        {
            // --- 1. Calculate Scale (Pulse) ---
            float pulseFactor = Mathf.Sin(Time.time * pulseSpeed) * scaleAmplitude;
            float currentScale = baseScale + pulseFactor;

            // Determine the X-scale multiplier based on the 'flip' checkbox.
            // If flip is true, the multiplier is -1, otherwise it is 1.
            float flipMultiplier = flip ? -1f : 1f; // <-- NEW FLIP LOGIC

            // Apply the pulse factor and the flip multiplier.
            // X-scale uses the multiplier for the flip, Y and Z remain normal.
            transform.localScale = new Vector3(
                currentScale * flipMultiplier, // X-scale is multiplied by -1 if flipped
                currentScale,
                currentScale
            );
        }

        if (sway)
        {
            // --- 2. Calculate Rotation (Sway) ---
            float swayValue = Mathf.Sin((Time.time * swaySpeed) + 0.5f) * maxSwayAngle;
            transform.localRotation = Quaternion.Euler(0f, 0f, swayValue);
        }
    }
}