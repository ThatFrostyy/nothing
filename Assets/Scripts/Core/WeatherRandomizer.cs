using System;
using System.Collections.Generic;
using UnityEngine;

namespace FF
{
    public class WeatherRandomizer : MonoBehaviour
    {
        [Serializable]
        public class WeatherOption
        {
            public string Name = "Weather";
            public GameObject WeatherVfxPrefab;
            public AudioClip LoopingSfx;
        }

        [SerializeField] private List<WeatherOption> weatherOptions = new();
        [SerializeField] private Transform weatherParent;
        [SerializeField] private bool playOnEnable = true;

        private AudioSource _audioSource;
        private GameObject _activeWeather;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }

            _audioSource.loop = true;
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
        }

        private void OnEnable()
        {
            if (playOnEnable)
            {
                ActivateRandomWeather();
            }
        }

        public void ActivateRandomWeather()
        {
            if (weatherOptions == null || weatherOptions.Count == 0)
            {
                return;
            }

            int index = UnityEngine.Random.Range(0, weatherOptions.Count);
            WeatherOption option = weatherOptions[index];
            ActivateWeather(option);
        }

        private void ActivateWeather(WeatherOption option)
        {
            if (option == null)
            {
                return;
            }

            if (_activeWeather)
            {
                Destroy(_activeWeather);
                _activeWeather = null;
            }

            if (option.WeatherVfxPrefab != null)
            {
                Transform parent = weatherParent ? weatherParent : transform;
                _activeWeather = Instantiate(option.WeatherVfxPrefab, parent);
            }

            if (_audioSource != null)
            {
                _audioSource.clip = option.LoopingSfx;
                if (option.LoopingSfx != null)
                {
                    _audioSource.Play();
                }
                else
                {
                    _audioSource.Stop();
                }
            }
        }
    }
}
