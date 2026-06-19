using UnityEngine;
using Unity.PolySpatial;
using UnityEngine.Video;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections;

namespace NSYNK.HyperSlides.Runtime
{
    /// <summary>
    /// XRVideoPlayer decides what videoplayer component to use based on if we are in UNITY_VISIONOS
    /// </summary>
    public class XRVideoPlayerV2 : XRSlideDissolveComponent
    {
        public delegate void VideoPlayStateChange(bool isPlaying);
        public VideoPlayStateChange OnVideoPlayStateChanged;

        [Header("Video Player Relations"), SerializeField]
        [Tooltip("The mesh renderer that this video clip will be applied to.")]
        MeshRenderer m_TargetMaterialRenderer;
        public MeshRenderer TargetMaterialRenderer
        {
            get => m_TargetMaterialRenderer;
            set
            {
                m_TargetMaterialRenderer = value;
                InitVideoPlayer();
            }
        }

        [SerializeField]
        [Tooltip("Video clip to be used.")]
        VideoClip m_Clip;
        public VideoClip Clip
        {
            get => m_Clip;
            set
            {
                m_Clip = value;
                InitVideoPlayer();
            }
        }

        [Header("Video Player Settings"), SerializeField]
        [Tooltip("Whether video player should loop this clip.")]
        bool m_Loop = true;
        public bool Loop
        {
            get => m_Loop;
            set
            {
                m_Loop = value;
                InitVideoPlayer();
            }
        }
        [SerializeField]
        [Tooltip("Whether video player should close after clip.")]
        bool m_CloseAutomatically = false;
        public bool CloseAutomatically
        {
            get => m_CloseAutomatically;
            set
            {
                m_CloseAutomatically = value;
                InitVideoPlayer();
            }
        }

        [SerializeField]
        [Tooltip("Whether video clip should play on awake.")]
        bool m_PlayOnAwake = true;
        public bool PlayOnAwake
        {
            get => m_PlayOnAwake;
            set
            {
                m_PlayOnAwake = value;
                InitVideoPlayer();
            }
        }

        [SerializeField]
        [Tooltip("Mute status of first track on the video clip. Multiple tracks currently not supported")]
        bool m_Mute = false;

        [SerializeField]
        [Tooltip("If the video should start only after the component is fully dissolved in or while dissolving. Also affect out dissolve")]
        bool m_PlayVideoDuringDissolve = true;

        /// <summary>
        /// for knowing if already closed or closing before dissolving or if video ends while dissolving
        /// </summary>
        private bool isClosing = false;

        [Header("Animation values")]
        public float CloseDuration = 1f;

        /// <summary>
        /// Used for non visionos devices
        /// </summary>
        private VideoPlayer videoPlayer;

        /// <summary>
        /// only used for visionos
        /// </summary>
        private VisionOSVideoComponent videoPlayerVOS;
        private RenderTexture renderTexture;

        [SerializeField]
        private float m_StartScale = 0.0f;

        [SerializeField, ReadOnly]
        [Tooltip("Is set to the scale of this gameobject")]
        private float m_EndScale;

        [SerializeField]
        private Easing.Ease Ease = Easing.Ease.Linear;
        [SerializeField]
        private Vector2 xScaleRange = new Vector2(0f, 1f);
        [SerializeField]
        private Vector2 yScaleRange = new Vector2(0f, 1f);
        [SerializeField]
        private Vector2 zScaleRange = new Vector2(0f, 0f);

        protected override void Awake()
        {
            m_EndScale = this.transform.localScale.x;
#if UNITY_VISIONOS
            videoPlayerVOS = this.GetComponentInChildren<VisionOSVideoComponent>();
            if (videoPlayerVOS == null)
            {
                //Debug.Log($"[XRVideoPlayer] No VideoPlayer component found on {this.gameObject.name}, creating a new one!");
                videoPlayerVOS = this.transform.GetChild(0).gameObject.AddComponent<VisionOSVideoComponent>();
            }
#else
            videoPlayer = this.GetComponentInChildren<VideoPlayer>();
            if (videoPlayer == null)
            {
                //Debug.Log($"[XRVideoPlayer] No VideoPlayer component found on {this.gameObject.name}, creating a new one!");
                videoPlayer = this.transform.GetChild(0).gameObject.AddComponent<VideoPlayer>();
            }
#endif
            InitVideoPlayer();
        }

