using System;
using System.Collections.Generic;

namespace NSYNK.HyperSlides.Core
{
    /// <summary>
    /// This class is used to wrap the list of XRPresentation objects for JSON serialization.
    /// It is necessary because Unity's JsonUtility does not support serializing lists directly.
    /// </summary>
    [Serializable]
    public class JSONWrapper
    {
        public List<XRPresentation> allPresentations;
        public JSONWrapper(List<XRPresentation> newList) => allPresentations = newList;
    }
}