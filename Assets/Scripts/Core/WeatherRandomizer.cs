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
        [SerializeField, Range(0f, 1f)] private float chanceForNoWeather = 0.1f;

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
            ApplyAmbienceVolume();
        }

        private void OnEnable()
        {
            GameAudioSettings.OnAmbienceVolumeChanged += HandleAmbienceVolumeChanged;

            if (playOnEnable)
            {
                ActivateRandomWeather();
            }
        }

        private void OnDisable()
        {
            GameAudioSettings.OnAmbienceVolumeChanged -= HandleAmbienceVolumeChanged;
        }

        public void ActivateRandomWeather()
        {
            if (weatherOptions == null || weatherOptions.Count == 0)
            {
                ClearActiveWeather();
                return;
            }

            if (chanceForNoWeather > 0f && UnityEngine.Random.value < Mathf.Clamp01(chanceForNoWeather))
            {
                ClearActiveWeather();
                return;
            }

            int index = UnityEngine.Random.Range(0, weatherOptions.Count);
            WeatherOption option = weatherOptions[index];
            ActivateWeather(option);
        }

        public void SetWeatherOptions(IEnumerable<WeatherOption> options)
        {
            weatherOptions = options != null ? new List<WeatherOption>(options) : new List<WeatherOption>();
        }

        public void SetChanceForNoWeather(float chance)
        {
            chanceForNoWeather = Mathf.Clamp01(chance);
        }

        private void ActivateWeather(WeatherOption option)
        {
            if (option == null)
            {
                return;
            }

            ClearActiveWeather();

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
                    ApplyAmbienceVolume();
                    _audioSource.Play();
                }
                else
                {
                    _audioSource.Stop();
                }
            }
        }

        private void ClearActiveWeather()
        {
            if (_activeWeather)
            {
                Destroy(_activeWeather);
                _activeWeather = null;
            }

            if (_audioSource != null)
            {
                _audioSource.Stop();
                _audioSource.clip = null;
            }
        }

        private void ApplyAmbienceVolume()
        {
            if (_audioSource != null)
            {
                _audioSource.volume = GameAudioSettings.AmbienceVolume;
            }
        }

        private void HandleAmbienceVolumeChanged(float value)
        {
            ApplyAmbienceVolume();
        }
    }
}
