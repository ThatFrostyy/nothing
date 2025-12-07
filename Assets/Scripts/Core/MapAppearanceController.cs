using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FF
{
    public class MapAppearanceController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer groundRenderer;
        [SerializeField] private WeatherRandomizer weatherRandomizer;
        [SerializeField] private Light2D lighting;
        [SerializeField] private MapDefinition defaultMap;

        private void OnEnable()
        {
            MapSelectionState.OnMapChanged += HandleMapChanged;
            ApplySelectedMap();
        }

        private void OnDisable()
        {
            MapSelectionState.OnMapChanged -= HandleMapChanged;
        }

        private void HandleMapChanged(MapDefinition map)
        {
            ApplyMap(map);
        }

        private void ApplySelectedMap()
        {
            ApplyMap(MapSelectionState.SelectedMap ?? defaultMap);
        }

        private void ApplyMap(MapDefinition map)
        {
            MapDefinition targetMap = map ? map : defaultMap;
            if (targetMap == null)
            {
                return;
            }

            if (groundRenderer)
            {
                groundRenderer.sprite = targetMap.GroundSprite;
            }

            if (lighting)
            {
                lighting.color = targetMap.LightingColor;
            }

            if (weatherRandomizer)
            {
                weatherRandomizer.SetWeatherOptions(targetMap.AvailableWeather);
                weatherRandomizer.SetChanceForNoWeather(targetMap.ChanceForNoWeather);
                weatherRandomizer.ActivateRandomWeather();
            }
        }
    }
}
