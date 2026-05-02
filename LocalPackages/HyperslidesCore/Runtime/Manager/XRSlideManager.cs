using NSYNK.HyperSlides.Network;
using NSYNK.HyperSlides.Runtime;
using NSYNK.HyperSlides.UI;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityMainThreadDispatcher;

namespace NSYNK.HyperSlides.Core
{
    /// <summary>
    /// Handles the updating, animation and triggering of slide relevant data like animations and visual updates.
    /// </summary>
    public class XRSlideManager : Singleton<XRSlideManager>
    {
        public static XRSlideElement.VisibilityState visibilityState = XRSlideElement.VisibilityState.None;
        public List<SlideAssetLoad> slideAssets = new();

#if UNITY_EDITOR
        [Header("Debug Editor information")]
        [Space]
        public XRPresentation inspectorPresentation;
        public XRSlide inspectorSlide;
        public XRSlide.Trigger inspectorTrigger;
#endif

        /// <summary>
        /// Delegate to register, when any slide or trigger changes.
        /// </summary>
        /// <param name="slide">The active slide</param>
        /// <param name="trigger">The active trigger</param>
        public delegate void OnXRSlideChange(XRSlide slide);
        public delegate void OnXRTriggerChange(XRSlide slide, XRSlide.Trigger trigger);
        public delegate void OnDissolveProgress(float progress);
        public delegate void OnSlideTimeChange(float time);
        public delegate void OnGlobalSlideTransition(XRSlideElement.VisibilityState visibilityState);
        public delegate void OnAssetLoadProgress(float progress);
        public delegate void OnAssetLoad(bool started);
        //Use this to trigger any slide change
        public static OnXRSlideChange OnXRSlideChanged;
        public static OnXRSlideChange ONXRSlidePrepare;
        public static OnXRSlideChange ONXRSlidePreload;
        public static OnGlobalSlideTransition OnGlobalSlideTransitioned;
        //Use this to trigger any trigger change
        public static OnXRTriggerChange OnXRTriggerChanged;
        //A simple float return trigger to align visuals to the current progress
        public static OnDissolveProgress OnDissolveInProgress, OnDissolveInProgressNormalized;
        public static OnDissolveProgress OnDissolveOutProgress, OnDissolveOutProgressNormalized;
        // Simple float return trigger to inform about the current time the slide has been active
        public static OnSlideTimeChange OnSlideActiveTimeUpdate;
        //Float return for addressables asset loading
        public static OnAssetLoadProgress OnAssetLoadProgressUpdate;
        public static OnAssetLoad OnAssetLoadStrated;
        //The current presentation that is loaded
        public static XRPresentation CurrentPresentation;
        public static int NetworkSlide = 0;

        //The current active slide
        private static XRSlide currentSlide;
        //The current trigger or the placeholder
        private static XRSlide.Trigger currentTrigger;
        //All slide elements that need to load their addressable

        [SerializeField, ReadOnly]
        //Fading time for max duration easing
        private float dissolveInDuration, dissolveOutDuration = 0;
        [SerializeField, ReadOnly]
        //Current normalized fading progress 0-1-0
        private float dissolveInProgress, dissolveOutProgress, dissolveNormalized = 0;

        [SerializeField, ReadOnly]
        private float slideActiveTime = 0;
        public float SlideActiveTime { 
            private set { 
                slideActiveTime = value;
            }
            get {
                return slideActiveTime; 
            }
        }
        [SerializeField, ReadOnly]
        //Current slide position from 1 to flatten contents list count
        private static int currentSlidePosition = 0;
        public static int CurrentSlidePosition
        {
            get { return currentSlidePosition; }
            set
            {
                if (CurrentPresentation != null && value > CurrentPresentation.contents.Count)
                    value = 1;
                else if (CurrentPresentation != null && value <= 0)
                    value = CurrentPresentation.contents.Count;

                currentSlidePosition = value;
            }
        }

        //The next slide to be shown
        private static XRSlide nextSlide, preloadedNextSlide, preloadedPreviousSlide = null;
        //The next trigger if available
        private static XRSlide.Trigger nextTrigger = null;
        //Placeholder trigger to avoid null error for now
        private static XRSlide.Trigger emptyTrigger = new XRSlide.Trigger("EmptyTrigger", "EmptyTrigger");

