using NSYNK;
using NSYNK.HyperSlides.Core;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace NSYNK.HyperSlides.UI
{
    /// <summary>
    /// This class is responsible for managing the global volume slider in the UI.
    /// It updates the slider value based on the global volume set in HyperslidesAudioManager.
    /// </summary>
    [RequireComponent(typeof(Slider))]
    public class UIMasterVolumeSlider : MonoBehaviour
    {
        Slider volumeSlider;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            volumeSlider = GetComponent<Slider>();
            this.ExecuteNextFrame(() =>
            {
                if (volumeSlider != null)
                {
                    volumeSlider.value = HyperslidesAudioManager.Instance.GetMasterVolume();
                    volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
                }
            });
        }

        private void OnVolumeChanged(float newVolume)
        {
            HyperslidesAudioManager.Instance.SetMasterVolume(newVolume);
        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}

