using UnityEngine;
using UnityEngine.Events;

namespace NSYNK.HyperSlides.UI
{
    public class UITogglePanel : MonoBehaviour
    {
        public UnityEvent<bool> OpenCloseEvents;

        public enum Direction
        {
            Left = 0,
            Right = 1
        }

        public Direction openingDirection = Direction.Left;
        public bool SetAsTopChildOnOpen = true;
        
        private RectTransform rectTransform;

        [SerializeField, ReadOnly]
        private bool isOpen = false;

        protected virtual void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            rectTransform.anchoredPosition = new Vector2(openingDirection == Direction.Left ? -rectTransform.sizeDelta.x : rectTransform.sizeDelta.x, rectTransform.anchoredPosition.y);
        }

        void Start()
        {
            OpenClose(true);
        }

        public void OpenClose() => OpenClose(false);

        void LateUpdate() => isOpen = Mathf.Approximately(rectTransform.anchoredPosition.x, openingDirection == Direction.Left ? -rectTransform.sizeDelta.x : rectTransform.sizeDelta.x);

        /// <summary>
        /// Opens or closes the slide overview panel
        /// </summary>
        public void OpenClose(bool forceClose = false)
        {
            bool wasOpen = forceClose || isOpen;

            Debug.Log($"Toggling panel {(wasOpen ? "closed" : "open")}");

            OpenCloseEvents?.Invoke(wasOpen);

            if (SetAsTopChildOnOpen && !wasOpen)
                transform.SetAsLastSibling();

            Vector2 anchoredPos = rectTransform.anchoredPosition;

            float openPosition = openingDirection == Direction.Left ? -rectTransform.sizeDelta.x : rectTransform.sizeDelta.x;
            float closedPosition = openingDirection == Direction.Left ? 0 : 0;

            this.AnimateFloat(this.transform, Easing.Ease.EaseInOutQuad, 0, 1, 0.5f, 0, update =>
            {
                anchoredPos.x = Mathf.Lerp(anchoredPos.x, wasOpen ? closedPosition : openPosition, update);
                rectTransform.anchoredPosition = anchoredPos;
            });
        }
    }
}