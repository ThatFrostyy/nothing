using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace FF
{
    public interface ISceneReferenceHandler
    {
        void ClearSceneReferences();
    }

    public static class SceneReferenceRegistry
    {
        static readonly List<ISceneReferenceHandler> Handlers = new();

        static SceneReferenceRegistry()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        public static void Register(ISceneReferenceHandler handler)
        {
            if (handler == null || Handlers.Contains(handler))
            {
                return;
            }

            Handlers.Add(handler);
        }

        public static void Unregister(ISceneReferenceHandler handler)
        {
            if (handler == null)
            {
                return;
            }

            Handlers.Remove(handler);
        }

        public static void ResetSceneReferences()
        {
            InvokeClearReferences();
        }

        static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InvokeClearReferences();
        }

        static void InvokeClearReferences()
        {
            for (int i = Handlers.Count - 1; i >= 0; i--)
            {
                Handlers[i]?.ClearSceneReferences();
            }
        }
    }
}
