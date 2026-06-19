using System;

namespace NSYNK.HyperSlides.Runtime
{
    /// <summary>
    /// A session code wrapper to be stored
    /// </summary>
    [Serializable]
    public class SessionCode
    {
        public string sessionCode = "000-000";
        public SessionCode(string code) => sessionCode = code;
    }
}