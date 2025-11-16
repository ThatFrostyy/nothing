using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace FF
{
    public static class AudioPlaybackPool
    {
        private const int DefaultMaxSources = 24;
        private static readonly List<AudioSource> Sources = new(DefaultMaxSources);
        private static GameObject host;
        private static int maxSources = DefaultMaxSources;

        public static void SetMaxSources(int limit)
        {
            maxSources = Mathf.Max(1, limit);
        }

        public static void PlayOneShot(AudioClip clip, Vector3 position, AudioMixerGroup mixer = null, float spatialBlend = 0f, float volume = 1f, float pitch = 1f)
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

            for (int i = 0; i < Sources.Count; i++)
            {
                AudioSource source = Sources[i];
                if (source && !source.isPlaying)
                {
                    return source;
                }
            }

            if (Sources.Count < Mathf.Max(1, maxSources))
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
            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.hideFlags = HideFlags.HideAndDontSave;
            Sources.Add(source);
            return source;
        }

        private static void EnsureHost()
        {
            if (host)
            {
                return;
            }

            host = new GameObject("AudioPlaybackPool");
            host.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(host);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            host = null;
            Sources.Clear();
            maxSources = DefaultMaxSources;
        }
    }
}
