using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FF
{
    public static class InputBindingManager
    {
        public static event Action OnBindingsChanged;

        private const string PlayerPrefsKey = "InputBindingOverrides";

        private static InputActionAsset _asset;
        private static bool _isInitialized;
        private static bool _isRebinding;

        public static void Initialize(InputActionAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            if (_asset == asset && _isInitialized)
            {
                return;
            }

            _asset = asset;
            _isInitialized = true;

            string saved = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(saved))
            {
                _asset.LoadBindingOverridesFromJson(saved);
            }
        }

        public static string GetBindingDisplay(InputActionReference actionReference, string bindingId, int fallbackIndex = 0, string defaultLabel = "?")
        {
            InputAction action = ResolveAction(actionReference);
            if (action == null)
            {
                return defaultLabel;
            }

            int index = ResolveBindingIndex(action, bindingId, fallbackIndex);
            if (index < 0 || index >= action.bindings.Count)
            {
                return defaultLabel;
            }

            return action.GetBindingDisplayString(index, out _, out _);
        }

        public static bool StartRebind(InputActionReference actionReference, string bindingId, int fallbackIndex, Action<string> onPrompt, Action onComplete, Action onCancel = null)
        {
            if (_isRebinding)
            {
                return false;
            }

            InputAction action = ResolveAction(actionReference);
            if (action == null)
            {
                return false;
            }

            int bindingIndex = ResolveBindingIndex(action, bindingId, fallbackIndex);
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            {
                return false;
            }

            _isRebinding = true;
            onPrompt?.Invoke("Listening for input...");

            action.Disable();
            var operation = action.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .OnCancel(op =>
                {
                    action.Enable();
                    op.Dispose();
                    _isRebinding = false;
                    onCancel?.Invoke();
                })
                .OnComplete(op =>
                {
                    action.Enable();
                    op.Dispose();
                    SaveOverrides();
                    _isRebinding = false;
                    onComplete?.Invoke();
                    OnBindingsChanged?.Invoke();
                });

            operation.Start();
            return true;
        }

        public static void ResetBindings()
        {
            if (_asset == null)
            {
                return;
            }

            _asset.RemoveAllBindingOverrides();
            SaveOverrides();
            OnBindingsChanged?.Invoke();
        }

        public static void SaveOverrides()
        {
            if (_asset == null)
            {
                return;
            }

            string json = _asset.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString(PlayerPrefsKey, json);
            PlayerPrefs.Save();
            SteamCloudSave.SaveToCloud();
        }

        private static InputAction ResolveAction(InputActionReference actionReference)
        {
            if (actionReference == null)
            {
                return null;
            }

            Initialize(actionReference.action?.actionMap?.asset);
            return actionReference.action;
        }

        private static int ResolveBindingIndex(InputAction action, string bindingId, int fallbackIndex)
        {
            if (action == null)
            {
                return -1;
            }

            if (!string.IsNullOrEmpty(bindingId))
            {
                int foundIndex = action.bindings.ToList().FindIndex(b => b.id.ToString() == bindingId || b.name == bindingId);
                if (foundIndex >= 0)
                {
                    return foundIndex;
                }
            }

            fallbackIndex = Mathf.Clamp(fallbackIndex, 0, action.bindings.Count - 1);
            return fallbackIndex;
        }
    }
}
