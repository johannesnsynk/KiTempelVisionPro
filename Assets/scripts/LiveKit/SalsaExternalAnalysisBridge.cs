using System;
using UnityEngine;

namespace NSYNK.LiveKitIntegration
{
    /// <summary>
    /// Computes a stable analysis value from the audio thread and exposes it for SALSA external analysis.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SalsaExternalAnalysisBridge : MonoBehaviour
    {
        [SerializeField]
        private float _gain = 6.0f;

        [SerializeField]
        private float _smoothAttack = 18.0f;

        [SerializeField]
        private float _smoothRelease = 8.0f;

        [SerializeField]
        private float _noiseGate = 0.0025f;

        private volatile float _latestLevel;
        private float _smoothedLevel;

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (data == null || data.Length == 0)
            {
                _latestLevel = 0f;
                return;
            }

            double sumSquares = 0d;
            for (int i = 0; i < data.Length; i++)
            {
                float s = data[i];
                sumSquares += s * s;
            }

            float rms = (float)Math.Sqrt(sumSquares / data.Length);
            if (rms < _noiseGate)
                rms = 0f;

            _latestLevel = Mathf.Clamp01(rms * _gain);
        }

        /// <summary>
        /// Delegate target for salsa.getExternalAnalysis.
        /// </summary>
        public float GetAnalysisValue()
        {
            float target = _latestLevel;
            float speed = target > _smoothedLevel ? _smoothAttack : _smoothRelease;
            _smoothedLevel = Mathf.MoveTowards(_smoothedLevel, target, speed * Time.unscaledDeltaTime);
            return _smoothedLevel;
        }
    }
}
