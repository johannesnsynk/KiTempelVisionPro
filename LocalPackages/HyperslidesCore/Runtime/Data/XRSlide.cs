using Newtonsoft.Json;

using UnityEngine;
using UnityEngine.Events;

using System;
using System.Collections.Generic;

namespace NSYNK.HyperSlides
{
    /// <summary>
    /// A json slide data container to handle the strapi response
    /// </summary>
    [Serializable]
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class XRSlide : JSONObjectBase, IContent
    {
        public static implicit operator bool(XRSlide slide) => slide != null;

        public XRSlide(string presentation, int cue)
        {
            cueNumber = cue;
            displayName = "Empty Slide";
            annotation = "Empty Slide Annotation";

            contentTags.Add(new ContentTag("Empty Tag"));

            presentationId = presentation;
        }

        public int cueNumber;
        public bool enabled;
        public string displayName;
        public string preview;
        public string headlineID;
        public string mediaID;
        public string annotation;
        public XRCameraTransform cameraTransform;
        public List<ContentTag> contentTags = new();
        public List<Trigger> triggers = new();
        public List<XRAnchor> anchorOverrides = new();
        public string presentationId;

        /// <summary>
        /// ContentTag class based on the JSON response
        /// </summary>
        [Serializable]
        [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
        public class ContentTag
        {
            public ContentTag() { }
            public ContentTag(string tagName)
            {
                id = tagName;
                name = tagName;
            }

            public string id;
            public string name;
        }

        /// <summary>
        /// Trigger class based on the JSON response
        /// </summary>
        [Serializable]
        [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
        public class Trigger : IContent
        {
            public string id;
            public string name;

            public Trigger(string _id, string _name)
            {
                id = _id;
                name = _name;
            }
        }

        /// <summary>
        /// A trigger event that can be triggered from the <see cref="NSYNK.HyperSlides.Runtime.XRSlideElement.triggerEvents"/>
        /// </summary>
        [Serializable]
        public class TriggerEvent
        {
            public string trigger;
            public UnityEvent triggeredEvent;
        }

        /// <summary>
        /// XRAnchor class based on the JSON response
        /// </summary>
        [Serializable]
        public class XRAnchor
        {

        }

        /// <summary>
        /// XRCameraTransform class based on the JSON response
        /// </summary>
        [Serializable]
        [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
        public class XRCameraTransform
        {
            public Vector3 scale;
            public Vector3 location;
            public Vector3 rotation;
        }
    }
}