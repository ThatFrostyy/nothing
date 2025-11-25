using UnityEngine;

namespace FF
{
    [CreateAssetMenu(fileName = "GameSettings", menuName = "Config/Game Settings")]
    public class GameSettings : ScriptableObject
    {
        private static GameSettings _instance;

        [Header("Camera")]
        [SerializeField] private bool screenShakeEnabled = true;
        [SerializeField, Min(0f)] private float screenShakeIntensity = 1f;
        [SerializeField, Min(0f)] private float recoilMultiplier = 1f;
        [SerializeField, Min(0.01f)] private float cameraFollowSmoothTime = 0.1f;

        public bool ScreenShakeEnabled => screenShakeEnabled;
        public float ScreenShakeIntensity => screenShakeIntensity;
        public float RecoilMultiplier => recoilMultiplier;
        public float CameraFollowSmoothTime => cameraFollowSmoothTime;

        public static GameSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<GameSettings>("GameSettings");
                    if (_instance == null)
                    {
                        _instance = CreateInstance<GameSettings>();
                    }
                }

                return _instance;
            }
            set => _instance = value;
        }
    }
}
