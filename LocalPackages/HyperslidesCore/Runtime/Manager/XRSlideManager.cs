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
        /// <summary>Event that is triggered when the slide changes</summary>
        public event Action<XRSlide> OnXRSlideChanged;
        /// <summary>Event that is triggered when the slide is prepared, before any addressables are loaded</summary>
        public event Action<XRSlide> OnXRSlidePrepare;
        /// <summary>Event that is triggered when the slide is preloaded, before any transition starts</summary>
        public event Action<XRSlide> OnXRSlidePreload;
        /// <summary>Event that is triggered when the global slide visibility state changes</summary>
        public event Action<XRSlideElement.VisibilityState> OnGlobalSlideTransitioned;
        /// <summary>Event that is triggered when the current trigger changes</summary>
        public event Action<XRSlide, XRSlide.Trigger> OnXRTriggerChanged;
        /// <summary>Event that is triggered on dissolving in progress</summary>
        public event Action<float> OnDissolveInProgress, OnDissolveInProgressNormalized;
        /// <summary>Event that is triggered on dissolving out progress</summary>
        public event Action<float> OnDissolveOutProgress, OnDissolveOutProgressNormalized;
        /// <summary>Float return for the current active slide time</summary>
        public event Action<float> OnSlideActiveTimeUpdate;
        /// <summary>Event that is triggered on asset load progress update</summary>
        public event Action<float> OnAssetLoadProgressUpdate;
        /// <summary>Event that is triggered when asset loading starts</summary>
        public event Action<bool> OnAssetLoadStarted;

        /// <summary>The current global visibility state of all slide elements</summary>
        public XRSlideElement.VisibilityState CurrentVisibilityState { get; private set; } = XRSlideElement.VisibilityState.None;
        /// <summary>The current active presentation</summary>
        public XRPresentation CurrentPresentation { get; private set; } = null;
        /// <summary>The current active slide index</summary>
        public int NetworkSlide { get; private set; } = 0;
        /// <summary>Current slide active time</summary>
        public float SlideActiveTime { get; private set; } = 0;

        /// <summary>Should the manager call Resources.UnloadUnusedAssets after each slide transition. </summary>
        public bool DoUnloadUnusedAssets = true;

        /// <summary>List of all registered slide assets for loading/unloading</summary>
        private List<SlideAssetLoad> slideAssets = new();
        /// <summary>The current active slide</summary>
        private XRSlide currentSlide;
        //The current trigger or the placeholder
        private XRSlide.Trigger currentTrigger;
        /// <summary>Current dissolve in and out duration</summary>
        [SerializeField, ReadOnly]
        private float dissolveInDuration, dissolveOutDuration = 0;
        /// <summary>Current dissolve in and out progress</summary>
        [SerializeField, ReadOnly]
        private float dissolveInProgress, dissolveOutProgress;
        /// <summary>Current dissolve normalized value from 0 to 1</summary>
        [SerializeField, ReadOnly, Range(0, 1)]
        private float dissolveNormalized = 0;

        //Current slide position from 1 to flatten contents list count
        private int currentSlidePosition = 0;
        public int CurrentSlidePosition
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

        ///Is there a loading action in progress
        public static bool LoadingActionInProgress => loadingActionProgress < 1f && loadingActionProgress > 0f;

        //The current progress of the loading action
        private static float loadingActionProgress = 0;
        public static float LoadingActionProgress
        {
            get => loadingActionProgress;
            set
            {
                loadingActionProgress = value;
                UIButton.onUpdateUI?.Invoke();
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
            XRNetworkManager.Instance.OnNetworkSlideUpdate += NetworkSlideUpdate;
            //XRDataManager.onDataReceived += LoadDefaultPresentation;
        }

        private void OnDisable()
        {
            XRNetworkManager.Instance.OnNetworkSlideUpdate -= NetworkSlideUpdate;
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
                XRPresentation newPresentation = XRDataManager.Instance.AllPresentations.Find(p => p.id == networkObject.presentationId);

                HyperSlidesStateManager.Instance.Settings.showNameTags = networkObject.showNameTags || HyperSlidesStateManager.Instance.Settings.showNameTags;

                if (newPresentation != null)
                {
                    if (CurrentPresentation == null || (CurrentPresentation.id != networkObject.presentationId))
                        SetPresentation(newPresentation, networkObject.presentationContentIndex);
                    else if(CurrentSlidePosition != networkObject.presentationContentIndex)
                        SetSlide(networkObject.presentationContentIndex);
                }
                else
                    Debug.Log($"Could not find presentation: {networkObject.presentationId}");
            });
        }

        private void Update()
        {
            if (CurrentPresentation != null)
                UpdateSlideActiveTime();
        }

        /// <summary>
        /// Go to the next slide by simply incrementing
        /// </summary>
        public void NextSlide()
        {
            if (!XRNetworkManager.Instance.LocalPlayer || CurrentPresentation == null)
                return;

            if (XRUIManager.TimerRunning())
                return;

            StopAllCoroutines();

            if (DeviceInfo.Instance.Role >= XRPlayer.Role.Moderator ||
                DeviceInfo.Instance.Role == XRPlayer.Role.Simulation)
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
            if (!XRNetworkManager.Instance.LocalPlayer || CurrentPresentation == null)
                return;

            if (XRUIManager.TimerRunning())
                return;

            StopAllCoroutines();

            if (DeviceInfo.Instance.Role >= XRPlayer.Role.Moderator ||
                DeviceInfo.Instance.Role == XRPlayer.Role.Simulation)
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
        public void SetDissolveInDuration(float duration) => dissolveInDuration = duration > dissolveInDuration ? duration : dissolveInDuration;
        /// <summary>
        /// Set the dissolve out duration from all needed slidelements
        /// </summary>
        /// <param name="duratin">The new duration, which will be compared to the latest in length</param>
        public void SetDissolveOutDuration(float duratin) => dissolveOutDuration = duratin > dissolveOutDuration ? duratin : dissolveOutDuration;
        public XRSlide.Trigger GetCurrentTrigger() => currentTrigger;
        public XRPresentation GetPresentationData() => CurrentPresentation;

        /// <summary>
        /// Get the current slide, if no slide is set, return a new instance with the current presentation id and the count of contents
        /// This is used to avoid null references in the UI
        /// </summary>
        /// <returns></returns>
        public XRSlide GetCurrentSlide()
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
        public XRSlide GetNextSlide()
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
        public void SetPresentation(XRPresentation presentation, int currentSlide = 0)
        {
            CurrentPresentation = presentation;
            CurrentPresentation.GetContents();

            Instance.SetSlide(currentSlide);
        }

        /// <summary>
        /// Register a <see cref="XRSlideElement"/> to the current elements, that need to update their addressables and visibility
        /// </summary>
        /// <param name="slideElement">The slideelement reference</param>
        /// <param name="loadAsset">Should this slide element be visible in next step </param>
        public void RegisterSlideElement(XRSlideElement slideElement, bool loadAsset)
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
            if (CurrentVisibilityState == XRSlideElement.VisibilityState.FullyVisible ||
                CurrentVisibilityState == XRSlideElement.VisibilityState.DissolvingIn)
            {
                if (SlideActiveTime < float.MaxValue - 10)
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
        /// Update the global slide state for all listeners
        /// </summary>
        /// <param name="state"></param>
        private void UpdateGlobalSlideState(XRSlideElement.VisibilityState state)
        {
            CurrentVisibilityState = state;
            OnGlobalSlideTransitioned?.Invoke(state);
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
            OnXRSlidePrepare?.Invoke(nextSlide);

            //Preload next and optionally previous slide assets
            OnXRSlidePreload?.Invoke(preloadedNextSlide);
            //ONXRSlidePreload?.Invoke(preloadedPreviousSlide);

            dissolveOutProgress =
                CurrentVisibilityState == XRSlideElement.VisibilityState.DissolvingIn ?
                dissolveOutDuration * (1 - dissolveNormalized) :
                dissolveOutDuration * dissolveNormalized;

            UpdateGlobalSlideState(XRSlideElement.VisibilityState.DissolvingOut);

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
            OnDissolveOutProgressNormalized?.Invoke(1);

            UpdateGlobalSlideState(XRSlideElement.VisibilityState.FullyHidden);

            dissolveOutProgress = 0;

            //Sort by load asset and unload first, then load new assets
            if (slideAssets.Count > 0)
            {
                LoadingActionProgress = 0;
                OnAssetLoadStarted?.Invoke(true);
                Debug.Log($"Preparing addressables started", Instance);
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
                        LoadingActionProgress = Math.Clamp(processedAssets / (float)slideAssets.Count, 0, 1);
                        OnAssetLoadProgressUpdate?.Invoke(Math.Clamp(processedAssets / (float)slideAssets.Count, 0, 1));
                        Debug.Log($"Processed {processedAssets}/{slideAssets.Count} slide assets (unloading); Progress: {processedAssets / (float)slideAssets.Count}", Instance);
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
                        LoadingActionProgress = Math.Clamp(processedAssets / (float)slideAssets.Count, 0, 1);
                        OnAssetLoadProgressUpdate?.Invoke(Math.Clamp(processedAssets / (float)slideAssets.Count, 0, 1));
                        Debug.Log($"Processed {processedAssets}/{slideAssets.Count} slide assets (loading); Progress: {processedAssets / (float)slideAssets.Count}", Instance);
                    }
                }

                LoadingActionProgress = 1;
                OnAssetLoadStarted?.Invoke(false);
                // UIButton.onUpdateUI?.Invoke();
                Debug.Log($"Preparing addressables ended", Instance);
            }

            // This unload is causing problems with certain assets. Unload should in theory still be happening but keep an eye on it
            if (DoUnloadUnusedAssets)
                Resources.UnloadUnusedAssets();
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

            UpdateGlobalSlideState(XRSlideElement.VisibilityState.DissolvingIn);

            if (currentSlide != null && nextSlide != null &&
                currentSlide.id != nextSlide.id)
                currentSlide = nextSlide;

            currentTrigger = nextTrigger;

            OnXRTriggerChanged?.Invoke(currentSlide, currentTrigger);

            if (XRCameraManager.Instance)
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
            OnDissolveInProgressNormalized?.Invoke(1);

            OnXRSlideChanged?.Invoke(currentSlide);

            UpdateGlobalSlideState(XRSlideElement.VisibilityState.FullyVisible);

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