        private void OnValidate()
        {
            m_EndScale = this.transform.localScale.x;
        }

        /// <summary>
        /// initalize the video player component with this components values
        /// also create material for mesh renderer when not visionos
        /// </summary>
        private void InitVideoPlayer()
        {
#if UNITY_VISIONOS
            if (videoPlayerVOS != null && m_Clip)
            {
                //videoPlayerVOS.Stop();
                videoPlayerVOS.Clip = m_Clip;
                videoPlayerVOS.IsLooping = m_Loop;
                videoPlayerVOS.PlayOnAwake = m_PlayOnAwake;
                videoPlayerVOS.SetDirectAudioMute(0, m_Mute);
                videoPlayerVOS.TargetMaterialRenderer = m_TargetMaterialRenderer;
            }
#else
            if (videoPlayer != null && m_Clip != null)
            {
                videoPlayer.clip = m_Clip;
                videoPlayer.isLooping = m_Loop;
                videoPlayer.playOnAwake = m_PlayOnAwake;
                for (int i = 0; i < videoPlayer.audioTrackCount; i++)
                {
                    videoPlayer.SetDirectAudioMute((ushort)i, m_Mute);
                }
                videoPlayer.renderMode = VideoRenderMode.RenderTexture;
                videoPlayer.targetTexture = new RenderTexture((int)m_Clip.width, (int)m_Clip.height, 0);
                if (TargetMaterialRenderer.materials[0].HasTexture("_BaseMap"))
                {
                    TargetMaterialRenderer.materials[0].SetTexture("_BaseMap", videoPlayer.targetTexture);
                }
            }
#endif
        }

        private async Task PlayVideo()
        {
            await Task.Delay(10);
            Play();
        }

        public void Play()
        {
#if UNITY_VISIONOS
            if (videoPlayerVOS != null)
            {
                //Debug.Log("Playing Video.");
                videoPlayerVOS.Play();
                //if (videoPlayerVOS.GetState() != VisionOSVideoComponent.PlayerState.IsPlaying)
            }
#else
            if (videoPlayer != null)
            {
                if (!videoPlayer.isPlaying)
                    videoPlayer.Play();
            }

#endif
            OnVideoPlayStateChanged?.Invoke(true);
            if (CloseAutomatically && isActiveAndEnabled)
            {
                StartCoroutine(QueueClose((float)m_Clip.length));
                //StartCoroutine(QueueClose(3f));
            }
        }

        public void Stop()
        {
#if UNITY_VISIONOS
            if (videoPlayerVOS != null)
            {
                //if (videoPlayerVOS.GetState() == VisionOSVideoComponent.PlayerState.IsPlaying)
                //Debug.Log("It's time to stop!");
                videoPlayerVOS.Stop();
            }
#else
            if (videoPlayer != null)
            {
                if (videoPlayer.isPlaying)
                    videoPlayer.Stop();
            }
#endif
            if (isActiveAndEnabled)
                StartCoroutine(Close());
        }

        private void VideoPlayerFinishedCallback(VideoPlayer vp)
        {
            Stop();
        }

        [SerializeField]
        public bool m_AutoResizeMesh;

        private void Start()
        {
            if (PlayOnAwake)
                SetMeshToResolutionAspectRatio();
        }

        private Vector3 videoPlayerStartScale = Vector3.zero;

        private void SetMeshToResolutionAspectRatio()
        {
            if (m_AutoResizeMesh)
            {
                if (!Clip)
                    return;

                if (Mathf.Approximately(videoPlayerStartScale.magnitude, 0))
                    videoPlayerStartScale = TargetMaterialRenderer.transform.localScale;

                float aspectRatio = (float)Clip.width / (float)Clip.height;
                aspectRatio *= videoPlayerStartScale.z;
                TargetMaterialRenderer.transform.localScale = new Vector3(aspectRatio, videoPlayerStartScale.y, videoPlayerStartScale.z);
            }
        }

