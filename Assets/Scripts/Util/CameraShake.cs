using UnityEngine;


namespace FF
{
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake I;

        Vector3 basePos;
        float time, duration, intensity;

        void Awake()
        {
            I = this; basePos = transform.localPosition;
        }

        void Update()
        {
            if (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float t = 1f - (time / duration);
                Vector2 rnd = intensity * t * Random.insideUnitCircle;
                transform.localPosition = basePos + (Vector3)rnd;
            }
            else transform.localPosition = basePos;
        }

        public static void Shake(float dur = 0.08f, float inten = 0.1f)
        {
            if (!I) return;
            I.duration = Mathf.Max(dur, I.duration);
            I.intensity = Mathf.Max(inten, I.intensity);
            I.time = 0f;
        }
    }
}