using UnityEngine;
using UnityEngine.AddressableAssets;

namespace NSYNK.HyperSlides.Runtime
{
    /// <summary>
    /// Class representing an audio event tied to a <see cref="XRSlideElement"/> visibility state.
    /// </summary>
    [System.Serializable]
    public class XRSlideAudioEvent
    {
        public XRSlideElement.VisibilityState triggerState = XRSlideElement.VisibilityState.None;
        public AssetReference audioClip;
        [Range(0f, 1f)]
        public float volume = 1.0f;
        public bool loop = false;

        private AudioSource audioSource;

        /// <summary>
        /// Check if the audio asset reference is valid
        /// </summary>
        /// <returns></returns>
        public bool ValidAsset() => !string.IsNullOrEmpty(audioClip.RuntimeKey.ToString());

        /// <summary>
        /// Preload the audio asset from addressables if not already loaded
        /// </summary>
        public void PreloadAsset()
        {
            if (audioSource != null && audioSource.clip == null)
                LoadAudioClipFromAddressables();
        }

        /// <summary>
        /// Play the audio clip if the audio source and clip are valid
        /// </summary>
        public void Play()
        {
            if (audioSource != null && audioSource.clip != null)
                audioSource.Play();
        }

        /// <summary>
        /// Release the audio asset to free up memory
        /// </summary>
        public void Release()
        {
            if (audioClip != null && audioClip.IsValid())
            {
                audioClip.ReleaseAsset();
                audioSource.clip = null;
            }
        }

        /// <summary>
        /// Load the audio clip from addressables
        /// </summary>
        private void LoadAudioClipFromAddressables()
        {
            if (audioClip == null || !ValidAsset())
            {
                Debug.LogWarning($"AudioClip reference is null or invalid: {audioClip} / {ValidAsset()}");
                return;
            }

            if (audioClip.Asset != null && audioClip.Asset is not AudioClip)
            {
                Debug.LogWarning("AudioClip reference is not a valid AudioClip.");
                return;
            }

            var handle = audioClip.LoadAssetAsync<AudioClip>();
            handle.Completed += (h) =>
            {
                if (h.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                    audioSource.clip = h.Result;
            };
        }

        /// <summary>
        /// Create an audio source on the given parent object if it doesn't already exist
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="parent"></param>
        public void CreateAudioSource(GameObject parent)
        {
            if (audioSource == null)
            {
                LoadAudioClipFromAddressables();

                audioSource = parent.AddComponent<AudioSource>();
                audioSource.volume = volume;
                audioSource.loop = loop;
                audioSource.playOnAwake = false;
            }
        }
    }
}