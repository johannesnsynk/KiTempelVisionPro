using System;
using System.Threading.Tasks;
using NSYNK.HyperSlides.Core;
using NSYNK.HyperSlides.Input;
using NSYNK.HyperSlides.Network;
using NSYNK.HyperSlides.Runtime;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NSYNK.HyperSlides.Interaction
{
    /// <summary>
    /// A component to mark objects as being in edit mode, which can be used to disable certain interactions while editing
    /// </summary>
    public class EditMode : Singleton<EditMode>
    {
        public UnityEvent<bool> toggleEditModeEvents;

        [ReadOnly]
        public bool IsInEditMode = false;
        public XRPresentation LastPresentation;
        public Transform RaycastHitIndicator;
        public GameObject ContentVisualizer;
        public LayerMask RaycastLayerMask;
        public TMPro.TextMeshProUGUI ScalingText;

        private float pinchStartScale = 1f;
        private float pinchStartYaw = 0f;
        private int lastSlideIndex = 1;
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private Vector3 initialPosition;
        private Vector2 initialTouchScreenPosition;
        private bool isDraggingRoot;
        private LineRenderer debugLineRenderer;
        private float debugLineHideTime;
        private CanvasGroup scalingTextCanvasGroup;
        private bool firstStart = true;

        private void Start()
        {
            DisableEditMode();
        }

        public void EnableEditMode()
        {
            Debug.Log("Enabling edit mode");
            ToggleEditMode(true);
        }

        public void DisableEditMode()
        {
            Debug.Log("Disabling edit mode");
            ToggleEditMode(false);
        }

        public void ToggleEditMode(bool force)
        {
            IsInEditMode = force;

            toggleEditModeEvents?.Invoke(IsInEditMode);

            if (ContentVisualizer != null)
                ContentVisualizer.SetActive(IsInEditMode);

            if (IsInEditMode)
            {
                LastPresentation = XRSlideManager.Instance.CurrentPresentation;
                lastSlideIndex = XRSlideManager.Instance.CurrentSlidePosition;

                XRSlideManager.Instance.Reset();
            }
            else
                XRSlideManager.Instance.SetPresentation(LastPresentation, lastSlideIndex);

            this.enabled = IsInEditMode;
        }

        private async void OnEnable()
        {
            SetVisualizerPosition();
            CheckPlayerPrefsForScaling();

            while (XRInputManager.Instance == null)
                await Task.Yield();

            XRInputManager.Instance.OnTouchUpdate += SetRootContent;
            XRInputManager.Instance.OnPinchZoom += SetRootContentZoom;
            XRInputManager.Instance.OnPinchRotate += SetRootContentRotation;
        }

        private void OnDisable()
        {
            if (debugLineRenderer != null)
                debugLineRenderer.enabled = false;

            if (scalingTextCanvasGroup != null)
                scalingTextCanvasGroup.gameObject.SetActive(false);

            if (XRInputManager.Instance != null)
            {
                XRInputManager.Instance.OnTouchUpdate -= SetRootContent;
                XRInputManager.Instance.OnPinchZoom -= SetRootContentZoom;
                XRInputManager.Instance.OnPinchRotate -= SetRootContentRotation;
            }
        }

        private void CheckPlayerPrefsForScaling()
        {
            float savedScale = PlayerPrefs.GetFloat("ContentScale", 1f);
            XRContentRoot.Instance.transform.localScale = Vector3.one * savedScale;

            SetScalingText(savedScale);
        }

        private void SetScalingText(float scale)
        {
            if (scalingTextCanvasGroup == null)
                scalingTextCanvasGroup = ScalingText.GetComponentInParent<CanvasGroup>();

            scalingTextCanvasGroup.gameObject.SetActive(true);

            scalingTextCanvasGroup.alpha = 1f;
            ScalingText.text = $"{scale * 100f:0}%";
        }

        /// <summary>
        /// A method to reset the position of the content root to be in front of the user
        /// </summary>
        public void ResetRootContentToView()
        {
            //Raycast from camera towards planes
            if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out RaycastHit hit, 10f, RaycastLayerMask))
            {
                targetPosition = hit.point;
                targetRotation = Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y, 0f);

                SetRaycastHitIndicator(hit.point, hit.normal);
            }
            else
            {
                targetPosition = Camera.main.transform.position + Camera.main.transform.forward * 2f;
                targetPosition.y = 0;
                targetRotation = Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y, 0f);

                RaycastHitIndicator.gameObject.SetActive(false);
            }

        }

        /// <summary> 
        /// Sets the scale of the content root based on pinch zoom input, allowing the user to resize the content in edit mode 
        /// </summary>
        /// <param name="pinchValue">The value of the pinch zoom input, where 0 is the start of the pinch and positive/negative values indicate the distance pinched in or out since the start</param>
        private void SetRootContentZoom(float pinchValue)
        {
            if (Mathf.Approximately(pinchValue, 0f))
                pinchStartScale = XRContentRoot.Instance.transform.localScale.x;

            float targetZoom = pinchStartScale + pinchValue * 0.001f;
            targetZoom = Mathf.Clamp(targetZoom, 0.1f, 1f);

            Vector3 targetScale = Vector3.one * targetZoom;
            XRContentRoot.Instance.transform.localScale = targetScale;
            PlayerPrefs.SetFloat("ContentScale", targetZoom);

            SetScalingText(targetZoom);
        }

        /// <summary>
        /// Set the position of the content visualizer object
        /// </summary>
        private void SetVisualizerPosition()
        {
            if (ContentVisualizer != null)
                ContentVisualizer.SetActive(IsInEditMode);
            else if (HyperSlidesStateManager.Instance.Settings.PlacementIndicatorPrefab)
                ContentVisualizer = Instantiate(HyperSlidesStateManager.Instance.Settings.PlacementIndicatorPrefab);
            else
                return;

            ContentVisualizer.transform.SetParent(XRContentRoot.Instance.transform, false);
        }

        /// <summary>
        /// A method to set the position of the content root based on touch input, allowing the user to drag the content around in edit mode. The method raycasts down from the desired position to place the content on the ground if possible.
        /// </summary>
        /// <param name="phase">The current touch phase</param>
        private void SetRootContent(UnityEngine.InputSystem.TouchPhase phase)
        {
            // Don't process touch input if it's over a UI element
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            bool shouldDispatchTouchUpdate = XRInputManager.Instance.selectedObject != null || IsInEditMode;

            if (!shouldDispatchTouchUpdate)
                return;

            if (phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                targetPosition = XRContentRoot.Instance.transform.position;
                initialPosition = XRContentRoot.Instance.transform.position;
                initialTouchScreenPosition = XRInputManager.Instance.PrimaryTouchScreenPosition;
                isDraggingRoot = true;
                return;
            }

            if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                isDraggingRoot = false;
                return;
            }

            if (!isDraggingRoot)
                return;

            RaycastHitIndicator.gameObject.SetActive(false);

            Vector2 screenDelta = XRInputManager.Instance.PrimaryTouchScreenPosition - initialTouchScreenPosition;
            Vector3 worldRight = Camera.main.transform.right;
            Vector3 worldForward = Camera.main.transform.forward;

            worldRight.y = 0f;
            worldForward.y = 0f;

            // Normalize the world right and forward vectors to ensure consistent movement speed regardless of their original magnitude
            worldRight = worldRight.sqrMagnitude > 0f ? worldRight.normalized : Vector3.right;
            worldForward = worldForward.sqrMagnitude > 0f ? worldForward.normalized : Vector3.forward;

            Vector3 screenOffset = (worldRight * screenDelta.x + worldForward * screenDelta.y) * 0.0025f;
            Vector3 desiredPosition = initialPosition + screenOffset;
            desiredPosition.y = targetPosition.y;

            if (GetRaycastHit(desiredPosition, out Vector3 hitPosition))
                targetPosition = hitPosition;
            else
                targetPosition = desiredPosition;
        }

        /// <summary>
        /// A method to set the rotation of the content root based on pinch rotate input
        /// </summary>
        /// <param name="angle"></param>
        private void SetRootContentRotation(float angle)
        {
            if (Mathf.Approximately(angle, 0f))
                pinchStartYaw = XRContentRoot.Instance.transform.eulerAngles.y;

            float targetYaw = pinchStartYaw - angle * 180f;
            targetRotation = Quaternion.Euler(0f, targetYaw, 0f);
        }

        /// <summary>
        /// A method to raycast down from a given position and return the hit point, used to move the content root on the ground when dragging in edit mode
        /// </summary> <param name="checkVector">The position to raycast down from</param> 
        /// <param name="hitPosition">The position of the raycast hit, or the original position if no hit was found</param>
        /// <returns>Whether a hit was found</returns>
        private bool GetRaycastHit(Vector3 checkVector, out Vector3 hitPosition)
        {
            const float rayStartOffset = 5f;
            const float rayDistance = 20f;

            Ray ray = new Ray(checkVector + Vector3.up * rayStartOffset, Vector3.down);

            if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, RaycastLayerMask))
            {
                hitPosition = hit.point;
                return true;
            }
            else
            {
                hitPosition = XRContentRoot.Instance.transform.position;
                return false;
            }
        }

        private void SetRaycastHitIndicator(Vector3 position, Vector3 normal)
        {
            RaycastHitIndicator.gameObject.SetActive(true);
            RaycastHitIndicator.position = position;
            RaycastHitIndicator.rotation = XRContentRoot.Instance.transform.rotation;
            RaycastHitIndicator.localScale = XRContentRoot.Instance.transform.localScale;
        }

        private void DrawDebugLine(Vector3 start, Vector3 end, Color color, float duration = 0f)
        {
            if (debugLineRenderer == null)
            {
                GameObject lineObject = new GameObject("EditModeDebugLine");
                lineObject.transform.SetParent(transform, false);

                debugLineRenderer = lineObject.AddComponent<LineRenderer>();
                debugLineRenderer.useWorldSpace = true;
                debugLineRenderer.positionCount = 2;
                debugLineRenderer.startWidth = 0.01f;
                debugLineRenderer.endWidth = 0.01f;
                debugLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                debugLineRenderer.enabled = false;
            }

            debugLineRenderer.startColor = color;
            debugLineRenderer.endColor = color;
            debugLineRenderer.SetPosition(0, start);
            debugLineRenderer.SetPosition(1, end);
            debugLineRenderer.enabled = true;

            debugLineHideTime = duration > 0f ? Time.time + duration : Time.time + Time.deltaTime;
        }

        void Update()
        {
            XRContentRoot.Instance.transform.SetPositionAndRotation(
                Vector3.Lerp(XRContentRoot.Instance.transform.position, targetPosition, Time.deltaTime * 10f),
                Quaternion.Slerp(XRContentRoot.Instance.transform.rotation, targetRotation, Time.deltaTime * 10f)
            );

            if (RaycastHitIndicator.gameObject.activeSelf)
                RaycastHitIndicator.rotation = Quaternion.Slerp(RaycastHitIndicator.rotation, XRContentRoot.Instance.transform.rotation, Time.deltaTime * 10f);

            if (debugLineRenderer != null && debugLineRenderer.enabled && Time.time >= debugLineHideTime)
                debugLineRenderer.enabled = false;

            if (scalingTextCanvasGroup != null)
                scalingTextCanvasGroup.alpha = Mathf.Lerp(scalingTextCanvasGroup.alpha, 0f, Time.deltaTime * 2f);
        }
    }
}