using UnityEngine;
using UnityEngine.Audio;

namespace FF
{
    public class AudioController : MonoBehaviour
    {
        public void PlayFire(Weapon weapon, Vector3 position, AudioSource source)
        {
            if (weapon == null || weapon.fireSFX == null)
            {
                return;
            }

            AudioMixerGroup mixer = source ? source.outputAudioMixerGroup : null;
            float spatialBlend = source ? source.spatialBlend : 0f;
            float volume = source ? source.volume : 1f;
            float pitch = source ? source.pitch : 1f;

            AudioPlaybackPool.PlayOneShot(weapon.fireSFX, position, mixer, spatialBlend, volume, pitch);
        }
    }
}
