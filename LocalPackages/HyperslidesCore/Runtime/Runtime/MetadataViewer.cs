using System;
using NSYNK.HyperSlides.Core;
using NSYNK.HyperSlides.Input;
using NSYNK.HyperSlides.Runtime;
using UnityEngine;
using UnityMainThreadDispatcher;
using Object = UnityEngine.Object;

namespace NSYNK.HyperSlides.UI
{
    /// <summary>
    /// A simple class you can attach to different gameobjects and update their object.
    /// Basically used for development return of values.
    /// TODO extend the GetType() types it can handle
    /// </summary>
    public class MetadataViewer : MonoBehaviour
    {
        public enum MetaType
        {
            SlideCount = 0,
            SlideActiveTime = 9,
            NextSlideName = 3,
            ActiveSlideName = 1,
            ActiveTriggerName = 2,
            ActiveSlideNameRaw = 10,
            EasingProcess = 4,
            AssetLoadingTrigger = 5,
            Annotation = 6,
            UserPosition = 7,
            DebugLogs = 8,
        }
        public MetaType metaType;

        public Object updateObject;

        private float assetLoadingProgress = 0f;

        private void OnEnable()
        {
            if (metaType == MetaType.DebugLogs)
                Application.logMessageReceivedThreaded += DebugLogging;
            else if (metaType == MetaType.AssetLoadingTrigger)
            {
                UIButton.onUpdateUI += OnUpdateUI;
                XRSlideManager.Instance.OnAssetLoadProgressUpdate += UpdateRadialProgressSmooth;
                XRSlideManager.Instance.OnAssetLoadStarted += UpdateRadialProgressVisibility;
            }
            else if (metaType == MetaType.SlideActiveTime)
            {
                XRSlideManager.Instance.OnSlideActiveTimeUpdate += UpdateMetadata;
            }
            else
            {
                XRSlideManager.Instance.OnXRSlideChanged += UpdateMetadata;
                XRSlideManager.Instance.OnXRTriggerChanged += UpdateMetadata;
                XRSlideManager.Instance.OnDissolveInProgressNormalized += UpdateMetadata;
                XRSlideManager.Instance.OnDissolveOutProgressNormalized += UpdateMetadataReversed;
                UpdateMetadata(null, null);
            }
        }

        private void OnUpdateUI()
        {
            Dispatcher.Enqueue(() =>
            {
                bool interactable = !XRUIManager.TimerRunning() && !XRSlideManager.LoadingActionInProgress;
                // if (!interactable)
                // {
                switch (updateObject.GetType().ToString())
                {
                    case "UnityEngine.UI.Mask":
                        ((UnityEngine.UI.Mask)updateObject).enabled = !interactable;
                        break;
                    case "UnityEngine.UI.Image":
                        UnityEngine.UI.Image imageToUpdate = (UnityEngine.UI.Image)updateObject;
                        imageToUpdate.enabled = !interactable;
                        if (interactable)
                            imageToUpdate.fillAmount = 0;
                        break;
                        // }
                }
            });
        }

        private void OnDisable()
        {
            if (XRSlideManager.Instance == null)
                return;

            if (metaType == MetaType.DebugLogs)
                Application.logMessageReceivedThreaded -= DebugLogging;
            else if (metaType == MetaType.AssetLoadingTrigger)
            {
                UIButton.onUpdateUI -= OnUpdateUI;
                XRSlideManager.Instance.OnAssetLoadProgressUpdate -= UpdateRadialProgressSmooth;
                XRSlideManager.Instance.OnAssetLoadStarted -= UpdateRadialProgressVisibility;
            }
            else if (metaType == MetaType.SlideActiveTime)
            {
                XRSlideManager.Instance.OnSlideActiveTimeUpdate -= UpdateMetadata;
            }
            else
            {
                XRSlideManager.Instance.OnXRSlideChanged -= UpdateMetadata;
                XRSlideManager.Instance.OnXRTriggerChanged -= UpdateMetadata;
                XRSlideManager.Instance.OnDissolveInProgressNormalized -= UpdateMetadata;
                XRSlideManager.Instance.OnDissolveOutProgressNormalized -= UpdateMetadataReversed;

                //XRSlideManager.OnDissolveInProgress -= UpdateMetadata;
                //XRSlideManager.OnDissolveOutProgress -= UpdateMetadataReversed;
            }
        }

