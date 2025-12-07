using System.Collections.Generic;
using UnityEngine;

namespace FF
{
    [CreateAssetMenu(menuName = "FF/Map", fileName = "Map_")]
    public class MapDefinition : ScriptableObject
    {
        [Header("Meta")]
        public string MapName = "Map";
        [TextArea]
        public string Description;

        [Header("Visuals")]
        public Sprite GroundSprite;
        public Color LightingColor = Color.white;

        [Header("Weather")]
        [SerializeField] private List<WeatherRandomizer.WeatherOption> availableWeather = new();
        [SerializeField, Range(0f, 1f)] private float chanceForNoWeather = 0.1f;

        [Header("Gameplay Overrides")]
        public GameObject MovementEffectOverride;

        public IReadOnlyList<WeatherRandomizer.WeatherOption> AvailableWeather => availableWeather;
        public float ChanceForNoWeather => chanceForNoWeather;
    }
}
