using UnityEngine;

namespace NSYNK.HyperSlides.Runtime
{
    [CreateAssetMenu(fileName = "NewServerConnection", menuName = "Hyperslides/Server Connection", order = 0)]
    public class ServerConnection : ScriptableObject
    {
        [Header("Custom Server Connection")]
        public string ConnectionName = "Default Connection";
        public string BackendIP = "https://next.hyperslides.de";
        public string NakamaIP = "nakama.hyperslides.de";
        public int Port = 443;
        public bool SSL = true;
        public string Protocol => SSL ? "https" : "http";
        public string ServerKey = "ooRB3B*7prh-dAD8Qnz*.UzTY";

        [Header("Sentry Logging")]
        public bool SentryLogging = false;
    }

    public class ServerConnectionJSON
    {
        public string BackendIP = "";
        public string NakamaIP = "";
        public int Port = 0;
        public bool SSL = true;
        public string Protocol => SSL ? "https" : "http";
        public string ServerKey = "";
        public bool SentryLogging = false;
    }
}