        private void Update()
        {
#if UNITY_VISIONOS
            VisionOSVideoComponent.PlayerState state = videoPlayerVOS.GetState();
            if (visibilityState == XRSlideElement.VisibilityState.FullyVisible && state != VisionOSVideoComponent.PlayerState.IsPlaying)
            {
                Stop();
            }
#endif
        }

        IEnumerator QueueClose(float waitTime)
        {
            yield return new WaitForSeconds(waitTime);

            //TODO temporary fix because we cant access visionos video states for now. GetState() always returns is playing.
            //yield return new WaitForSeconds(1.5f);

            Stop();
        }

        IEnumerator Close()
        {
            if (visibilityState == XRSlideElement.VisibilityState.FullyVisible && !isClosing)
            {
                float elapsedTime = 0f;
                isClosing = true;

                while (elapsedTime < CloseDuration)
                {
                    elapsedTime += Time.deltaTime;
                    //Debug.Log($"{elapsedTime}: {this.transform.localScale}, {CloseDuration}");
                    this.transform.localScale = Vector3.Lerp(new Vector3(m_EndScale, m_EndScale, m_EndScale), new Vector3(0, 0, 0), Mathf.Clamp(elapsedTime / CloseDuration, 0f, 1f));
                    UpdateScale(1f - Mathf.Clamp(elapsedTime / CloseDuration, 0f, 1f));
                    yield return null;
                }
            }
        }

        public override void OnDissolveChanged(float dissolveNormalized, float dissolveInOut)
        {
            if (!isClosing)
                //this.transform.localScale = Vector3.Lerp(new Vector3(m_StartScale, m_StartScale, m_StartScale), new Vector3(m_EndScale, m_EndScale, m_EndScale), dissolveNormalized);
                UpdateScale(dissolveNormalized);
        }

        private void UpdateScale(float alpha)
        {
            float x = Easing.EaseIt(Ease, m_StartScale, m_EndScale, xScaleRange.x != xScaleRange.y ? Extensions.RemapClamped(alpha, xScaleRange.x, xScaleRange.y, 0f, 1f) : 1f);
            float y = Easing.EaseIt(Ease, m_StartScale, m_EndScale, yScaleRange.x != yScaleRange.y ? Extensions.RemapClamped(alpha, yScaleRange.x, yScaleRange.y, 0f, 1f) : 1f);
            float z = Easing.EaseIt(Ease, m_StartScale, m_EndScale, zScaleRange.x != zScaleRange.y ? Extensions.RemapClamped(alpha, zScaleRange.x, zScaleRange.y, 0f, 1f) : 1f);
            //Debug.Log($"{x}, {y}, {z}");
            this.transform.localScale = new Vector3(x, y, z);
        }

        protected override void OnDisable()
        {
            Stop();
            StopAllCoroutines();
            base.OnDisable();
        }

        public async override void OnVisibilityStateChanged(XRSlideElement.VisibilityState state)
        {
            //Debug.Log($"Hello? {state}");
            // Start or stop video before or after dissolve based on the bool
            switch (state)
            {
                case XRSlideElement.VisibilityState.DissolvingIn:
                    if (m_PlayVideoDuringDissolve) await PlayVideo();
                    isClosing = false;
                    break;
                case XRSlideElement.VisibilityState.DissolvingOut:
                    if (!m_PlayVideoDuringDissolve) this.Stop();
                    break;
                case XRSlideElement.VisibilityState.FullyVisible:
                    this.Play();
                    isClosing = false;
                    break;
                case XRSlideElement.VisibilityState.FullyHidden:
                    this.Stop();
                    isClosing = false;
                    break;
                default:
                    this.Stop();
                    isClosing = false;
                    break;
            }
        }
    }
}