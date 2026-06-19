using NSYNK.HyperSlides.Input;
using NSYNK.HyperSlides.Network;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NSYNK.HyperSlides.UI
{
    public class XRUISlider : MonoBehaviour
    {
        public bool interactable = true;
        public bool showValueLabel = true;
        [ReadOnly]
        public float value = 0;

        public UnityEvent<float> sliderEvent;

        [HideInInspector]
        public UnityEvent<float> onValueChanged;
        [HideInInspector]
        public UnityEvent<UnityEngine.InputSystem.TouchPhase> onTouchPhaseChanged;

        [SerializeField]
        private MeshRenderer fillRenderer;
        [SerializeField]
        private TMPro.TextMeshPro valueLabel;
        private float boxColliderSizeX;
        protected float newValue, oldValue;
        private bool animating = false;
        private bool interacting = false;

        protected void Start()
        {
            boxColliderSizeX = GetComponent<BoxCollider>().size.x;
        }

        protected void OnEnable()
        {
            if (XRInputManager.Instance != null)
                XRInputManager.Instance.OnTouchUpdate += HandleTouchSlider;
        }
        protected void OnDisable()
        {
            if (XRInputManager.Instance != null)
                XRInputManager.Instance.OnTouchUpdate -= HandleTouchSlider;
        }

        protected void HandleTouchSlider(UnityEngine.InputSystem.TouchPhase touchPhase)
        {
            if (!interactable || animating)
                return;

            if (XRInputManager.Instance.selectedObject == gameObject)
            {
                interacting = true;
                Press(XRInputManager.Instance.InteractionPosition);
                onTouchPhaseChanged?.Invoke(touchPhase);
            }
            else
            {
                interacting = false;
                onTouchPhaseChanged?.Invoke(touchPhase);
            }
        }

        public void AnimateSlider(float start, float finish, float duration)
        {
            animating = true;

            newValue = oldValue = start;
            onValueChanged?.Invoke(start);

            fillRenderer.material.SetFloat("_Percentage", 1 - Mathf.Clamp(value, 0.0f, 1.0f));

            this.AnimateFloat(transform, Easing.Ease.EaseInOutQuad, start, finish, duration, 0, (float progress) =>
            {
                oldValue = newValue = progress;
                onValueChanged?.Invoke(progress);
            },
            () =>
            {
                oldValue = newValue = finish;
                onValueChanged?.Invoke(finish);
                animating = false;
            });
        }

        /// <summary>
        /// When user is actually pressing on the mesh
        /// </summary>
        /// <param name="position"></param>
        public void Press(Vector3 position)
        {
            var localPosition = transform.InverseTransformPoint(position);
            float boxValue = localPosition.x / boxColliderSizeX + 0.5f;

            oldValue = value;
            newValue = 1 - boxValue;
            onValueChanged?.Invoke(newValue);
        }

        /// <summary>
        /// Network called slider update
        /// </summary>
        /// <param name="networkValue"></param>
        public void UpdateSlider(float networkValue)
        {
            if (!interacting && !animating)
            {
                oldValue = value;
                newValue = networkValue;
            }
        }

        /// <summary>
        /// Update the textmesh textlabel
        /// </summary>
        protected void UpdateValueLabel()
        {
            if (valueLabel)
            {
                valueLabel.gameObject.SetActive(showValueLabel);

                if (showValueLabel)
                    valueLabel.text = value.ToString("F2");
            }
        }

        protected void Update()
        {
            newValue = Mathf.Clamp01(newValue);
            value = Mathf.Lerp(oldValue, newValue, Time.deltaTime * HyperSlidesStateManager.Instance.Settings.syncedValueEasing);

            fillRenderer.material.SetFloat("_Percentage", 1 - Mathf.Clamp(value, 0.0f, 1.0f));

            sliderEvent?.Invoke(value);

            UpdateValueLabel();
        }
    }
}