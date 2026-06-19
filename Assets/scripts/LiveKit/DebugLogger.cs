using UnityEngine;

public class DebugLogger : MonoBehaviour
{
    public TMPro.TextMeshProUGUI logText;

    void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        if (logText != null && type == LogType.Log) // Only log regular messages, ignore warnings and errors for this display
        {
            logText.text += logString + "\n";

            GUIUtility.systemCopyBuffer = logString; // Copy the log message to the clipboard
        }
    }
}