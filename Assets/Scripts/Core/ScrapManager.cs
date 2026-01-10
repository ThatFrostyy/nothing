using Steamworks;
using UnityEngine;
using System;

namespace FF
{
    public class ScrapManager : MonoBehaviour
    {
        public static ScrapManager Instance { get; private set; }
        
        private const string ScrapStatName = "scrap";
        private int _currentScrap;
        public int CurrentScrap => _currentScrap;

        public event Action<int> OnScrapChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Initial fetch
            FetchScrapFromSteam();
        }

        public void FetchScrapFromSteam()
        {
            if (!SteamManager.Initialized) return;
            
            if (SteamUserStats.GetStat(ScrapStatName, out int scrap))
            {
                _currentScrap = scrap;
                OnScrapChanged?.Invoke(_currentScrap);
            }
        }

        public void AddScrap(int amount)
        {
            if (amount <= 0) return;
            _currentScrap += amount;
            PushScrapToSteam();
            OnScrapChanged?.Invoke(_currentScrap);
        }

        public bool TrySpendScrap(int amount)
        {
            if (amount <= 0) return false;
            if (_currentScrap < amount) return false;

            _currentScrap -= amount;
            PushScrapToSteam();
            OnScrapChanged?.Invoke(_currentScrap);
            return true;
        }

        private void PushScrapToSteam()
        {
            if (!SteamManager.Initialized) return;
            
            SteamUserStats.SetStat(ScrapStatName, _currentScrap);
            SteamUserStats.StoreStats();
        }
    }
}
