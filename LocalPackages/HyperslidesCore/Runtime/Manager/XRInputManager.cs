using NSYNK.HyperSlides.Network;
using NSYNK.HyperSlides.Core;
using NSYNK.HyperSlides.Runtime;

using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
#if UNITY_VISIONOS
using Unity.PolySpatial.InputDevices;
#endif
using UnityEngine.InputSystem.XR;
using UnityEngine.EventSystems;

using UnityMainThreadDispatcher;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.InputSystem.EnhancedTouch;
using System;
using NSYNK.HyperSlides.Interaction;

namespace NSYNK.HyperSlides.Input
{
    /// <summary>
    /// Manager for handling XR input across devices
    /// </summary>
    public class XRInputManager : Singleton<XRInputManager>
    {
        private const float MOVEMENT_SPEED = 30.0f;
        private const float PLAYER_HEIGHT_OFFSET = 1.6f;
        private const float SWIPE_THRESHOLD = 50f;
        private const float INTERACTION_VISUALIZER_SCALE = 0.05f;
        private const float MAX_PITCH_ANGLE = 89f;
        private const float MIN_PITCH_ANGLE = 271f;

        public Action<TouchPhase> OnTouchUpdate;
        public Action<float> OnPinchZoom;
        public Action<float> OnPinchRotate;
        public Vector3 InputUserPosition = new();
        public Vector3 InteractionPosition = new();
        public Vector2 PrimaryTouchScreenPosition { get; private set; }
        public Quaternion InputUserRotation = Quaternion.identity;

        public TrackedPoseDriver LocalPoseDriver;
        public Transform XROrigin;
        public Material IntertactionMaterial;

        public GameObject SelectedObject { get; private set; }
        [Obsolete("Use SelectedObject instead")]
        public GameObject selectedObject => SelectedObject;
#if UNITY_VISIONOS
        public SpatialPointerState primaryTouchData { get; private set; }
#endif

        [ReadOnly]
        public bool IsTouching = false;

        private Vector2 swipeDirectionVector = new();
        private NakamaUserControls inputActions;
        private GameObject interactionVisualizer;
        private ReadOnlyArray<Touch> activeTouches;
        private Touch primaryTouch, secondaryTouch;

        private bool isPinching;
        private float initialPinchDistance;
        private Vector2 previousPinchDirection;
        private float accumulatedPinchRotation;
        private float currentPinchDistance;

        protected override void OnSingletonAwake()
        {
            inputActions = new NakamaUserControls();
            inputActions.Enable();

            if (UnityEngine.Debug.isDebugBuild)
                CreateInteractionVisualizer();

#if UNITY_VISIONOS || UNITY_IOS
            EnhancedTouchSupport.Enable();
#endif
        }

        /// <summary>
        /// Create a visual marker for the interaction position, to debug offsets
        /// </summary>
        private void CreateInteractionVisualizer()
        {
            interactionVisualizer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            interactionVisualizer.name = "Interaction Visualizer";
            interactionVisualizer.GetComponent<MeshRenderer>().material = IntertactionMaterial;
            interactionVisualizer.transform.localScale = Vector3.one * INTERACTION_VISUALIZER_SCALE;

            if (interactionVisualizer.TryGetComponent(out Collider col))
                col.enabled = false;
        }

        /// <summary>
        /// Subscribe to input events based on platform. For standalone and editor, subscribe to keyboard inputs for next/prev slide. For iOS, subscribe to touch swipe inputs for next/prev slide navigation.
        /// </summary>
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

        /// <summary>
        /// Unsubscribe from input events.
        /// </summary>
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

        /// <summary>
        /// Handle touch swipe input to determine the direction of the swipe for slide navigation on touch devices.
        /// </summary>
        /// <param name="obj"></param>
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
                if (SelectedObject || EventSystem.current.IsPointerOverGameObject())
                    return;

                if (swipeDirectionVector.x <= -SWIPE_THRESHOLD)
                    XRSlideManager.Instance.NextSlide();
                else if (swipeDirectionVector.x >= SWIPE_THRESHOLD)
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
            if (XRSlideManager.Instance == null)
                return;

            // Access directly when needed (once per frame)
            var localPlayer = XRNetworkManager.Instance?.LocalPlayer;
            var currentPresentation = XRSlideManager.Instance.CurrentPresentation;

