using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls letterbox bars to maintain a 16:9 aspect ratio regardless of device screen size.
/// Place this script on a Canvas with ScreenSpace-Overlay render mode.
/// </summary>
public class LetterboxController : MonoBehaviour
{
    [Header("Black Bar References")]
    [SerializeField] private RectTransform topBar;
    [SerializeField] private RectTransform bottomBar;

    [Header("Settings")]
    [SerializeField] private float targetAspect = 16f / 9f;
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool adjustOnScreenChange = true;

    private Canvas canvas;
    private float currentAspect;
    private int lastScreenWidth;
    private int lastScreenHeight;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            Debug.LogError("LetterboxController requires a Canvas with ScreenSpace-Overlay render mode!");
        }
    }

    private void Start()
    {
        if (applyOnStart)
        {
            UpdateLetterbox();
        }

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
    }

    private void Update()
    {
        if (adjustOnScreenChange && (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight))
        {
            UpdateLetterbox();
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
        }
    }

    /// <summary>
    /// Updates the letterbox bars to maintain the target aspect ratio
    /// </summary>
    public void UpdateLetterbox()
    {
        if (topBar == null || bottomBar == null)
        {
            Debug.LogError("Top or bottom bar reference is missing!");
            return;
        }

        // Calculate current aspect ratio
        currentAspect = (float)Screen.width / Screen.height;

        // Get screen dimensions in canvas space
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        float screenHeight = canvasRect.rect.height;
        float screenWidth = canvasRect.rect.width;

        if (currentAspect > targetAspect)
        {
            // DOn't do anything to have working ui on iphone

            // Screen is wider than 16:9 - add bars on sides (not implemented in this version)
            // This is less common on iPad, but you could add side bars if needed
            // topBar.sizeDelta = new Vector2(0, 0);
            // bottomBar.sizeDelta = new Vector2(0, 0);
        }
        else if (currentAspect < targetAspect)
        {
            // Screen is taller than 16:9 - add letterbox bars on top and bottom
            // Calculate the height needed for the bars
            float targetHeight = screenWidth / targetAspect;
            float barHeight = (screenHeight - targetHeight) / 2f;

            // Apply to the bars
            topBar.sizeDelta = new Vector2(0, barHeight);
            bottomBar.sizeDelta = new Vector2(0, barHeight);

            // Position the bars
            topBar.anchoredPosition = Vector2.zero;
            bottomBar.anchoredPosition = Vector2.zero;
        }
        else
        {
            // Already at target aspect ratio - hide the bars
            // topBar.sizeDelta = new Vector2(screenWidth, 0);
            // bottomBar.sizeDelta = new Vector2(screenWidth, 0);
        }
    }

    /// <summary>
    /// Returns the visible area's dimensions as a Rect (in screen coordinates)
    /// </summary>
    /// <returns>Rect representing the visible 16:9 area</returns>
    public Rect GetVisibleArea()
    {
        if (currentAspect >= targetAspect)
        {
            // No letterboxing or pillarboxing
            return new Rect(0, 0, Screen.width, Screen.height);
        }
        else
        {
            // Letterboxing - calculate the visible height
            float visibleHeight = Screen.width / targetAspect;
            float barHeight = (Screen.height - visibleHeight) / 2f;
            
            return new Rect(0, barHeight, Screen.width, visibleHeight);
        }
    }
}