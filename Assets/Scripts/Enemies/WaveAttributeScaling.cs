using UnityEngine;

namespace FF
{
    [System.Serializable]
    public class WaveAttributeScaling
    {
        public AnimationCurve healthMultiplierByWave = AnimationCurve.Linear(1f, 1f, 20f, 4f);
        public AnimationCurve moveSpeedMultiplierByWave = AnimationCurve.Linear(1f, 1f, 20f, 1.6f);
        public AnimationCurve fireRateMultiplierByWave = AnimationCurve.Linear(1f, 1f, 20f, 1.5f);
        public AnimationCurve damageMultiplierByWave = AnimationCurve.Linear(1f, 1f, 20f, 2.2f);
        public AnimationCurve xpMultiplierByWave = AnimationCurve.Linear(1f, 1f, 20f, 2.5f);

        public EnemyWaveModifiers CreateModifiers(int wave)
        {
            return new EnemyWaveModifiers(
                Evaluate(healthMultiplierByWave, wave, 1f),
                Evaluate(moveSpeedMultiplierByWave, wave, 1f),
                Evaluate(fireRateMultiplierByWave, wave, 1f),
                Evaluate(damageMultiplierByWave, wave, 1f),
                Evaluate(xpMultiplierByWave, wave, 1f));
        }

        private static float Evaluate(AnimationCurve curve, int wave, float fallback)
        {
            if (curve == null || curve.length == 0)
            {
                return fallback;
            }

            return Mathf.Max(0.01f, curve.Evaluate(Mathf.Max(1, wave)));
        }
    }
}
