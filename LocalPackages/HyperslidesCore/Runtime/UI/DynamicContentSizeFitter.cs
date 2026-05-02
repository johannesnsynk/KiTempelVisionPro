using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A content size fitter that is animating the new size instead of setting it directly
/// </summary>
public class DynamicContentSizeFitter : ContentSizeFitter
{
    public float maxWidth = 0;

    private float easingSpeed = 15;
    private Vector2 minSize = Vector2.zero;
    private Vector2 animatedSize = Vector2.zero;
    private RectTransform rect;

    protected override void Awake()
    {
        base.Awake();

        rect = GetComponent<RectTransform>();
    }

    public override void SetLayoutHorizontal() {
        if (!Application.isPlaying)
            base.SetLayoutHorizontal();
    }

    public override void SetLayoutVertical() {
        if (!Application.isPlaying)
            base.SetLayoutVertical();
    }

    /// <summary>
    /// Double check for the fitmodes, so we avoid recalculating if not needed and save some performance (you never know)
    /// </summary>
    private void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        LayoutRebuilder.MarkLayoutForRebuild(rect);
        minSize = MinSize();

        if (horizontalFit == FitMode.PreferredSize && Mathf.Abs(minSize.x - animatedSize.x) > 1)
            animatedSize.x = Mathf.Lerp(animatedSize.x, minSize.x, Time.deltaTime * easingSpeed);

        if (verticalFit == FitMode.PreferredSize && Mathf.Abs(minSize.y - animatedSize.y) > 1)
            animatedSize.y = Mathf.Lerp(animatedSize.y, minSize.y, Time.deltaTime * easingSpeed);

        rect.SetSizeWithCurrentAnchors((RectTransform.Axis)1, animatedSize.y);
        rect.SetSizeWithCurrentAnchors(0, animatedSize.x);
    }

    private Vector2 MinSize()
    {
        Vector2 preferredSize = new(LayoutUtility.GetPreferredSize(rect, 0), LayoutUtility.GetPreferredSize(rect, 1));

        if (maxWidth > 0)
            preferredSize.x = Mathf.Clamp(preferredSize.x, 0, maxWidth);

        return preferredSize;
    }
}
