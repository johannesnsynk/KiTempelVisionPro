using System;
using System.Collections.Generic;
using NSYNK.HyperSlides.Core;
using UnityEngine;

namespace NSYNK.HyperSlides.Runtime
{
    public class XRLoadVisualizer : MonoBehaviour
    {
        [Range(0, 1)]
        public float dissolveProgress = 0;

        public bool visualizerVisible = false;

        public List<VisibilityStateEvent<float>> visibilityStateEvents = new List<VisibilityStateEvent<float>>();

        /// <summary>
        /// Register on <see cref="XRSlideManager"/> events.
        /// </summary>
        private void OnEnable()
        {
            XRSlideManager.Instance.OnGlobalSlideTransitioned += UpdateOnGlobalSlideTransition;
            XRSlideManager.Instance.OnDissolveInProgressNormalized += UpdateDissolveInProgress;
            XRSlideManager.Instance.OnDissolveOutProgressNormalized += UpdateDissolveOutProgress;
        }

        /// <summary>
        /// Release any registered callbacks.
        /// </summary>
        private void OnDisable()
        {
            XRSlideManager.Instance.OnGlobalSlideTransitioned -= UpdateOnGlobalSlideTransition;
            XRSlideManager.Instance.OnDissolveInProgressNormalized -= UpdateDissolveInProgress;
            XRSlideManager.Instance.OnDissolveOutProgressNormalized -= UpdateDissolveOutProgress;
        }

        /// <summary>
        /// Update the visualizers state based on the global slide manager visibility state
        /// </summary>
        /// <param name="visibilityState"></param>
        private void UpdateOnGlobalSlideTransition(XRSlideElement.VisibilityState visibilityState)
        {
            visibilityStateEvents.ForEach(e =>
            {
                if (e.visibilityState == visibilityState)
                    e.unityEvent.Invoke(dissolveProgress);
            });

            switch (visibilityState)
            {
                case XRSlideElement.VisibilityState.FullyVisible:
                    Debug.Log("Hide visualizer");
                    break;
                case XRSlideElement.VisibilityState.DissolvingOut:
                    Debug.Log("Show visualizer");
                    break;
                case XRSlideElement.VisibilityState.FullyHidden:
                    Debug.Log("Idle visualizer");
                    break;
                case XRSlideElement.VisibilityState.DissolvingIn:
                    Debug.Log("Prepare to hide visualizer");
                    break;
            }
        }

        /// <summary>
        /// Update values with the fade progress
        /// </summary>
        /// <param name="progress"></param>
        private void UpdateDissolveInProgress(float progress)
        {
            dissolveProgress = Mathf.Clamp01(1 - progress);
        }

        /// <summary>
        /// Update values with the fade progress
        /// </summary>
        /// <param name="progress"></param>
        private void UpdateDissolveOutProgress(float progress)
        {
            dissolveProgress = Mathf.Clamp01(progress);
        }
    }
}