        private void DebugLogging(string condition, string stackTrace, LogType type)
        {
            Dispatcher.Enqueue(() =>
            {
                if (!CheckUpdateObject())
                    return;

                switch (updateObject.GetType().ToString())
                {
                    case "TMPro.TextMeshProUGUI":
                        if (((TMPro.TextMeshProUGUI)updateObject).text.Length > 100000)
                            ((TMPro.TextMeshProUGUI)updateObject).text = "";
                        ((TMPro.TextMeshProUGUI)updateObject).text += condition + "\n";
                        break;
                }
            });
        }

        /// <summary>
        /// Update any string data based on slide and trigger changes.
        /// </summary>
        /// <param name="activeSlide"></param>
        /// <param name="activeTrigger"></param>
        private void UpdateMetadata(XRSlide activeSlide)
        {
            if (!CheckUpdateObject())
                return;

            switch (updateObject.GetType().ToString())
            {
                case "TMPro.TextMeshProUGUI":
                    ((TMPro.TextMeshProUGUI)updateObject).text = MetadataString();
                    break;
            }

            if (TryGetComponent(out RectTransform rect))
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }

        /// <summary>
        /// Overload to update metadata based on trigger changes as well.
        /// </summary>
        /// <param name="slide"></param>
        /// <param name="activeTrigger"></param>
        private void UpdateMetadata(XRSlide slide, XRSlide.Trigger activeTrigger)
        {
            switch (updateObject.GetType().ToString())
            {
                case "TMPro.TextMeshProUGUI":
                    ((TMPro.TextMeshProUGUI)updateObject).text = MetadataString();
                    break;
            }

            if (TryGetComponent(out RectTransform rect))
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }

        /// <summary>
        /// A float callback based on any animation update
        /// </summary>
        /// <param name="progress"></param>
        private void UpdateMetadata(float progress)
        {
            if (!CheckUpdateObject())
                return;

            switch (updateObject.GetType().ToString())
            {
                case "UnityEngine.UI.Image":
                    ((UnityEngine.UI.Image)updateObject).fillAmount = progress;
                    break;
                case "TMPro.TextMeshProUGUI":
                    ((TMPro.TextMeshProUGUI)updateObject).text = MetadataString();
                    break;
            }
        }

        /// <summary>
        /// Update the radial progress in reverse.
        /// </summary>
        /// <param name="progress"></param>
        private void UpdateMetadataReversed(float progress)
        {
            if (!CheckUpdateObject())
                return;

            switch (updateObject.GetType().ToString())
            {
                case "UnityEngine.UI.Image":
                    ((UnityEngine.UI.Image)updateObject).fillAmount = 1 - progress;
                    break;
            }
        }

        /// <summary>
        /// Update the progress of the radial loading based on the asset loading progress.
        /// </summary>
        /// <param name="started"></param>
        private void UpdateRadialProgressVisibility(bool started)
        {
            if (started) assetLoadingProgress = 0;
            switch (updateObject.GetType().ToString())
            {
                case "UnityEngine.UI.Mask":
                    if (started)
                    {
                        ((UnityEngine.UI.Mask)updateObject).enabled = true;
                    }
                    break;
                case "UnityEngine.UI.Image":
                    UnityEngine.UI.Image imageToUpdate = (UnityEngine.UI.Image)updateObject;
                    if (started)
                    {
                        imageToUpdate.enabled = true;
                    }
                    break;
            }
        }

