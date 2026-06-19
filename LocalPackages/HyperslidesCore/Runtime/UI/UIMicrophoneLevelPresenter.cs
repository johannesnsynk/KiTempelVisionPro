using NSYNK.HyperSlides.Core;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


namespace NSYNK.HyperSlides.UI
{
    /// <summary>
    /// This class is responsible for displaying the microphone levels for each channel.
    /// It initializes visualizers based on the number of channels and updates them accordingly.
    /// </summary>
    public class UIMicrophoneLevelChannelPresenter : MonoBehaviour
    {
        public GameObject micLevelVisualizerPrefab; // Prefab for the microphone level visualizer

        private List<Image> micLevelVisualizerImages = new List<Image>();

        public float LerpSpeed = 10f;

        void Update()
        {
            if (micLevelVisualizerImages.Count != HyperslidesAudioManager.Instance.NumChannels)
                InitMicLevelVisualizers();
            foreach (Image image in micLevelVisualizerImages)
            {
                // Update the fill amount of each image based on the corresponding audio level
                int index = micLevelVisualizerImages.IndexOf(image);
                if (index >= 0 && index < HyperslidesAudioManager.Instance.AudioLevelsPerChannel.Count)
                {
                    image.fillAmount = Mathf.Lerp(image.fillAmount, Mathf.Clamp01(HyperslidesAudioManager.Instance.AudioLevelsPerChannel[index]), Time.deltaTime * LerpSpeed);
                }
            }

        }

        private void OnEnable()
        {
            this.ExecuteNextFrame(() =>
            {
                InitMicLevelVisualizers();
            });
        }

        private void InitMicLevelVisualizers()
        {
            // Clear existing children
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }
            micLevelVisualizerImages.Clear();
            // Create new visualizers based on the number of channels
            for (int i = 0; i < HyperslidesAudioManager.Instance.NumChannels; i++)
            {
                GameObject micLevelVisualizer = Instantiate(micLevelVisualizerPrefab, transform);
                micLevelVisualizer.name = $"MicLevelVisualizer_{i}";
                try 
                {
                    micLevelVisualizerImages.Add(micLevelVisualizer.transform.Find("LevelVisualizerImage").GetComponent<Image>());
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to get Image component from {micLevelVisualizer.name}: {e.Message}");
                }
            }
        }

    }
}
