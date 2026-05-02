using TMPro;
using UnityEngine;

namespace NSYNK.HyperSlides.Runtime
{
    /// <summary>
    /// Displays the current frame rate in a TextMeshProUGUI component.
    /// </summary>
    public class UIFrameRateIndicator : MonoBehaviour
    {
        public float SmoothSpeed = 5f;

        private float fps, smoothFps;
        private TextMeshProUGUI textComponent;

        void Start() => textComponent = GetComponent<TextMeshProUGUI>();

        void Update()
        {
            if (textComponent == null)
                return;

            fps = 1f / Time.unscaledDeltaTime;
            if (Time.timeSinceLevelLoad < 1.0f) smoothFps = fps;
            smoothFps += (fps - smoothFps) * Mathf.Clamp(Time.unscaledDeltaTime * SmoothSpeed, 0, 1);
            textComponent.text = ((int)smoothFps).ToString() + " fps";
        }
    }
}