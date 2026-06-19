using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NSYNK.HyperSlides.Network;
using UnityEngine;

namespace NSYNK.HyperSlides
{
    /// <summary>
    /// A json presentation data container to handle localisation within a presentation
    /// TODO add querying for locale data and switching to this content => Events
    /// </summary>
    [Serializable]
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class XRPresentation : JSONObjectBase
    {
        public string title;
        public string description;
        public string cover;
        public object[] anchors;
        public string localeId;

        public List<IContent> contents = new ();
        public List<XRSlide> slides = new();

        //Make transform overrides optional to support downward compatibility with older presentation formats that don't include them
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<XRNetworkObjects.SessionTransformOverride> transformOverrides = new();

        public static implicit operator bool(XRPresentation presentation) => presentation != null;

        /// <summary>
        /// Get all slides and triggers and pass them into an interface list.
        /// </summary>
        public void GetContents()
        {
            contents.Clear();

            foreach (XRSlide slide in slides)
            {
                //Maybe use later if we include inactive slides for whatever reason
                //if (!slide.enabled)
                //    continue;

                contents.Add(slide);

                if (slide.triggers != null)
                    foreach (XRSlide.Trigger trigger in slide.triggers)
                        contents.Add(trigger);
            }
        }

        /// <summary>
        /// Set the current language and region
        /// </summary>
        /// <param name="locale">Language localisation identifier string</param>
        /// <param name="location">TODO add locationinfo conversion</param>
        public void SetCurrentLanguageAndRegion(string locale, LocationInfo location)
        {
            Debug.Log("Set language and region to: " + locale + " => " + location.ToString());
        }
    }
}