        private void Start() => Reset();

        private void OnEnable()
        {
            XRNetworkManager.onNetworkSlideUpdate += NetworkSlideUpdate;
            //XRDataManager.onDataReceived += LoadDefaultPresentation;
        }

        private void OnDisable()
        {
            XRNetworkManager.onNetworkSlideUpdate -= NetworkSlideUpdate;
            //XRDataManager.onDataReceived -= LoadDefaultPresentation;
        }

        /// <summary>
        /// Reset all states to no presentation
        /// </summary>
        public void Reset()
        {
            nextSlide = null;
            nextTrigger = null;
            currentSlide = null;
            currentTrigger = null;
            CurrentPresentation = null;

            OnXRSlideChanged?.Invoke(null);
        }

        /// <summary>
        /// The slide update coming from network to load and show the active slide the moderator has chosen
        /// </summary>
        /// <param name="networkObject"></param>
        private void NetworkSlideUpdate(XRNetworkObjects.XRSessionState networkObject)
        {
            Dispatcher.Enqueue(() =>
            {
                XRPresentation newPresentation = XRDataManager.allPresentations.Find(p => p.id == networkObject.presentationId);

                if (networkObject.tickRate > 0)
                    RuntimeHandler.Settings.updateRate = networkObject.tickRate;
                else
                    RuntimeHandler.Settings.updateRate = RuntimeHandler.Settings.backupUpdateRate;

                RuntimeHandler.Settings.showNameTags = networkObject.showNameTags || RuntimeHandler.Settings.showNameTags;

                if (newPresentation != null)
                {
                    if (CurrentPresentation == null || (CurrentPresentation.id != networkObject.presentationId))
                        SetPresentation(newPresentation, networkObject.presentationContentIndex);
                    else
                        SetSlide(networkObject.presentationContentIndex);
                }
                else
                    Debug.Log($"Could not find presentation: {networkObject.presentationId}");
            });
        }

        private void Update()
        {
            if (XRNetworkManager.localPlayer && CurrentPresentation != null)
            {
                UpdateSlideActiveTime();
            }
        }

        /// <summary>
        /// Go to the next slide by simply incrementing
        /// </summary>
        public void NextSlide()
        {
            if (!XRNetworkManager.localPlayer || CurrentPresentation == null)
                return;

            if (XRUIManager.TimerRunning())
                return;

            StopAllCoroutines();

            if (DeviceInfo.Role >= XRPlayer.Role.Moderator ||
                DeviceInfo.Role == XRPlayer.Role.Simulation)
            {
                //CurrentSlidePosition++;
                //Debug.Log("NEXT: " + CurrentSlidePosition);
                XRNetworkManager.Instance.SendSlideNextUpdate();
            }

        }

        /// <summary>
        /// Go the previous slide by decrementing
        /// </summary>
        public void PreviousSlide()
        {
            if (!XRNetworkManager.localPlayer || CurrentPresentation == null)
                return;

            if (XRUIManager.TimerRunning())
                return;

            StopAllCoroutines();

            if (DeviceInfo.Role >= XRPlayer.Role.Moderator ||
                DeviceInfo.Role == XRPlayer.Role.Simulation)
            {
                //CurrentSlidePosition--;
                XRNetworkManager.Instance.SendSlidePrevUpdate();
            }
        }

        /// <summary>
        /// Set the next slide and update animation and triggers
        /// </summary>
        /// <param name="position">The next absolute or relative position</param>
        public void SetSlide(int position)
        {
            CurrentSlidePosition = position;
            NetworkSlide = position;

            Instance.GetNextContent();

            if (currentSlide == nextSlide && nextTrigger != emptyTrigger)
            {
                if (currentTrigger != nextTrigger)
                {
                    currentTrigger = nextTrigger;
                    OnXRTriggerChanged?.Invoke(currentSlide, currentTrigger);
                }
            }
            else
            {
                if (currentSlide == null)
                    currentSlide = nextSlide;

                StopAllCoroutines();
                StartCoroutine(Instance.ContentFadeOut());
            }
        }

