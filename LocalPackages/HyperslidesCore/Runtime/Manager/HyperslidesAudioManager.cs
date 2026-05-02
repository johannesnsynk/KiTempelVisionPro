using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

/// <summary>
/// This class manage global audio settings
/// Also controls the microphone input
/// </summary>
namespace NSYNK.HyperSlides.Core
{
    public class HyperslidesAudioManager : Singleton<HyperslidesAudioManager>
    {
        [SerializeField, ReadOnly]
        private float masterVolume = 1.0f; // Master volume for all audio listeners

        public bool useMic = false;

        private AudioClip microphoneInput;
        private bool microphoneInitialized = false;
        public float sensitivity = 100f;
        private Coroutine microphoneCoroutine;

        
        private int AudioClipDuration = 100;

        private List<NSYNK.KalmanFilter> kalmanFilters = new List<NSYNK.KalmanFilter>();

        [SerializeField, ReadOnly]
        private List<float> audioLevelsPerChannel = new List<float>();

        public List<float> AudioLevelsPerChannel
        {
            get
            {
                if (audioLevelsPerChannel.Count == 0)
                    audioLevelsPerChannel.Add(0f);
                return audioLevelsPerChannel;
            }
        }

        public int NumChannels { private set; get; } = 0;

        private const string MasterVolumeKey = "HS_MasterVolume";

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            // Load saved master volume or use default
            if (PlayerPrefs.HasKey(MasterVolumeKey))
            {
                float savedVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 1.0f);
                SetMasterVolume(savedVolume);
            }
            else
            {
                SetMasterVolume(masterVolume);
            }
        }

        private void OnEnable()
        {
#if UNITY_IOS
            microphoneInput = Microphone.Start(Microphone.devices[0], true, AudioClipDuration, 44100);
            microphoneInitialized = true;
#endif
        }

        private void OnDisable()
        {
#if UNITY_IOS
            Microphone.End(Microphone.devices[0]);
            microphoneInitialized = false;
#endif
        }

        // Update is called once per frame
        void Update()
        {
#if UNITY_IOS
            GetAudioLevels();
#endif
        }

        public void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume); // Ensure the volume is between 0 and 1
            AudioListener.volume = masterVolume; // Set the global audio listener volume
            PlayerPrefs.SetFloat(MasterVolumeKey, masterVolume);
            PlayerPrefs.Save();
        }

        public float GetMasterVolume()
        {
            return masterVolume;
        }

        private void GetAudioLevels()
        {
            if (useMic && microphoneInitialized && microphoneInput != null && Microphone.IsRecording(Microphone.devices[0])) {

                //get mic volume
                int dec = 128;
                int micPosition = Microphone.GetPosition(Microphone.devices[0]) - (dec + 1); // null means the first microphone

                // Only proceed if the mic position is valid and enough data is available
                if (micPosition > 0 && microphoneInput.samples > dec)
                {
                    float[] waveData = new float[dec];
                    microphoneInput.GetData(waveData, micPosition);
                    NumChannels = microphoneInput.channels;
                    float[] maxChannelLevels = new float[NumChannels];

                    // Getting a peak on the last 128 samples
                    for (int i = 0; i < dec; i++)
                    {
                        float wavePeak = waveData[i] * waveData[i];
                        if (maxChannelLevels[i % NumChannels] < wavePeak)
                        {
                            maxChannelLevels[i % NumChannels] = wavePeak;
                        }
                    }

                    for (int i = 0; i < NumChannels; i++)
                    {
                        // Normalize the peak value to a range of 0 to 1
                        float normalizedLevel = Mathf.Clamp01(Mathf.Sqrt(maxChannelLevels[i]) * sensitivity);
                        if (audioLevelsPerChannel.Count <= i)
                        {
                            audioLevelsPerChannel.Add(normalizedLevel);
                        }
                        else
                        {
                            audioLevelsPerChannel[i] = normalizedLevel;
                        }
                    }
                }
               
            }
        }

        private void OnApplicationPause(bool pause)
        {
#if UNITY_IOS
            if (pause)
            {
                // Stop the microphone when the application is paused
                if (microphoneInitialized && Microphone.IsRecording(Microphone.devices[0]))
                {
                    Microphone.End(Microphone.devices[0]);
                    microphoneInitialized = false;
                    if (microphoneCoroutine != null)
                    {
                        StopCoroutine(microphoneCoroutine);
                        microphoneCoroutine = null;
                    }
                }
            }
            else
            {
                // Restart the microphone when the application resumes
                if (!microphoneInitialized)
                {
                    microphoneInput = Microphone.Start(Microphone.devices[0], true, AudioClipDuration, 44100);
                    microphoneInitialized = true;
                }
            }
#endif
        }
    }
}