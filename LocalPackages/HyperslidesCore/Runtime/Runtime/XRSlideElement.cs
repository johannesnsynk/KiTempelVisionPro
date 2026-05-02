using UnityEngine;
using NSYNK.HyperSlides.Core;
using System.Collections.Generic;
using System;
using UnityEngine.Events;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using System.Threading.Tasks;
using System.Threading;

namespace NSYNK.HyperSlides.Runtime
{
    /// <summary>
    /// The runtime equivalent to the JSON slide data source.
    /// This class handles the visbility of its own child contents.
    /// It relies on the <see cref="XRSlideManager"/> events like <see cref="XRSlideManager.OnXRSlideChange"/> and <see cref="XRSlideManager.OnXRTriggerChanged"/>
    /// </summary>
    public class XRSlideElement : MonoBehaviour
    {
        public enum VisibilityState {
            None = -1,
            FullyVisible = 0,
            FullyHidden = 1,
            DissolvingOut = 2,
            DissolvingIn = 3,
            WillDissolve = 4
        };

        //Delegates
        public delegate void OnVisibilityStateChange(VisibilityState state);
        public delegate void OnDissolveChange(float dissolveNormalized, float dissolveInOut);
        public delegate void OnTriggerChange(XRSlide.Trigger trigger);
        public OnVisibilityStateChange OnVisibilityStateChanged;
        public OnDissolveChange OnDissolveChanged;
        // When a trigger update gets send
        public OnTriggerChange OnTriggerChanged;

        //Public and/or inspector values
        public string ContentTag { get { return contentTag; } private set { contentTag = value; } }
        [Header("Runtime properties")]
        [SerializeField]
        private string contentTag = "default";
        public float DissolveInDuration = 1;
        public float DissolveOutDuration = 1;

        [SerializeField, ReadOnly]
        private float dissolveNormalized = 0;
        [SerializeField, ReadOnly]
        private float dissolveInOut = 0;

        public float DissolveNormalized => dissolveNormalized;
        public float DissolveInOut => dissolveInOut;

        private VisibilityState visibilityState;
        public VisibilityState Visibility => visibilityState;

        private XRSlide.Trigger trigger;
        public XRSlide.Trigger Trigger => trigger;

        //Events you can hook 
        [Header("Visibility and Trigger Events")]
        public List<VisibilityStateEvent<float>> visibilityStateEvents = new List<VisibilityStateEvent<float>>();
        public List<XRSlide.TriggerEvent> triggerEvents;

        [Header("Content Addressable Reference")]
        [ReadOnly]
        public bool assetIsValid = false;
        public AssetReference assetReference;
        private GameObject instantiatedReference;
        [ReadOnly]
        public bool AssetActive, IsLoading = false;
        [ReadOnly]
        public GameObject rootContent;

        private void Start()
        {
            //Avoid 0 durations
            DissolveInDuration = Mathf.Clamp(DissolveInDuration, .1f, Mathf.Infinity);
            DissolveOutDuration = Mathf.Clamp(DissolveOutDuration, .1f, Mathf.Infinity);
        }

        /// <summary>
        /// Register on <see cref="XRSlideManager"/> events.
        /// </summary>
        private void OnEnable()
        {
            ValidateRootContent();

            XRSlideManager.ONXRSlidePreload += PreloadAsset;
            XRSlideManager.ONXRSlidePrepare += Prepare;
            XRSlideManager.OnXRSlideChanged += UpdateVisibility;
            XRSlideManager.OnXRTriggerChanged += UpdateTrigger;
            XRSlideManager.OnDissolveInProgress += UpdateDissolveInProgress;
            XRSlideManager.OnDissolveOutProgress += UpdateDissolveOutProgress;

            SetVisibilityState(VisibilityState.FullyHidden);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += ValidateRootContent;
#endif
        }

        /// <summary>
        /// Release any registered callbacks.
        /// </summary>
        private void OnDisable()
        {
            XRSlideManager.ONXRSlidePreload -= PreloadAsset;
            XRSlideManager.ONXRSlidePrepare -= Prepare;
            XRSlideManager.OnXRSlideChanged -= UpdateVisibility;
            XRSlideManager.OnXRTriggerChanged -= UpdateTrigger;
            XRSlideManager.OnDissolveInProgress -= UpdateDissolveInProgress;
            XRSlideManager.OnDissolveOutProgress -= UpdateDissolveOutProgress;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= ValidateRootContent;
#endif
        }

        /// <summary>
        /// Check, if an empty root gameobject is present or create it
        /// </summary>
        private void ValidateRootContent()
        {
            if (transform.childCount == 0)
            {
                rootContent = new GameObject("ElementRoot");
                rootContent.transform.SetParent(transform);
            }
            else
                rootContent = transform.GetChild(0).gameObject;

            visibilityStateEvents.ForEach(vse => vse.Validate());
        }