            if (localPlayer && currentPresentation != null)
            {
                Vector2 direction = inputActions.IUser.Move.ReadValue<Vector2>();
                Vector2 elevation = inputActions.IUser.Elevate.ReadValue<Vector2>();

                var inputUserRotationEuler = InputUserRotation.eulerAngles;

                InputUserPosition += Quaternion.AngleAxis(inputUserRotationEuler.y, Vector3.up) * new Vector3(direction.x, elevation.y, direction.y);

                //WASD Movement for testing
                if (DeviceInfo.Instance.Role != XRPlayer.Role.Simulation && DeviceInfo.Instance.Role != XRPlayer.Role.Broadcast)
                {
                    bool shouldRotate = false;

#if UNITY_EDITOR
                    // In editor, support both right mouse button and two-finger touch for rotation
                    ReadOnlyArray<Touch> activeTouches = Touch.activeTouches;
                    if (activeTouches.Count >= 2 && (EditMode.Instance && !EditMode.Instance.IsInEditMode || !EditMode.Instance)) // Only allow touch rotation when not in edit mode to avoid conflicts with content manipulation
                    {
                        // Two-finger rotation when touch simulation is active
                        Vector2 touch1Delta = activeTouches[0].delta;
                        Vector2 touch2Delta = activeTouches[1].delta;
                        Vector2 averageDelta = (touch1Delta + touch2Delta) * 0.5f;

                        float RotationX = MOVEMENT_SPEED * averageDelta.x * Time.deltaTime;
                        float RotationY = MOVEMENT_SPEED * averageDelta.y * Time.deltaTime;

                        inputUserRotationEuler = ClampCameraRotation(inputUserRotationEuler, RotationX, RotationY);
                        InputUserRotation = Quaternion.Euler(inputUserRotationEuler);
                        shouldRotate = true;
                    }
#endif

                    var isRightClickPerformed = inputActions.IUser.RightMouseButton.phase == UnityEngine.InputSystem.InputActionPhase.Performed;
                    if (!shouldRotate && isRightClickPerformed)
                    {
                        float RotationX = MOVEMENT_SPEED * inputActions.IUser.RotationX.ReadValue<float>() * Time.deltaTime;
                        float RotationY = MOVEMENT_SPEED * inputActions.IUser.RotationY.ReadValue<float>() * Time.deltaTime;

                        inputUserRotationEuler = ClampCameraRotation(inputUserRotationEuler, RotationX, RotationY);

                        InputUserRotation = Quaternion.Euler(inputUserRotationEuler);
                    }

                    LocalPoseDriver.transform.SetPositionAndRotation(InputUserPosition + new Vector3(0, PLAYER_HEIGHT_OFFSET, 0), InputUserRotation);
                }
                else
                {
                    if (DeviceInfo.Instance.Role == XRPlayer.Role.Simulation)
                        InputUserPosition = XROrigin.transform.position;
                    else if (DeviceInfo.Instance.Role == XRPlayer.Role.Broadcast)
                        InputUserPosition = XRCameraManager.Instance.mainCamera.transform.position;
                }
            }
            else
            {
                LocalPoseDriver.transform.SetPositionAndRotation(new Vector3(0, PLAYER_HEIGHT_OFFSET, 0), Quaternion.identity);
            }
#else
        InputUserPosition = inputActions.IUser.Position.ReadValue<Vector3>();
        InputUserPosition.y = 0;
#endif
        }

        /// <summary>
        /// Check if the local player can receive input based on their role
        /// </summary>
        /// <returns></returns>
        private bool CanReceiveInput()
        {
            return DeviceInfo.Instance.Role >= XRPlayer.Role.Participant;
        }

        /// <summary>
        /// Handle touch inputs and event calls
        /// </summary>
        private void HandleTouchInputs()
        {
            HyperSlidesStateManager.Instance.ShowVersionOverlay = false;

            if (!CanReceiveInput())
                return;

            activeTouches = Touch.activeTouches;
            IsTouching = activeTouches.Count > 0;

            if (!IsTouching)
            {
                isPinching = false;
                currentPinchDistance = 0f;

                if (SelectedObject != null)
                {
                    SelectedObject = null;
                    OnTouchUpdate?.Invoke(TouchPhase.Ended);
                }
                return;
            }

            primaryTouch = activeTouches[0];

#if UNITY_IOS
            HyperSlidesStateManager.Instance.ShowVersionOverlay = activeTouches.Count == 5;

            if (activeTouches.Count > 1)
            {
                HandleTwoFingerInteraction();
                return;
            }

            if (isPinching)
                return;
#endif

#if UNITY_VISIONOS
            primaryTouchData = EnhancedSpatialPointerSupport.GetPointerState(primaryTouch);
#endif

            switch (primaryTouch.phase)
            {
                case TouchPhase.Began:
                    HandleTouchBegan(primaryTouch);
                    break;
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    HandleTouchContinued(primaryTouch);
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    HandleTouchEnded(primaryTouch);
                    break;
            }

#if UNITY_IOS
            if (activeTouches.Count == 0)
                isPinching = false;
#endif
        }

