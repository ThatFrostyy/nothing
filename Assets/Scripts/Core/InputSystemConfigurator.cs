#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.InputSystem;

namespace FF
{
    [DefaultExecutionOrder(-1024)]
    public class InputSystemConfigurator : MonoBehaviour
    {
        [SerializeField]
        private InputSettings.UpdateMode updateMode = InputSettings.UpdateMode.ProcessEventsInBoth;

        [SerializeField]
        private bool applyOnAwake = true;

        private void Awake()
        {
            if (applyOnAwake)
            {
                Apply();
            }
        }

        public void Apply()
        {
            if (InputSystem.settings == null)
            {
                Debug.LogWarning("InputSystem settings are not available.");
                return;
            }

            if (InputSystem.settings.updateMode != updateMode)
            {
                InputSystem.settings.updateMode = updateMode;
            }
        }
    }
}
#endif