        private async void OnValidate()
        {
            try
            {
                var locations = await Addressables.LoadResourceLocationsAsync(assetReference).Task;
                assetIsValid = locations != null && locations.Count > 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error validating AssetReference for {gameObject.name}: {ex.Message}");
                assetIsValid = false;
            }
        }

        /// <summary>
        /// Check if the asset reference is set and returning a runtimekey
        /// </summary>
        /// <returns></returns>
        public bool ValidAsset() => !string.IsNullOrEmpty(assetReference.RuntimeKey.ToString());

        /// <summary>
        /// Load the asset reference, if there is one, its not currently loading and if its not instantiated already
        /// </summary>
        public async void LoadAssetReference()
        {
            if (!ValidAsset() || instantiatedReference || IsLoading)
                return;

            IsLoading = true;

            //Check if the addressable actually exists
            var locations = await Addressables.LoadResourceLocationsAsync(assetReference).Task;
            if (locations != null && locations.Count > 0)
            {
                AsyncOperationHandle<GameObject> assetLoader = Addressables.InstantiateAsync(assetReference, rootContent.transform, false);
                await assetLoader.Task;

                if (assetLoader.Status == AsyncOperationStatus.Succeeded)
                {
                    //Debug.Log("Load asset reference: " + assetLoader.Result);

                    instantiatedReference = assetLoader.Result;
                    AssetActive = true;
                    IsLoading = false;
                }
                else
                {
                    if (visibilityState == VisibilityState.FullyHidden)
                        UnloadAssetReference();
                }
            }
            else
            {
                Debug.LogWarning("Error in loading asset: " + assetReference);
                AssetActive = true;
                IsLoading = false;
            }
        }

        /// <summary>
        /// Unload the current loaded asset, if its active
        /// </summary>
        public void UnloadAssetReference()
        {
            if (AssetActive)
            {
                //Debug.Log("Unload asset reference: " + assetLoader.Result);

                Addressables.ReleaseInstance(instantiatedReference);
                instantiatedReference = null;
                AssetActive = false;

                //Resources.UnloadUnusedAssets();
            }
        }

        /// <summary>
        /// Reset the slide element and hide the content
        /// </summary>
        private void Reset()
        {
            SetVisibilityState(VisibilityState.FullyHidden);

            OnDissolveChanged?.Invoke(0, 0);

            if(rootContent)
                rootContent.SetActive(false);
        }

        /// <summary>
        /// Update the visibility based on <see cref="XRSlide.ContentTag"/>.
        /// </summary>
        /// <param name="slide"></param>
        private void UpdateVisibility(XRSlide slide)
        {
            if (slide == null)
            {
                Reset();
                return;
            }

            bool shouldBeVisible = ShouldBeVisible(slide);

            switch (visibilityState)
            {
                case VisibilityState.WillDissolve:
                    SetVisibilityState(shouldBeVisible ? VisibilityState.DissolvingIn : VisibilityState.DissolvingOut);
                    break;
                case VisibilityState.DissolvingIn:
                    SetVisibilityState(shouldBeVisible ? VisibilityState.FullyVisible : VisibilityState.DissolvingOut);
                    break;
                case VisibilityState.DissolvingOut:
                    SetVisibilityState(shouldBeVisible ? VisibilityState.DissolvingIn : VisibilityState.FullyHidden);
                    break;
                case VisibilityState.FullyVisible:
                    if (shouldBeVisible)
                    {
                        OnDissolveChanged?.Invoke(1, 1);
                        OnVisibilityStateChanged?.Invoke(visibilityState);
                    }
                    break;
                case VisibilityState.FullyHidden:
                    if (!shouldBeVisible)
                        Reset();
                    else
                        SetVisibilityState(VisibilityState.DissolvingIn);
                    break;
            }

            rootContent.SetActive(visibilityState != VisibilityState.FullyHidden && visibilityState != VisibilityState.WillDissolve);
        }

        /// <summary>
        /// Before dissolving out the current slide, prepare all new and old slidelements to act accordingly
        /// </summary>
        /// <param name="slide">The next slide to check against</param>
        private void Prepare(XRSlide slide)
        {
            bool shouldBeVisible = ShouldBeVisible(slide);
            bool isVisible = visibilityState != VisibilityState.FullyHidden && visibilityState != VisibilityState.WillDissolve;

            if (!isVisible && shouldBeVisible)
                XRSlideManager.SetDissolveInDuration(DissolveInDuration);

            if(isVisible && !shouldBeVisible)
                XRSlideManager.SetDissolveOutDuration(DissolveOutDuration);

            switch (visibilityState)
            {
                case VisibilityState.WillDissolve:
                    if(isVisible)
                        SetVisibilityState(shouldBeVisible ? VisibilityState.DissolvingIn : VisibilityState.DissolvingOut);
                    break;
                case VisibilityState.DissolvingIn:
                    SetVisibilityState(VisibilityState.DissolvingOut);
                    break;
                case VisibilityState.DissolvingOut:
                    SetVisibilityState(VisibilityState.DissolvingOut);
                    break;
                case VisibilityState.FullyVisible:
                    SetVisibilityState(shouldBeVisible ? VisibilityState.FullyVisible : VisibilityState.DissolvingOut);
                    break;
                case VisibilityState.FullyHidden:
                    SetVisibilityState(shouldBeVisible ? VisibilityState.WillDissolve : VisibilityState.FullyHidden);
                    break;
            }

            if (shouldBeVisible || AssetActive)
                if(ValidAsset())
                    XRSlideManager.RegisterSlideElement(this, shouldBeVisible);
        }

