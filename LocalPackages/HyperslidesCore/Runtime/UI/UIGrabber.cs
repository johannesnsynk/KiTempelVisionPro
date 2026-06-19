using System.Collections;
using System.Collections.Generic;
using NSYNK.HyperSlides.Input;
using NSYNK.HyperSlides.XR;
using UnityEngine;

using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace NSYNK.HyperSlides.UI
{
    public class UIGrabber : MonoBehaviour
    {
        public GameObject Root;
        bool isActive = false;

        Transform rootTransform;
        Vector3 interactionPosition;
        Vector3 positionOffset;
        Quaternion rotationOffset;

        void OnEnable()
        {
            XRInputManager.Instance.OnTouchUpdate += HandeChangeTouchPhase;
        }

        void OnDisable()
        {
            if (XRInputManager.Instance)
                XRInputManager.Instance.OnTouchUpdate -= HandeChangeTouchPhase;
        }

        void HandeChangeTouchPhase(TouchPhase touchPhase)
        {
            if (touchPhase == TouchPhase.Began && XRInputManager.Instance.selectedObject == this.gameObject)
            {
                isActive = true;
                InitializeMovement();
            }
            if (touchPhase == TouchPhase.Ended)
                isActive = false;
        }

        void Update()
        {
            if (!isActive)
                return;
            Move();
        }

        void InitializeMovement()
        {
            if (Root == null)
                return;
            rootTransform = Root.transform;
            interactionPosition = XRInputManager.Instance.InteractionPosition;
            var inverseDeviceRotation = Quaternion.Inverse(XRInputManager.Instance.InputUserRotation);
            rotationOffset = inverseDeviceRotation * rootTransform.rotation;
            positionOffset = inverseDeviceRotation * (rootTransform.position - interactionPosition);
        }
        void Move()
        {
            if (Root == null)
                return;
            var deviceRotation = XRInputManager.Instance.InputUserRotation;

#if UNITY_STANDALONE || UNITY_EDITOR
            var headPosition = XRInputManager.Instance.LocalPoseDriver.transform.position;
#else
            var headPosition = XRTrackedUser.Instance.Position();
#endif
            var position = XRInputManager.Instance.InteractionPosition + deviceRotation * positionOffset;

            // var rotation = deviceRotation * rotationOffset;
            // Root.transform.SetPositionAndRotation(position, rotation);
            Root.transform.position = position;
            Root.transform.LookAt(headPosition);
            Root.transform.Rotate(Vector3.up, 180f);
        }
    }
}

