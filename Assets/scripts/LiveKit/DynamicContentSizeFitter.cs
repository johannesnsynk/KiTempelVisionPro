using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A content size fitter that is animating the new size instead of setting it directly
/// </summary>
public class DynamicContentSizeFitter : ContentSizeFitter
{
    public float maxWidth = 0;

    private float easingSpeed = 15;

    private Vector2 minSize;
    private float animatedHeight = 0;
    private float animatedWidth = 0;
    private RectTransform rect;
    private LayoutGroup layoutGroup;

    protected override void Awake()
    {
        base.Awake();

        rect = GetComponent<RectTransform>();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        layoutGroup = GetComponent<LayoutGroup>();

        base.OnValidate();
    }
#endif

    public override void SetLayoutHorizontal() { }
    public override void SetLayoutVertical() { }

    /// <summary>
    /// Double check for the fitmodes, so we avoid recalculating if not needed and save some performance (you never know)
    /// </summary>
    private void Update()
    {
        minSize = MinSize();

        if (verticalFit == FitMode.PreferredSize && !Mathf.Approximately(minSize.y, animatedHeight))
        {
            animatedHeight = Mathf.Lerp(animatedHeight, minSize.y, Time.deltaTime * easingSpeed);

            LayoutRebuilder.MarkLayoutForRebuild(rect);
            rect.SetSizeWithCurrentAnchors((RectTransform.Axis)1, animatedHeight);
        }

        if (horizontalFit == FitMode.PreferredSize && !Mathf.Approximately(minSize.x, animatedWidth))
        {
            animatedWidth = Mathf.Lerp(animatedWidth, minSize.x, Time.deltaTime * easingSpeed);

            LayoutRebuilder.MarkLayoutForRebuild(rect);
            rect.SetSizeWithCurrentAnchors((RectTransform.Axis)0, animatedWidth);
        }
    }

    private Vector2 MinSize()
    {
        Vector2 preferredSize = new(LayoutUtility.GetPreferredSize(rect, 0), LayoutUtility.GetPreferredSize(rect, 1));

        if (maxWidth > 0)
            preferredSize.x = Mathf.Clamp(preferredSize.x, 0, maxWidth);

        //if (layoutGroup)
        //{
        //    preferredSize.x = Mathf.Clamp(preferredSize.x, (layoutGroup.padding.top + layoutGroup.padding.bottom) + 1, Mathf.Infinity);
        //    preferredSize.y = Mathf.Clamp(preferredSize.y, (layoutGroup.padding.left + layoutGroup.padding.right) + 1, Mathf.Infinity);
        //}
        //else
        //{
        //    preferredSize.x = Mathf.Clamp(preferredSize.x, 1, Mathf.Infinity);
        //    preferredSize.y = Mathf.Clamp(preferredSize.y, 1, Mathf.Infinity);
        //}

        return preferredSize;
    }
}
