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
        public bool UseMic = false;
        public float Sensitivity = 100f;
        public int NumChannels { private set; get; } = 0;

        [SerializeField, ReadOnly]
        private float masterVolume = 1.0f; // Master volume for all audio listeners
        [SerializeField, ReadOnly]
        private List<float> audioLevelsPerChannel = new List<float>();
        private const string MasterVolumeKey = "HS_MasterVolume";

        private AudioClip microphoneInput;
        private bool microphoneInitialized = false;

#if UNITY_IOS && !UNITY_EDITOR
        private Coroutine microphoneCoroutine;
        private int audioClipDuration = 100;
#endif

        public List<float> AudioLevelsPerChannel
        {
            get
            {
                if (audioLevelsPerChannel.Count == 0)
                    audioLevelsPerChannel.Add(0f);
                return audioLevelsPerChannel;
            }
        }

        private void Start()
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

#if UNITY_IOS && !UNITY_EDITOR
        private void OnEnable()
        {
            microphoneInput = Microphone.Start(Microphone.devices[0], true, audioClipDuration, 44100);
            microphoneInitialized = true;
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        private void OnDisable()
        {
            if (Microphone.devices != null && Microphone.devices.Length > 0 && Microphone.IsRecording(Microphone.devices[0]))
                Microphone.End(Microphone.devices[0]);
            microphoneInitialized = false;
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        private void Update()
        {
            GetAudioLevels();
        }
#endif

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
            if (UseMic && microphoneInitialized && microphoneInput != null && Microphone.IsRecording(Microphone.devices[0]))
            {

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
                        float normalizedLevel = Mathf.Clamp01(Mathf.Sqrt(maxChannelLevels[i]) * Sensitivity);
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

#if UNITY_IOS && !UNITY_EDITOR
        private void OnApplicationPause(bool pause)
        {
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
                    microphoneInput = Microphone.Start(Microphone.devices[0], true, audioClipDuration, 44100);
                    microphoneInitialized = true;
                }
            }
        }
#endif
    }
}