        /// <summary>
        /// Register this slidelement to be loaded on next slide update
        /// </summary>
        /// <param name="slide"></param>
        public void PreloadAsset(XRSlide slide)
        {
            bool shouldBeVisible = ShouldBeVisible(slide);

            if (shouldBeVisible && ValidAsset())
                XRSlideManager.RegisterSlideElement(this, shouldBeVisible);
        }

        /// <summary>
        /// Check against the current slides content tags
        /// </summary>
        /// <param name="slide">The slide providing tag and trigger information</param>
        /// <returns></returns>
        private bool ShouldBeVisible(XRSlide slide)
        {
            if( slide != null )
                return slide.contentTags.Find(tag => tag.name == ContentTag) != null;
            else
                return false;
        }

        /// <summary>
        /// Update trigger events using the same delegate for easier use <see cref="XRSlide.Trigger"/>
        /// </summary>
        /// <param name="slide"></param>
        /// <param name="trigger"></param>
        /// <param name="visibilityState"></param>
        private void UpdateTrigger(XRSlide slide, XRSlide.Trigger trigger)
        {
            //Debug.Log("Checking slide trigger object visbility: " + contentTag);
            bool shouldBeVisible = ShouldBeVisible(slide);

            if (shouldBeVisible && trigger != null)
                HandleTriggerEvents(trigger);

        }

        /// <summary>
        /// Handle the trigger events from the current trigger
        /// </summary>
        /// <param name="trigger">The trigger to check against</param>
        private void HandleTriggerEvents(XRSlide.Trigger trigger)
        {
            if (trigger != null && visibilityState != VisibilityState.FullyHidden)
            {
                this.trigger = trigger;
                OnTriggerChanged?.Invoke(trigger);

                XRSlide.TriggerEvent foundEvent = triggerEvents.Find(t => t.trigger == trigger.name);

                if (foundEvent != null)
                    foundEvent.triggeredEvent?.Invoke();
            }
        }

        /// <summary>
        /// Update values with the fade progress
        /// </summary>
        /// <param name="progress"></param>
        private void UpdateDissolveInProgress(float progress)
        {
            bool isVisibleOrDissolving =
                visibilityState == VisibilityState.DissolvingIn ||
                visibilityState == VisibilityState.FullyVisible;

            rootContent.SetActive(isVisibleOrDissolving);

            if (visibilityState != VisibilityState.DissolvingIn)
                return;

            dissolveNormalized = Mathf.Clamp01(Mathf.Lerp(0, 1, progress / DissolveInDuration));
            dissolveInOut = dissolveNormalized;

            OnDissolveChanged?.Invoke(dissolveNormalized, dissolveInOut);

            visibilityStateEvents.ForEach(e => {
                if (e.visibilityState == visibilityState)
                    e.unityEvent.Invoke(dissolveNormalized);
            });
        }

        /// <summary>
        /// Update values with the fade progress
        /// </summary>
        /// <param name="progress"></param>
        private void UpdateDissolveOutProgress(float progress)
        {
            bool isVisibleOrDissolving =
                visibilityState == VisibilityState.DissolvingOut ||
                visibilityState == VisibilityState.FullyVisible;

            rootContent.SetActive(isVisibleOrDissolving);

            if (visibilityState != VisibilityState.DissolvingOut)
                return;

            dissolveNormalized = Mathf.Clamp01(Mathf.Lerp(1, 0, progress / DissolveOutDuration));
            dissolveInOut = Mathf.Lerp(1, 2, progress / DissolveOutDuration);

            OnDissolveChanged?.Invoke(dissolveNormalized, dissolveInOut);

            visibilityStateEvents.ForEach(e => {
                if (e.visibilityState == visibilityState)
                    e.unityEvent.Invoke(dissolveNormalized);
            });
        }

        /// <summary>
        /// Update the visibility of the visual content.
        /// TODO add more checks and integrate more states
        /// </summary>
        /// <param name="visibilityState"></param>
        private void SetVisibilityState(VisibilityState state)
        {
            //Debug.Log(gameObject.name + " visibility state: " + state);

            visibilityState = state;

            OnDissolveChanged?.Invoke(dissolveNormalized, dissolveInOut);
            OnVisibilityStateChanged?.Invoke(visibilityState);

            visibilityStateEvents.ForEach(e => {
                if (e.visibilityState == visibilityState)
                    e.unityEvent.Invoke(0);
            });
        }
    }
}