        /// <summary>
        /// Get the next content from <see cref="XRPresentation.contents"/>
        /// </summary>
        public void GetNextContent()
        {
            slideAssets.Clear();

            int slideCount = CurrentSlidePosition;

            foreach (IContent content in CurrentPresentation.contents)
            {
                int contentIndex = CurrentPresentation.contents.IndexOf(content);

                if (contentIndex < slideCount)
                {
                    if (content is XRSlide)
                    {
                        nextTrigger = emptyTrigger;
                        nextSlide = content as XRSlide;
                    }

                    if (content is XRSlide.Trigger)
                    {
                        nextTrigger = content as XRSlide.Trigger;
                    }
                }
            }

#if UNITY_EDITOR
            inspectorSlide = nextSlide;
            inspectorTrigger = nextTrigger;
#endif

            if (nextSlide != null)
            {
                int countIndex = CurrentPresentation.slides.Count();
                int contentIndex = CurrentPresentation.slides.IndexOf(nextSlide);

                int preloadPrevSlideInt = contentIndex - 1 > 0 ? contentIndex - 1 : countIndex - 1;
                int preloadNextSlideInt = contentIndex + 1 < countIndex ? contentIndex + 1 : 0;

                preloadedPreviousSlide = CurrentPresentation.slides[preloadPrevSlideInt];
                preloadedNextSlide = CurrentPresentation.slides[preloadNextSlideInt];
            }

            //Debug.Log(preloadedPreviousSlide?.displayName + " < " + nextSlide.displayName + " > " + preloadedNextSlide.displayName);
        }

        public void IsAnimationAllowed(bool allowed) => Debug.Log("Animation allowed: " + allowed, this);
        /// <summary>
        /// Set the dissolve in duration from all needed slidelements
        /// </summary>
        /// <param name="duration">The new duration, which will be compared to the latest in length</param>
        public static void SetDissolveInDuration(float duration) => Instance.dissolveInDuration = duration > Instance.dissolveInDuration ? duration : Instance.dissolveInDuration;
        /// <summary>
        /// Set the dissolve out duration from all needed slidelements
        /// </summary>
        /// <param name="duratin">The new duration, which will be compared to the latest in length</param>
        public static void SetDissolveOutDuration(float duratin) => Instance.dissolveOutDuration = duratin > Instance.dissolveOutDuration ? duratin : Instance.dissolveOutDuration;
        public static XRSlide.Trigger GetCurrentTrigger() => currentTrigger;
        public static XRPresentation GetPresentationData() => CurrentPresentation;

        /// <summary>
        /// Get the current slide, if no slide is set, return a new instance with the current presentation id and the count of contents
        /// This is used to avoid null references in the UI
        /// </summary>
        /// <returns></returns>
        public static XRSlide GetCurrentSlide()
        {
            if (currentSlide)
                return currentSlide;
            else if (CurrentPresentation != null)
                return new XRSlide(CurrentPresentation.id, CurrentPresentation.contents == null ? 0 : CurrentPresentation.contents.Count);
            else
                return new XRSlide("NoPresentation", 0);
        }

        /// <summary>
        /// Get the next slide, if no slide is set, return a new instance with the current presentation id and the count of contents
        /// This is used to avoid null references in the UI
        /// </summary>
        /// <returns></returns>
        public static XRSlide GetNextSlide()
        {
            XRSlide nextSlide = null;

            if (CurrentPresentation.contents == null || CurrentPresentation.contents.Count <= 1)
                return new XRSlide(CurrentPresentation.id, CurrentPresentation.contents == null ? 0 : CurrentPresentation.contents.Count);

            if (CurrentSlidePosition == CurrentPresentation.contents.Count)
                return CurrentPresentation.contents[0] as XRSlide;

            for (int contentIndex = CurrentSlidePosition; contentIndex < CurrentPresentation.contents.Count; contentIndex++)
            {
                if (!nextSlide && CurrentPresentation.contents[contentIndex] is XRSlide)
                    nextSlide = CurrentPresentation.contents[contentIndex] as XRSlide;
            }

            return nextSlide;
        }

