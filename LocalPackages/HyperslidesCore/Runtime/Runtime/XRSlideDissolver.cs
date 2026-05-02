namespace NSYNK.HyperSlides.Runtime
{
    public class XRSlideDissolver : XRSlideDissolveComponent
    {
        public override void OnDissolveChanged(float dissolveNormalized, float dissolveInOut)
        {
            visibilityStateEvents.ForEach(e => {
                if (e.visibilityState == visibilityState)
                    e.unityEvent.Invoke(dissolveNormalized);
            });
        }

        public override void OnVisibilityStateChanged(XRSlideElement.VisibilityState state)
        {
            visibilityState = state;
        }
    }
}