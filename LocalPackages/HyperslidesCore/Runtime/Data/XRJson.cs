using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NSYNK.HyperSlides
{
    /// <summary>
    /// A wrapper for the strapi response to be able to access the result json object.
    /// </summary>
    [Serializable]
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class XRJson
    {
        //public string[] _reqBody;
        //public Dictionary<string, int> _reqParams;
        public string timestamp;
        public string controller;
        public XRPresentation data;
    }

    /// <summary>
    /// An overwriting XRJson inheritance to be able to handle single object and arrays of <see cref="XRPresentation>"/> in the result object.
    /// </summary>
    [Serializable]
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class XRJsonArray : XRJson
    {
        public new List<XRPresentation> data;
    }
}