        /// <summary>
        /// Update the presentation and restart from slide 0
        /// </summary>
        /// <param name="presentation"></param>
        public static void SetPresentation(XRPresentation presentation, int currentSlide = 0)
        {
            CurrentPresentation = presentation;
            CurrentPresentation.GetContents();

            Instance.SetSlide(currentSlide);

#if UNITY_EDITOR
            Instance.inspectorPresentation = presentation;
#endif
        }

        /// <summary>
        /// Register a <see cref="XRSlideElement"/> to the current elements, that need to update their addressables and visibility
        /// </summary>
        /// <param name="slideElement">The slideelement reference</param>
        /// <param name="loadAsset">Should this slide element be visible in next step </param>
        public static void RegisterSlideElement(XRSlideElement slideElement, bool loadAsset)
        {
            SlideAssetLoad foundAssetLoad = Instance.slideAssets.Find(s => s.slideElement == slideElement);

            if (foundAssetLoad != null)
                foundAssetLoad.shouldLoadAsset = loadAsset;
            else
                Instance.slideAssets.Add(new(slideElement, loadAsset));
        }

        /// <summary>
        /// Updatethe slide active time
        /// </summary>
        private void UpdateSlideActiveTime()
        {
            if (visibilityState == XRSlideElement.VisibilityState.FullyVisible ||
                visibilityState == XRSlideElement.VisibilityState.DissolvingIn)
            {
                if (slideActiveTime < float.MaxValue - 10)
                    SlideActiveTime += Time.deltaTime;
                else
                    SlideActiveTime = 0;
                OnSlideActiveTimeUpdate?.Invoke(SlideActiveTime);
            }
            else
            {
                SlideActiveTime = 0;
            }
        }

        /// <summary>
        /// Fade out current content before fading in next, if it is a slide change
        /// </summary>
        /// <returns></returns>
        private IEnumerator ContentFadeOut()
        {
            dissolveInDuration = .1f;
            dissolveOutDuration = .1f;

            //Prepare all contents
            ONXRSlidePrepare?.Invoke(nextSlide);

            ONXRSlidePreload?.Invoke(preloadedNextSlide);
            //ONXRSlidePreload?.Invoke(preloadedPreviousSlide);

            dissolveOutProgress =
                visibilityState == XRSlideElement.VisibilityState.DissolvingIn ?
                dissolveOutDuration * (1 - dissolveNormalized) :
                dissolveOutDuration * dissolveNormalized;

            visibilityState = XRSlideElement.VisibilityState.DissolvingOut;
            OnGlobalSlideTransitioned?.Invoke(visibilityState);

            //dissolveOutProgress = dissolveOutDuration * dissolveNormalized;

            XRUIManager.StartTimer(XRUIManager.dissolveTimer, dissolveInDuration + dissolveOutDuration);

            //Fade out previous
            while (dissolveOutProgress < dissolveOutDuration)
            {
                dissolveOutProgress += Time.deltaTime;

                OnDissolveOutProgress?.Invoke(dissolveOutProgress);
                OnDissolveOutProgressNormalized?.Invoke(dissolveOutProgress / dissolveOutDuration);

                dissolveNormalized = dissolveOutProgress / dissolveOutDuration;
                yield return null;
            }

            OnDissolveOutProgress?.Invoke(dissolveOutDuration);
            visibilityState = XRSlideElement.VisibilityState.FullyHidden;
            OnGlobalSlideTransitioned?.Invoke(visibilityState);

            dissolveOutProgress = 0;

            //Sort by load asset and unload first, then load new assets
            if (slideAssets.Count > 0)
            {
                XRUIManager.Instance.LoadingActionProgress = 0;
                XRUIManager.Instance.LoadingActionInProgress = true;
                OnAssetLoadStrated?.Invoke(true);
                Debug.Log($"[XRSlideManager] Preparing addressables started");
                int processedAssets = 0;
                slideAssets.OrderBy(sA => !sA.shouldLoadAsset);

                foreach (SlideAssetLoad assetLoad in slideAssets)
                {
                    if (!assetLoad.shouldLoadAsset)
                    {
                        assetLoad.slideElement.UnloadAssetReference();
                        while (!assetLoad.IsDone())
                            yield return null;
                        processedAssets++;
                        XRUIManager.Instance.LoadingActionProgress = Math.Clamp((processedAssets / (float)slideAssets.Count), 0, 1);
                        OnAssetLoadProgressUpdate?.Invoke(Math.Clamp((processedAssets / (float)slideAssets.Count), 0, 1));
                        Debug.Log($"[XRSlideManager] Processed {processedAssets}/{slideAssets.Count} slide assets (unloading); Progress: {processedAssets / (float)slideAssets.Count}");
                    }
                }

                foreach (SlideAssetLoad assetLoad in slideAssets)
                {
                    if (assetLoad.shouldLoadAsset)
                    {
                        assetLoad.slideElement.LoadAssetReference();
                        while (!assetLoad.IsDone())
                            yield return null;
                        processedAssets++;
                        XRUIManager.Instance.LoadingActionProgress = Math.Clamp((processedAssets / (float)slideAssets.Count), 0, 1);
                        OnAssetLoadProgressUpdate?.Invoke(Math.Clamp((processedAssets / (float)slideAssets.Count), 0, 1));
                        Debug.Log($"[XRSlideManager] Processed {processedAssets}/{slideAssets.Count} slide assets (loading); Progress: {processedAssets / (float)slideAssets.Count}");
                    }
                }

                XRUIManager.Instance.LoadingActionProgress = 1;
                XRUIManager.Instance.LoadingActionInProgress = false;
                OnAssetLoadStrated?.Invoke(false);
                // UIButton.onUpdateUI?.Invoke();
                Debug.Log($"[XRSlideManager] Preparing addressables ended");
            }

            // This unload is causing problems with certain assets. Unload should in theory still be happening but keep an eye on it
            //Resources.UnloadUnusedAssets();
            yield return new WaitForEndOfFrame();

            yield return ContentFadeIn();
        }

