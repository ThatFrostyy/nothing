using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FF
{
    public class KeybindSettingsUI : MonoBehaviour
    {
        [Serializable]
        private class BindingEntry
        {
            public string bindingId;
            public int bindingIndex;
            public InputActionReference action;
            public TMP_Text label;
            public Button rebindButton;
        }

        [SerializeField] private InputActionAsset actions;
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private Button resetButton;
        [SerializeField] private List<BindingEntry> bindings = new();

        private void Awake()
        {
            if (!actions && bindings.Count > 0)
            {
                actions = bindings[0].action?.action?.actionMap?.asset;
            }

            if (resetButton)
            {
                resetButton.onClick.AddListener(ResetBindings);
            }

            foreach (var binding in bindings)
            {
                if (binding.rebindButton)
                {
                    var localBinding = binding;
                    binding.rebindButton.onClick.AddListener(() => StartRebind(localBinding));
                }
            }
        }

        private void OnEnable()
        {
            InputBindingManager.Initialize(actions);
            InputBindingManager.OnBindingsChanged += RefreshDisplay;
            RefreshDisplay();
        }

        private void OnDisable()
        {
            InputBindingManager.OnBindingsChanged -= RefreshDisplay;
        }

        public void RefreshDisplay()
        {
            foreach (var binding in bindings)
            {
                if (binding.label == null)
                {
                    continue;
                }

                string display = InputBindingManager.GetBindingDisplay(binding.action, binding.bindingId, binding.bindingIndex, binding.label.text);
                binding.label.text = display;
            }
        }

        private void StartRebind(BindingEntry entry)
        {
            if (entry == null || entry.action == null)
            {
                return;
            }

            bool started = InputBindingManager.StartRebind(
                entry.action,
                entry.bindingId,
                entry.bindingIndex,
                prompt => SetPrompt($"{entry.action.name}: {prompt}"),
                () =>
                {
                    SetPrompt(string.Empty);
                    RefreshDisplay();
                },
                () => SetPrompt(string.Empty)
            );

            if (!started)
            {
                SetPrompt("Unable to start rebind");
            }
        }

        private void ResetBindings()
        {
            InputBindingManager.ResetBindings();
            RefreshDisplay();
            SetPrompt("Bindings reset to default");
        }

        private void SetPrompt(string text)
        {
            if (!promptText)
            {
                return;
            }

            promptText.text = text;
        }
    }
}
