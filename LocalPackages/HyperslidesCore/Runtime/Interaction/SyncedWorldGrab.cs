using NSYNK.HyperSlides.Input;
using NSYNK.HyperSlides.Runtime;
using NSYNK.HyperSlides.XR;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace NSYNK.HyperSlides.Network
{
    public class SyncedWorldGrab : NetworkSynced
    {
        /// <summary>
        /// The object that is grabbed by the user. If null, the transform of this component will be used.
        /// </summary>
        public Transform grabbedObject;

        /// <summary>
        /// If true, the grabbed object will keep looking at the user's position.
        /// This is useful for objects that should always face the user, like UI elements or interactive objects.
        /// </summary>
        public bool lookAtUserPosition = true;

        /// <summary>
        /// The position where the interaction is taking place, used to calculate offsets for movement.
        /// </summary>
        private Vector3 interactionPosition;

        /// <summary>
        /// The offset in position and rotation from the interaction position to the grabbed object.
        /// </summary>
        private Vector3 positionOffset;

        private bool isGrabbed = false;
        private Canvas foundCanvas;

        public override void OnValidate()
        {
            base.OnValidate();

            if (grabbedObject == null)
                grabbedObject = transform;
        }

        public override void OnEnable()
        {
            base.OnEnable();
            XRInputManager.onTouchUpdate += HandeChangeTouchPhase;

            CheckForCollider();
        }

        public override void OnDisable()
        {
            base.OnDisable();
            XRInputManager.onTouchUpdate -= HandeChangeTouchPhase;
        }

        private void CheckForCollider()
        {
            if (grabbedObject.GetComponent<Collider>() == null)
            {
                BoxCollider newBoxCollider = grabbedObject.gameObject.AddComponent<BoxCollider>();
                foundCanvas = grabbedObject.GetComponentInParent<Canvas>();

                if (foundCanvas != null && grabbedObject != transform)
                    newBoxCollider.size = grabbedObject.localScale * 100;
                else
                    newBoxCollider.size = grabbedObject.localScale;
            }
        }

        /// <summary>
        /// Handles the change in touch phase for the primary touch input.
        /// </summary>
        /// <param name="touchPhase"></param>
        private void HandeChangeTouchPhase(TouchPhase touchPhase)
        {
            if (XRInputManager.Instance.m_SelectedObject == grabbedObject.gameObject)
            {
                isGrabbed =
                    touchPhase != TouchPhase.None &&
                    (
                        touchPhase == TouchPhase.Began ||
                        touchPhase == TouchPhase.Moved ||
                        touchPhase == TouchPhase.Stationary
                    );

                switch (touchPhase)
                {
                    case TouchPhase.Began:
                        PrepareRelativeOffsets();
                        break;
                    case TouchPhase.Moved:
                        Move();
                        break;
                    case TouchPhase.Stationary:
                        Move();
                        break;
                }
            }
        }

        /// <summary>
        /// Prepares the relative offsets for position and rotation based on the initial transform and interaction position.
        /// </summary>
        private void PrepareRelativeOffsets()
        {
            interactionPosition = XRInputManager.Instance.primaryTouchData.interactionPosition;

            Quaternion inverseDeviceRotation = Quaternion.Inverse(XRInputManager.Instance.primaryTouchData.inputDeviceRotation);
            positionOffset = inverseDeviceRotation * (transform.position - interactionPosition);
        }

        /// <summary>
        /// Moves the object based on the current touch input and updates its network synced transform.
        /// </summary>
        private void Move()
        {
            if (!isGrabbed || grabbedObject == null) return;

            Vector3 desiredGrabbedWorldPos = XRInputManager.Instance.primaryTouchData.interactionPosition;

            Quaternion rotation = transform.rotation;
            if (lookAtUserPosition)
            {
                Vector3 userPosition = XRTrackedUser.Instance.Position();
                Vector3 directionToUser = (userPosition - transform.position).normalized;
                rotation = Quaternion.LookRotation(directionToUser, Vector3.up);

                if (foundCanvas)
                    rotation *= Quaternion.Euler(0, 180, 0);
            }

            Vector3 grabbedLocalOffset = grabbedObject.position - transform.position;
            Vector3 rotatedOffset = rotation * (Quaternion.Inverse(transform.rotation) * grabbedLocalOffset);
            Vector3 parentWorldPos = desiredGrabbedWorldPos - rotatedOffset;

            Quaternion localRotation = Quaternion.Inverse(XRContentRoot.Instance.transform.rotation) * rotation;

            UpdateTransform(
                transform.parent != null ? transform.parent.InverseTransformPoint(parentWorldPos) : parentWorldPos,
                localRotation,
                transform.localScale);
        }
    }
}