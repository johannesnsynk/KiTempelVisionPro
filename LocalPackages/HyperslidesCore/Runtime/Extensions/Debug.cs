using UnityEngine;
using UnityMainThreadDispatcher;

namespace NSYNK.HyperSlides
{
    public static class Debug
    {
        [HideInCallstack]
        static public void Break()
        {
            UnityEngine.Debug.Break();
        }

        [HideInCallstack]
        static public async Awaitable LogQueue(object message, UnityEngine.Object context = null)
        {
            await Awaitable.MainThreadAsync();

            if (context != null)
                Log(message, context);
            else
                Log(message);
        }

        [HideInCallstack]
        static public void Log(object message)
        {
#if UNITY_EDITOR
            UnityEngine.Debug.Log(message);
#else
            CleanLog(message.ToString());
#endif
        }

        [HideInCallstack]
        static public void Log(object message, Object context)
        {
#if UNITY_EDITOR
            UnityEngine.Debug.Log($"<color=#{LogColor(context)}>{context}</color>\n{message}", context);
#else
            CleanLog(message.ToString());
#endif
        }

        [HideInCallstack]
        static public void Log(object message, System.Type type)
        {
#if UNITY_EDITOR
            UnityEngine.Debug.Log($"<color=#{LogColorFromType(type)}>{type}</color>\n{message}", null);
#else
            CleanLog(message.ToString());
#endif
        }

        static private void CleanLog(string message)
        {
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

            UnityEngine.Debug.Log(message);
        }

        [HideInCallstack]
        static public void LogWarning(object message)
        {
            UnityEngine.Debug.LogWarning(message);
        }

        [HideInCallstack]
        static public void LogWarning(object message, Object context)
        {
            UnityEngine.Debug.LogWarning($"<color=#{LogColor(context)}>{context}</color>\n{message}", context);
        }

        [HideInCallstack]
        static public void LogError(object message)
        {
            UnityEngine.Debug.LogError(message);
        }

        [HideInCallstack]
        static public void LogError(object message, Object context)
        {
            UnityEngine.Debug.LogError($"<color=#{LogColor(context)}>{context}</color>\n{message}", context);
        }

        [HideInCallstack]
        static public void LogError(object message, System.Type type)
        {
            UnityEngine.Debug.LogError($"<color=#{LogColorFromType(type)}>{type}</color>\n{message}", null);
        }

        [HideInCallstack]
        static public void DrawLine(Vector3 start, Vector3 end, Color color, float duration = 0.0F, bool depthTest = true)
        {
            UnityEngine.Debug.DrawLine(start, end, color, duration, depthTest);
        }

        [HideInCallstack]
        public static string LogColor<T>(T caller)
        {
            Color color = GetLogColor(caller.GetType());

            return ColorUtility.ToHtmlStringRGB(color);
        }

        [HideInCallstack]
        public static string LogColorFromType(System.Type callerType)
        {
            Color color = GetLogColor(callerType);

            return ColorUtility.ToHtmlStringRGB(color);
        }

        [HideInCallstack]
        public static Color GetLogColor(System.Type callerType)
        {
            Color color = Color.white;

            switch (callerType.Namespace.ToString())
            {
                case "NSYNK.HyperSlides":
                    color = new(0, 204, 102);
                    break;
                case "NSYNK.HyperSlides.Core":
                    color = Color.cyan;
                    break;
                case "NSYNK.HyperSlides.Network":
                    color = Color.green;
                    break;
                case "NSYNK.HyperSlides.Runtime":
                    color = Color.yellow;
                    break;
                case "NSYNK.HyperSlides.Input":
                    color = Color.grey;
                    break;
                case "NSYNK.HyperSlides.XR":
                    color = Color.magenta;
                    break;
                case "NSYNK.HyperSlides.Network.Communication":
                    color = Color.blue;
                    break;
                case "NSYNK.HyperSlides.EditorScripts":
                    color = Color.red;
                    break;
            }

            return color;
        }
    }
}