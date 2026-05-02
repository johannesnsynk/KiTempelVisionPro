using NSYNK.HyperSlides.Network;
using NSYNK.HyperSlides.Core;
using NSYNK.HyperSlides.Runtime;

using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using Unity.PolySpatial;
using Unity.PolySpatial.InputDevices;
using UnityEngine.InputSystem.XR;
using UnityEngine.EventSystems;

using UnityMainThreadDispatcher;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.InputSystem.EnhancedTouch;

namespace NSYNK.HyperSlides.Input
{
    public class XRInputManager : Singleton<XRInputManager>
    {
        public delegate void TouchPhaseState(TouchPhase touchPhase);
        public static TouchPhaseState onTouchUpdate;
        public static Vector3 inputUserPosition = new();
        public static Vector3 interactionPosition = new();
        public static Quaternion inputUserRotation = Quaternion.identity;

        public TrackedPoseDriver localPoseDriver;
        public Transform xrOrigin;

        public GameObject m_SelectedObject { get; private set; }
        public SpatialPointerState primaryTouchData { get; private set; }

        [ReadOnly]
        public bool IsTouching = false;

        private Vector2 swipeDirectionVector = new();
        private NakamaUserControls inputActions;
        private GameObject interactionVisualizer;

        public override void Awake()
        {
            base.Awake();

            inputActions = new NakamaUserControls();
            inputActions.Enable();

            if (UnityEngine.Debug.isDebugBuild)
                CreateInteractionVisualizer();

#if !UNITY_VISIONOS
            EnhancedTouchSupport.Enable();
#endif
        }

        /// <summary>
        /// Create a visual marker for the interaction position, to debug offsets
        /// </summary>
        private void CreateInteractionVisualizer()
        {
            interactionVisualizer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            interactionVisualizer.transform.localScale = Vector3.one * 0.05f;

            if (interactionVisualizer.TryGetComponent(out Collider col))
                col.enabled = false;
        }

        private void OnEnable()
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            float slideNextPrev = inputActions.IUser.NextPrev.ReadValue<float>();
            inputActions.IUser.NextPrev.performed += NextPrevSlide;
#endif

#if UNITY_IOS || UNITY_EDITOR
            inputActions.IUser.TouchSwipe.performed += TouchSwipe;
            inputActions.IUser.Touch.canceled += NextPrevSlideSwipe;
#endif
        }

        private void OnDisable()
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            inputActions.IUser.NextPrev.performed -= NextPrevSlide;
#endif

#if UNITY_IOS || UNITY_EDITOR
            inputActions.IUser.TouchSwipe.performed -= TouchSwipe;
            inputActions.IUser.Touch.canceled -= NextPrevSlideSwipe;
#endif
        }

        private void TouchSwipe(UnityEngine.InputSystem.InputAction.CallbackContext obj) => swipeDirectionVector = obj.ReadValue<Vector2>();

        /// <summary>
        /// Fire next or prev slide based on the keyboard input in editor or standalone
        /// </summary>
        /// <param name="obj"></param>
        private void NextPrevSlide(UnityEngine.InputSystem.InputAction.CallbackContext obj)
        {
            if (obj.ReadValue<float>() >= 1)
                XRSlideManager.Instance.NextSlide();
            else if (obj.ReadValue<float>() <= -1)
                XRSlideManager.Instance.PreviousSlide();
        }

        /// <summary>
        /// Handle swiping on touch devices to go back and forth in the presentation
        /// </summary>
        /// <param name="obj"></param>
        private void NextPrevSlideSwipe(UnityEngine.InputSystem.InputAction.CallbackContext obj)
        {
            Dispatcher.Enqueue(() =>
            {
                if (m_SelectedObject || EventSystem.current.IsPointerOverGameObject())
                    return;

                if (swipeDirectionVector.x <= -50)
                    XRSlideManager.Instance.NextSlide();
                else if (swipeDirectionVector.x >= 50)
                    XRSlideManager.Instance.PreviousSlide();
            });
        }

        void Update()
        {
            HandleStandaloneInput();
            HandleTouchInputs();
        }

        /// <summary>
        /// Handle standalone and editor inputs to move and look around
        /// </summary>
        private void HandleStandaloneInput()
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            Vector2 direction = inputActions.IUser.Move.ReadValue<Vector2>();
            Vector2 elevation = inputActions.IUser.Elevate.ReadValue<Vector2>();

            var inputUserRotationEuler = inputUserRotation.eulerAngles;

            inputUserPosition += Quaternion.AngleAxis(inputUserRotationEuler.y, Vector3.up) * new Vector3(direction.x, elevation.y, direction.y);

            //WASD Movement for testing
            if (XRNetworkManager.localPlayer && XRSlideManager.CurrentPresentation != null)
            {
                if (DeviceInfo.Role != XRPlayer.Role.Simulation && DeviceInfo.Role != XRPlayer.Role.Broadcast)
                {
                    var isRightClickPerformed = inputActions.IUser.RightMouseButton.phase == UnityEngine.InputSystem.InputActionPhase.Performed;
                    if (isRightClickPerformed)
                    {
                        float RotationX = 30.0f * inputActions.IUser.RotationX.ReadValue<float>() * Time.deltaTime;
                        float RotationY = 30.0f * inputActions.IUser.RotationY.ReadValue<float>() * Time.deltaTime;

                        inputUserRotationEuler.x -= RotationY;
                        inputUserRotationEuler.y += RotationX;

                        inputUserRotation = Quaternion.Euler(inputUserRotationEuler);
                    }

                    localPoseDriver.transform.SetPositionAndRotation(inputUserPosition, inputUserRotation);
                }
                else
                {
                    if (DeviceInfo.Role == XRPlayer.Role.Simulation)
                        inputUserPosition = xrOrigin.transform.position;
                    else if (DeviceInfo.Role == XRPlayer.Role.Broadcast)
                        inputUserPosition = XRCameraManager.Instance.mainCamera.transform.position;
                }
            }
