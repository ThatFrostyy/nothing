using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace FF
{
    public static class AudioPlaybackPool
    {
        private const int DefaultMaxSources = 24;
        private static readonly List<AudioSource> Sources = new(DefaultMaxSources);
        private static GameObject _host;
        private static int _maxSources = DefaultMaxSources;

        public static void SetMaxSources(int limit)
        {
            _maxSources = Mathf.Max(1, limit);
            CullDestroyedSources();
        }

        public static void PlayOneShot(
            AudioClip clip,
            Vector3 position,
            AudioMixerGroup mixer = null,
            float spatialBlend = 0f,
            float volume = 1f,
            float pitch = 1f)
        {
            if (!clip)
            {
                return;
            }

            AudioSource source = GetAvailableSource();
            if (!source)
            {
                return;
            }

            source.transform.position = position;
            source.outputAudioMixerGroup = mixer;
            source.spatialBlend = Mathf.Clamp01(spatialBlend);
            source.volume = Mathf.Clamp01(volume);
            source.pitch = Mathf.Max(0.01f, pitch);
            source.PlayOneShot(clip);
        }

        private static AudioSource GetAvailableSource()
        {
            EnsureHost();
            CullDestroyedSources();

            for (int i = 0; i < Sources.Count; i++)
            {
                AudioSource source = Sources[i];
                if (source && !source.isPlaying)
                {
                    return source;
                }
            }

            if (Sources.Count < Mathf.Max(1, _maxSources))
            {
                return CreateSource();
            }

            // Reuse the oldest source when all are busy to avoid global audio starvation.
            AudioSource fallback = Sources[0];
            if (fallback)
            {
                fallback.Stop();
                return fallback;
            }

            return null;
        }

        private static AudioSource CreateSource()
        {
            EnsureHost();
            var source = _host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.hideFlags = HideFlags.HideAndDontSave;
            Sources.Add(source);
            return source;
        }

        private static void EnsureHost()
        {
            if (_host)
            {
                return;
            }

            _host = new GameObject("AudioPlaybackPool");
            _host.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(_host);
        }

        private static void CullDestroyedSources()
        {
            for (int i = Sources.Count - 1; i >= 0; i--)
            {
                if (!Sources[i])
                {
                    Sources.RemoveAt(i);
                }
            }

            if (Sources.Count <= _maxSources)
            {
                return;
            }

            int removeCount = Sources.Count - _maxSources;
            Sources.RemoveRange(Sources.Count - removeCount, removeCount);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            _host = null;
            Sources.Clear();
            _maxSources = DefaultMaxSources;
        }
    }
}
