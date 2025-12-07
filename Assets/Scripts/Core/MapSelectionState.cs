using System;

namespace FF
{
    public static class MapSelectionState
    {
        public static event Action<MapDefinition> OnMapChanged;

        public static MapDefinition SelectedMap { get; private set; }
        public static bool HasSelection => SelectedMap != null;

        public static void SetSelection(MapDefinition map)
        {
            if (map == SelectedMap)
            {
                return;
            }

            SelectedMap = map;
            OnMapChanged?.Invoke(SelectedMap);
        }

        public static void ClearSelection()
        {
            if (SelectedMap == null)
            {
                return;
            }

            SelectedMap = null;
            OnMapChanged?.Invoke(null);
        }
    }
}
