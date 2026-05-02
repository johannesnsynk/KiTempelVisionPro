using System.Collections;
using System.Collections.Generic;
using NSYNK.HyperSlides.Input;
using NSYNK.HyperSlides.XR;
using UnityEngine;

using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace NSYNK.HyperSlides.UI {
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
            XRInputManager.onTouchUpdate += HandeChangeTouchPhase;
        }

        
        void OnDisable() 
        {
            XRInputManager.onTouchUpdate -= HandeChangeTouchPhase;
        }

        void Destroy() 
        {
            XRInputManager.onTouchUpdate -= HandeChangeTouchPhase;
        }


        void HandeChangeTouchPhase(TouchPhase touchPhase) {
            if (touchPhase == TouchPhase.Began && XRInputManager.Instance.m_SelectedObject == this.gameObject) {
                isActive = true;
                InitializeMovement();
            }
            if (touchPhase == TouchPhase.Ended)
                isActive = false;
        }

        void Update() {
            if (!isActive)
                return;
            Move();
        }

        void InitializeMovement() {
            if (Root == null)
                return;
            rootTransform = Root.transform;
            interactionPosition = XRInputManager.Instance.primaryTouchData.interactionPosition;
            var inverseDeviceRotation = Quaternion.Inverse(XRInputManager.Instance.primaryTouchData.inputDeviceRotation);
            rotationOffset = inverseDeviceRotation * rootTransform.rotation;
            positionOffset = inverseDeviceRotation * (rootTransform.position - interactionPosition);
        }
        void Move() 
        {   
            if (Root == null)
                return;
            var deviceRotation = XRInputManager.Instance.primaryTouchData.inputDeviceRotation;

#if UNITY_STANDALONE || UNITY_EDITOR
            var headPosition = XRInputManager.Instance.localPoseDriver.transform.position;
#else
            var headPosition = XRTrackedUser.Instance.Position();
#endif
            var position = XRInputManager.Instance.primaryTouchData.interactionPosition + deviceRotation * positionOffset;

            // var rotation = deviceRotation * rotationOffset;
            // Root.transform.SetPositionAndRotation(position, rotation);
            Root.transform.position = position;
            Root.transform.LookAt(headPosition);
            Root.transform.Rotate(Vector3.up, 180f);
        }
    }
}

