using UnityEngine;
using TMPro;

public class RecordTimer : MonoBehaviour
{
[SerializeField]
    private TextMeshProUGUI m_text;

    private float m_startTime;

    private void Start()
    {
        Activate(false);
    }

    // Update is called once per frame
    private void Update()
    {
        float totalSeconds = Time.realtimeSinceStartup - m_startTime;
        
        // Calculate hours, minutes, seconds
        int hours = Mathf.FloorToInt(totalSeconds / 3600f);
        int minutes = Mathf.FloorToInt((totalSeconds % 3600f) / 60f);
        int seconds = Mathf.FloorToInt(totalSeconds % 60f);
        
        // Calculate frames (based on the specified frame rate)
        float frameTime = 1f / 60;
        int frames = Mathf.FloorToInt((totalSeconds % 1f) / frameTime);
        
        // Format as HH:MM:SS:FF
        string timecode = string.Format("<mspace=0.5em>{0:00}:{1:00}:{2:00}:{3:00}</mspace>", 
                                        hours, minutes, seconds, frames);

        m_text.text = timecode;
    }

    public void StartTimer()
    {
        m_startTime = Time.realtimeSinceStartup;
        Activate(true);
    }

    public void StopTimer()
    {
        Activate(false);
    }

    public void Activate(bool active)
    {
        gameObject.SetActive(active);
    }
}
