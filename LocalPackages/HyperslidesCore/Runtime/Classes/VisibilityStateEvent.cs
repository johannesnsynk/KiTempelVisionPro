using System;
using UnityEngine;
using UnityEngine.Events;

namespace NSYNK.HyperSlides.Runtime
{
    /// <summary>
    /// A visibility event, that can hook into our current enum types.
    /// TODO Extent the T value to be more dynamic without having different type arrays
    /// </summary>
    /// <typeparam name="T">The type you want to pass (currently only used as a float array)</typeparam>
    [Serializable]
    public class VisibilityStateEvent<T>
    {
        [HideInInspector]
        public string stateName = "";
        public XRSlideElement.VisibilityState visibilityState = XRSlideElement.VisibilityState.None;
        public UnityEvent<T> unityEvent;

        public void Validate()
        {
            stateName = visibilityState.ToString();
        }
    }
}