using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using static NSYNK.HyperSlides.Runtime.XRSlideElement;

namespace NSYNK.HyperSlides.Runtime
{
    public abstract class XRSlideDissolveComponent : MonoBehaviour
    {
        //Events you can hook 
        [Header("Visibility and Trigger Events")]
        [ReadOnly]
        public VisibilityState visibilityState;
        public List<VisibilityStateEvent<float>> visibilityStateEvents = new List<VisibilityStateEvent<float>>();

        private XRSlideElement rootXRSlideElement;

        protected virtual void Awake() { }

        /// <summary>
        /// Subscribe to the root slide element if found and trigger first visbility state
        /// </summary>
        protected virtual void OnEnable()
        {
            if (!rootXRSlideElement) 
                rootXRSlideElement = (XRSlideElement)transform.GetComponentInParent(typeof(XRSlideElement), true);

            if (!rootXRSlideElement)
                return;

            visibilityState = rootXRSlideElement.Visibility;
            SyncVisibilityState(visibilityState);

            // Make sure to sync the initial state properly (especially for scripts inside addressables)
            OnDissolveChanged(rootXRSlideElement.DissolveNormalized, rootXRSlideElement.DissolveInOut);
            OnTriggerChanged(rootXRSlideElement.Trigger);
            
            rootXRSlideElement.OnVisibilityStateChanged += SyncVisibilityState;
            rootXRSlideElement.OnDissolveChanged += OnDissolveChanged;
            rootXRSlideElement.OnTriggerChanged += OnTriggerChanged;            
        }

        /// <summary>
        /// Handle unsubscribing to all delegates and fire visibility update before disabling
        /// </summary>
        protected virtual void OnDisable()
        {
            //rootXRSlideElement = (XRSlideElement)transform.GetComponentInParent(typeof(XRSlideElement), true);

            if (!rootXRSlideElement)
                return;

            visibilityState = rootXRSlideElement.Visibility;
            SyncVisibilityState(visibilityState);

            rootXRSlideElement.OnVisibilityStateChanged -= SyncVisibilityState;
            rootXRSlideElement.OnDissolveChanged -= OnDissolveChanged;
            rootXRSlideElement.OnTriggerChanged -= OnTriggerChanged;
        }

        public abstract void OnDissolveChanged(float dissolveNormalized, float dissolveInOut);
        public abstract void OnVisibilityStateChanged(VisibilityState state);
        public virtual void OnTriggerChanged(XRSlide.Trigger trigger)
        {
            return;
        }

        /// <summary>
        /// Force the update of the current visibilitystate without overriding
        /// </summary>
        /// <param name="state"></param>
        private protected void SyncVisibilityState(VisibilityState state)
        {
            visibilityState = state;
            OnVisibilityStateChanged(state);

            visibilityStateEvents.ForEach(e => {
                if (e.visibilityState == visibilityState)
                    e.unityEvent.Invoke(0);
            });
        }
    }
}