#else
            inputUserPosition = inputActions.IUser.Position.ReadValue<Vector3>();
            inputUserPosition.y = 0;
#endif
        }

        /// <summary>
        /// Handle touch inputs and event calls
        /// </summary>
        private void HandleTouchInputs()
        {
            //Avoid any input handling for spectators and clients for now
            if (XRNetworkManager.localPlayer != null && DeviceInfo.Role < XRPlayer.Role.Participant)
                return;

            ReadOnlyArray<Touch> activeTouches = Touch.activeTouches;
            IsTouching = activeTouches.Count > 0;

            //One hand interaction
            if (activeTouches.Count > 0)
            {
                primaryTouchData = EnhancedSpatialPointerSupport.GetPointerState(activeTouches[0]);

                if (activeTouches[0].phase == TouchPhase.Began)
                {
                    m_SelectedObject = GetSelectedObject(activeTouches[0]);
                    onTouchUpdate?.Invoke(activeTouches[0].phase);
                }

                if (activeTouches[0].phase != TouchPhase.Canceled &&
                       activeTouches[0].phase != TouchPhase.Ended &&
                       activeTouches[0].phase != TouchPhase.None)
                {
                    interactionPosition = GetInteractionPosition(activeTouches[0]);

                    if (m_SelectedObject != null)
                        onTouchUpdate?.Invoke(activeTouches[0].phase);
                }

                if (activeTouches[0].phase == TouchPhase.Ended || activeTouches[0].phase == TouchPhase.Canceled)
                {
                    m_SelectedObject = null;
                    onTouchUpdate?.Invoke(activeTouches[0].phase);
                }
            }
            else
                m_SelectedObject = null;
        }

        /// <summary>
        /// Get the selected object based on touch raycast or device platform values
        /// </summary>
        /// <param name="touch"></param>
        /// <returns>The gameobject currently selected</returns>
        private GameObject GetSelectedObject(Touch touch)
        {
            GameObject hitObject = null;

#if UNITY_VISIONOS && !UNITY_EDITOR
            hitObject = primaryTouchData.targetObject;
#else
            Ray ray = Camera.main.ScreenPointToRay(touch.screenPosition);

            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
            {
                Debug.Log($"Hit object: {hit.collider.gameObject.name} at position: {hit.point}");
                if (hit.collider != null)
                    hitObject = hit.collider.gameObject;
            }

#endif

            return hitObject;
        }

        /// <summary>
        /// Get current interaction position based on touch raycast or device platform values
        /// </summary>
        /// <param name="touch"></param>
        /// <returns>The world position of the interaction</returns>
        private Vector3 GetInteractionPosition(Touch touch)
        {
            Vector3 hitPosition = Vector3.zero;

#if UNITY_VISIONOS && !UNITY_EDITOR
            hitPosition = primaryTouchData.interactionPosition;
#else
            Ray ray = Camera.main.ScreenPointToRay(touch.screenPosition);

            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
                if (hit.collider != null)
                return hit.point;
#endif
            if (interactionVisualizer)
                interactionVisualizer.transform.position = hitPosition;

            return hitPosition;
        }
    }
}