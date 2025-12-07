using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
            public bool isComposite;
        }

        [SerializeField] private InputActionAsset actions;
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private Button resetButton;
        [SerializeField] private List<BindingEntry> bindings = new();
        //
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

                bool composite = binding.isComposite;

                if (composite)
                {
                    binding.label.text = string.Join("/", GetCompositeKeys(binding));
                }
                else
                {
                    binding.label.text = InputBindingManager.GetBindingDisplay(
                        binding.action,
                        binding.bindingId,
                        binding.bindingIndex,
                        binding.label.text
                    );
                }
            }
        }

        private string[] GetCompositeKeys(BindingEntry entry)
        {
            var action = entry.action.action;
            return action.bindings
                .Where(b => b.isPartOfComposite)
                .Select(b => b.ToDisplayString())
                .ToArray();
        }


        private void StartRebind(BindingEntry entry)
        {
            if (entry == null || entry.action == null)
            {
                return;
            }

            bool composite = entry.isComposite;

            if (composite)
            {
                StartCompositeRebind(entry);
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

        private void StartCompositeRebind(BindingEntry entry)
        {
            var action = entry.action.action;

            // Fix: We need to capture the INDEX of the binding, not the path string.
            // WithTargetBinding requires the integer index of the binding in the action.bindings array.
            var compositeIndices = new List<int>();
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (action.bindings[i].isPartOfComposite)
                {
                    compositeIndices.Add(i);
                }
            }

            StartCoroutine(RebindCompositeParts(action, compositeIndices.ToArray()));
        }

        // Fix: Changed parameter from string[] to int[]
        private IEnumerator RebindCompositeParts(InputAction action, int[] bindingIndices)
        {
            // FIX 1: The action must be disabled before starting an interactive rebind.
            action.Disable();

            foreach (var i in bindingIndices)
            {
                bool done = false;

                // Use the binding index to get a nice name (e.g. "Up", "Down")
                string partName = action.bindings[i].name;

                var operation = action.PerformInteractiveRebinding()
                    .WithTargetBinding(i)
                    .OnComplete(op =>
                    {
                        op.Dispose();
                        done = true;
                    })
                    .Start();

                SetPrompt($"Press key for {partName.ToUpperInvariant()}");
                yield return new WaitUntil(() => done);
            }

            // FIX 2: Re-enable the action so the player can move again.
            action.Enable();

            // FIX 3: Force a save! 
            // (See the "Important Note" below about InputBindingManager)
            InputBindingManager.SaveOverrides();

            SetPrompt(string.Empty);
            RefreshDisplay();
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

            promptText.gameObject.SetActive(!string.IsNullOrEmpty(text));
            promptText.text = text;
        }
    }
}
