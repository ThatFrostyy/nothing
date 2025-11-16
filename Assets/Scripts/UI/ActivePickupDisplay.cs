using System.Collections.Generic;
using UnityEngine;

namespace FF
{
    public class ActivePickupDisplay : MonoBehaviour
    {
        [SerializeField] private RectTransform container;
        [SerializeField] private ActivePickupEntry entryPrefab;

        private readonly List<ActivePickupEntry> activeEntries = new();

        void OnEnable()
        {
            UpgradePickup.OnEffectApplied += HandleEffectApplied;
        }

        void OnDisable()
        {
            UpgradePickup.OnEffectApplied -= HandleEffectApplied;
            ClearEntries();
        }

        void Update()
        {
            UpdateEntries();
        }

        private void HandleEffectApplied(UpgradePickupEffect effect)
        {
            if (!effect || effect.Duration <= 0f || !entryPrefab)
            {
                return;
            }

            ActivePickupEntry entry = Instantiate(entryPrefab, container ? container : transform);
            entry.Initialize(effect.Icon, effect.Multiplier, effect.Duration);
            activeEntries.Add(entry);
        }

        private void UpdateEntries()
        {
            for (int i = activeEntries.Count - 1; i >= 0; i--)
            {
                if (!activeEntries[i] || !activeEntries[i].UpdateEntry())
                {
                    DestroyEntry(i);
                }
            }
        }

        private void ClearEntries()
        {
            for (int i = activeEntries.Count - 1; i >= 0; i--)
            {
                DestroyEntry(i);
            }
        }

        private void DestroyEntry(int index)
        {
            if (index < 0 || index >= activeEntries.Count)
            {
                return;
            }

            ActivePickupEntry entry = activeEntries[index];
            activeEntries.RemoveAt(index);
            if (entry)
            {
                Destroy(entry.gameObject);
            }
        }
    }
}
