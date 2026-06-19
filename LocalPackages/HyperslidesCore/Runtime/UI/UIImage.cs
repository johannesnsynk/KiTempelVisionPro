using UnityEngine;

namespace NSYNK.HyperSlides.UI
{
    public class UIImage : MonoBehaviour
    {
        private RectTransform rectTransform;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        public void FlipImageAnimated(bool flip)
        {
            if (rectTransform == null)
                return;

            float targetRotationZ = flip ? 0f : 180f;

            this.AnimateFloat(this.transform, Easing.Ease.EaseInOutQuad, 0, 1, 0.5f, 0, update =>
            {
                Vector3 currentRotation = rectTransform.localEulerAngles;
                currentRotation.y = Mathf.LerpAngle(currentRotation.y, targetRotationZ, update);
                rectTransform.localRotation = Quaternion.Euler(currentRotation);
            });
        }
    }
}