        private void HandleTwoFingerInteraction()
        {
            if (activeTouches.Count > 1)
            {
                secondaryTouch = activeTouches[1];

                float absolutePinchDistance = Vector2.Distance(primaryTouch.screenPosition, secondaryTouch.screenPosition);

                bool initialPinch = !isPinching || primaryTouch.phase == TouchPhase.Began || secondaryTouch.phase == TouchPhase.Began;

                if (initialPinch)
                {
                    initialPinchDistance = absolutePinchDistance;
                    previousPinchDirection = (secondaryTouch.screenPosition - primaryTouch.screenPosition).normalized;
                    accumulatedPinchRotation = 0f;
                    currentPinchDistance = 0f;
                    isPinching = true;
                }
                else
                {
                    currentPinchDistance = absolutePinchDistance - initialPinchDistance;

                    Vector2 currentPinchDirection = (secondaryTouch.screenPosition - primaryTouch.screenPosition).normalized;
                    float deltaRotation = Vector2.SignedAngle(previousPinchDirection, currentPinchDirection);
                    accumulatedPinchRotation += deltaRotation;
                    previousPinchDirection = currentPinchDirection;
                }

                float normalizedRotation = Mathf.Repeat(accumulatedPinchRotation, 360f) / 180f;
                if (normalizedRotation > 1f)
                    normalizedRotation -= 2f;

                OnPinchZoom?.Invoke(currentPinchDistance);
                OnPinchRotate?.Invoke(normalizedRotation);

                return;
            }

            currentPinchDistance = 0f;
            accumulatedPinchRotation = 0f;
        }

        private void HandleTouchBegan(Touch touch)
        {
            PrimaryTouchScreenPosition = touch.screenPosition;
            SelectedObject = GetTouchedObject(touch);
            InteractionPosition = GetInteractionWorldPosition(touch);
            UpdateInteractionVisualizer(InteractionPosition);
            OnTouchUpdate?.Invoke(touch.phase);
        }

        private void HandleTouchContinued(Touch touch)
        {
            PrimaryTouchScreenPosition = touch.screenPosition;
            InteractionPosition = GetInteractionWorldPosition(touch);
            UpdateInteractionVisualizer(InteractionPosition);
            OnTouchUpdate?.Invoke(touch.phase);
        }

        private void HandleTouchEnded(Touch touch)
        {
            SelectedObject = null;
            OnTouchUpdate?.Invoke(touch.phase);
        }

        /// <summary>
        /// Get the world position of the touch interaction
        /// </summary>
        /// <param name="touch">The touch input</param>
        /// <returns>The world position of the interaction</returns>
        private Vector3 GetInteractionWorldPosition(Touch touch)
        {
#if UNITY_VISIONOS
            return primaryTouchData.interactionPosition;
#else
            Ray ray = Camera.main.ScreenPointToRay(touch.screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
            {
                // Ignore hits on the object we're currently grabbing to prevent it moving towards player
                if (SelectedObject != null && hit.collider.gameObject == SelectedObject)
                    return ray.origin + ray.direction * 1.5f;

                return hit.point;
            }
            else
            {
                return ray.origin + ray.direction * 1.5f; // Default distance when no hit
            }
#endif
        }

        /// <summary>
        /// Check if the primary touch is currently over a UI element to prevent interactions with the scene when trying to interact with the UI.
        /// </summary>
        /// <returns>True if the primary touch is over a UI element, false otherwise.</returns>
        public bool IsPointerOverUI()
        {
            if (EventSystem.current.IsPointerOverGameObject())
                return true;

            for (int i = 0; i < Touch.activeTouches.Count; i++)
            {
                if (EventSystem.current.IsPointerOverGameObject(Touch.activeTouches[i].touchId))
                    return true;
            }

            return false;
        }


        /// <summary>
        /// Get the touched object based on platform
        /// </summary>
        /// <param name="touch">The touch input</param>
        /// <returns>The GameObject that was touched</returns>
        private GameObject GetTouchedObject(Touch touch)
        {
#if UNITY_VISIONOS
            return primaryTouchData.targetObject;
#else
            Ray ray = Camera.main.ScreenPointToRay(touch.screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
            {
                return hit.collider.gameObject;
            }
            return null;
#endif
        }

        /// <summary>
        /// Clamp camera rotation to avoid flipping when looking too far up or down
        /// </summary>
        /// <param name="currentRotation">The current rotation of the camera</param>
        /// <param name="deltaX">The change in rotation around the X axis</param>
        /// <param name="deltaY">The change in rotation around the Y axis</param>
        /// <returns>The clamped rotation vector</returns>
        private Vector3 ClampCameraRotation(Vector3 currentRotation, float deltaX, float deltaY)
        {
            currentRotation.x -= deltaY;
            currentRotation.y += deltaX;

            // Clamp pitch to avoid camera flipping
            currentRotation.x = Mathf.Repeat(currentRotation.x + 360f, 360f);
            currentRotation.x = currentRotation.x > 180f
                ? Mathf.Max(currentRotation.x, MIN_PITCH_ANGLE)
                : Mathf.Min(currentRotation.x, MAX_PITCH_ANGLE);

            return currentRotation;
        }

        /// <summary>
        /// Update the debug visualizer position
        /// </summary>
        /// <param name="position">The world position to move the visualizer to</param>
        private void UpdateInteractionVisualizer(Vector3 position)
        {
            if (interactionVisualizer != null)
                interactionVisualizer.transform.position = position;
        }
    }
}