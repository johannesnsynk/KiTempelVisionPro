using System;
using Newtonsoft.Json;

namespace NSYNK.HyperSlides
{
    /// <summary>
    /// A base class other strapi classes can inherit from to not doulbe standard things like datetime values or ids.
    /// </summary>
    [Serializable]
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class JSONObjectBase
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string displayName;
        public string id;
        public DateTime createdAt;
        public DateTime updatedAt;
    }
}