        /// <summary>
        /// An eased progress filling based on a float
        /// </summary>
        /// <param name="progress"></param>
        private void UpdateRadialProgressSmooth(float progress)
        {
            bool isImage = updateObject.GetType().ToString() == "UnityEngine.UI.Image";
            bool isMask = updateObject.GetType().ToString() == "UnityEngine.UI.Mask";

            this.AnimateFloat(transform,
                Easing.Ease.EaseInOutQuad,
                // currentFill,
                assetLoadingProgress,
                progress,
                0.25f,
                0f,
                (f) =>
                {
                    assetLoadingProgress = f;
                    if (isImage)
                    {
                        UnityEngine.UI.Image imageToUpdate = (UnityEngine.UI.Image)updateObject;
                        imageToUpdate.fillAmount = f;
                    }
                },
                () =>
                {
                    if (!(progress > 0 && progress < 1))
                    {
                        assetLoadingProgress = 1;
                        if (isImage)
                        {
                            UnityEngine.UI.Image imageToUpdate = (UnityEngine.UI.Image)updateObject;
                            imageToUpdate.enabled = false;
                        }

                        if (isMask)
                        {
                            ((UnityEngine.UI.Mask)updateObject).enabled = false;
                        }

                        // Debug.Log($"[MetadataViewer][{gameObject.name}] Finished radial progress [{progress}]");
                    }
                });
        }

        /// <summary>
        /// Check if the update object is assigned and log a warning if not.
        /// </summary>
        /// <returns></returns>
        private bool CheckUpdateObject()
        {
            if (updateObject == null)
                Debug.LogWarning("Update object is null on: " + gameObject);

            return updateObject != null;
        }

        private void Update()
        {
            if (metaType == MetaType.UserPosition)
            {
                UpdateMetadata(null, null);
            }
        }

        /// <summary>
        /// Based on the selected MetaType, return a string to be displayed. Mainly used for development and debugging, but can be extended for other uses as well.
        /// </summary>
        /// <returns></returns>
        private string MetadataString()
        {
            string returnValue = "";

            if (XRSlideManager.Instance.GetPresentationData() == null)
                return returnValue;

            switch (metaType)
            {
                case MetaType.SlideCount:
                    returnValue = XRSlideManager.Instance.CurrentSlidePosition + " / " + XRSlideManager.Instance.GetPresentationData().contents.Count;
                    break;
                case MetaType.SlideActiveTime:
                    returnValue = $"{XRSlideManager.Instance.SlideActiveTime:0.0}s";
                    break;
                case MetaType.ActiveSlideName:
                    returnValue = XRSlideManager.Instance.GetCurrentSlide() != null ? XRSlideManager.Instance.GetCurrentSlide().cueNumber + " " + XRSlideManager.Instance.GetCurrentSlide().displayName : "No active slide";
                    break;
                case MetaType.ActiveSlideNameRaw:
                    returnValue = XRSlideManager.Instance.GetCurrentSlide() != null ? XRSlideManager.Instance.GetCurrentSlide().displayName : "";
                    break;
                case MetaType.ActiveTriggerName:
                    returnValue = XRSlideManager.Instance.GetCurrentTrigger() != null && XRSlideManager.Instance.GetCurrentTrigger().name != "EmptyTrigger" ? "Trigger: " + XRSlideManager.Instance.GetCurrentTrigger().name : "";
                    break;
                case MetaType.Annotation:
                    returnValue = XRSlideManager.Instance.GetCurrentSlide()?.annotation;
                    break;
                case MetaType.NextSlideName:
                    returnValue = XRSlideManager.Instance.GetNextSlide() != null ? (XRSlideManager.Instance.GetNextSlide().cueNumber + " " + XRSlideManager.Instance.GetNextSlide().displayName) : "No next slide";
                    returnValue = "Next Slide > " + returnValue;
                    break;
                case MetaType.UserPosition:
                    returnValue = XRInputManager.Instance.InputUserPosition.ToString();
                    break;
            }

            return returnValue;
        }
    }
}