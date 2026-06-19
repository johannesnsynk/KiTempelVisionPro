using TMPro;
using UnityEngine;

public class FrameRateIndicator : MonoBehaviour
{
    public float SmoothSpeed = 5f;
    float fps, smoothFps;
    TextMeshProUGUI text;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        text = GetComponent<TextMeshProUGUI>();
    }

    // Update is called once per frame
    void Update()
    {
        fps = 1f / Time.unscaledDeltaTime;
        if(Time.timeSinceLevelLoad < 1.0f) smoothFps = fps;
        smoothFps += (fps - smoothFps) * Mathf.Clamp(Time.unscaledDeltaTime * SmoothSpeed, 0, 1);
        text.text = ((int)smoothFps).ToString()+ " fps";
    }
}