        /// <summary>
        /// Fade in new content if the slide differs. If its the same slide, we check against the next trigger and set the ease time to 0
        /// </summary>
        /// <returns></returns>
        private IEnumerator ContentFadeIn()
        {
            OnXRSlideChanged?.Invoke(nextSlide);

            visibilityState = XRSlideElement.VisibilityState.DissolvingIn;
            OnGlobalSlideTransitioned?.Invoke(visibilityState);

            if (currentSlide != null && nextSlide != null &&
                currentSlide.id != nextSlide.id)
                currentSlide = nextSlide;

            currentTrigger = nextTrigger;

            OnXRTriggerChanged?.Invoke(currentSlide, currentTrigger);
            XRCameraManager.Instance.UpdateXROriginPosition(dissolveInDuration + dissolveOutDuration);

            dissolveInProgress = 0;

            //Fade in next
            while (dissolveInProgress < dissolveInDuration)
            {
                dissolveInProgress += Time.deltaTime;

                OnDissolveInProgress?.Invoke(dissolveInProgress);
                OnDissolveInProgressNormalized?.Invoke(dissolveInProgress / dissolveInDuration);

                dissolveNormalized = dissolveInProgress / dissolveInDuration;
                yield return null;
            }

            dissolveInProgress = dissolveInDuration;

            OnDissolveInProgress?.Invoke(dissolveInDuration);
            OnXRSlideChanged?.Invoke(currentSlide);

            visibilityState = XRSlideElement.VisibilityState.FullyVisible;
            OnGlobalSlideTransitioned?.Invoke(visibilityState);

            dissolveNormalized = 0;
        }

        /// <summary>
        /// A slide asset wrapper, that holds the <see cref="XRSlideElement"/> and the current state of its loading/unloading
        /// </summary>
        [Serializable]
        public class SlideAssetLoad
        {
            public XRSlideElement slideElement;
            public bool shouldLoadAsset = false;
            public bool assetProcessed = false;

            public SlideAssetLoad(XRSlideElement s, bool lA)
            {
                slideElement = s;
                shouldLoadAsset = lA;
            }

            public bool IsDone()
            {
                assetProcessed = (!shouldLoadAsset && !slideElement.AssetActive) ||
                    (shouldLoadAsset && slideElement.AssetActive);
                return assetProcessed;
            }
        }
    }
}