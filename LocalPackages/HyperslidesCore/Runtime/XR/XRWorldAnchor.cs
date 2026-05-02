using NSYNK.HyperSlides.Network;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace NSYNK.HyperSlides.XR
{
    public class XRWorldAnchor : MonoBehaviour
    {
        public enum AnchorType
        {
            Default = 0,
            Position = 1,
            Rotation = 2
        }

        public AnchorType anchorType = AnchorType.Default;
        public GameObject visuals;
        public GameObject personaPosition, personaRotation;
        public GameObject buttonCanvas;
        public TMPro.TextMeshPro idText;

        [HideInInspector]
        public ARAnchor trackedAnchor;
        [HideInInspector]
        //name should change to trackable
        public ARTrackable trackedImage;

        // trying to make it more generic so I can do custom logic for calculating the target position and rotation
        struct TargetPositionRotation {
            public bool isSet;
            public Vector3 position;
            public Quaternion rotation;
        }
        TargetPositionRotation targetPositionRotation;

        private void Awake()
        {
            personaPosition.SetActive(false);
            personaRotation.SetActive(false);
        }

        private void OnEnable()
        {
            if (!buttonCanvas.GetComponent<Canvas>().worldCamera)
                buttonCanvas.GetComponent<Canvas>().worldCamera = Camera.main;
        }

        public bool IsTracking()
        {
            if (RuntimeHandler.Settings.trackingType == Settings.TrackingType.Anchors)
                return trackedAnchor != null;
            if (RuntimeHandler.Settings.trackingType == Settings.TrackingType.Image)
                return anchorType != AnchorType.Position || trackedImage != null || targetPositionRotation.isSet;
            if (RuntimeHandler.Settings.trackingType == Settings.TrackingType.Free)
                return true;

            return false;
        }

        /// <summary>
        /// Method fired by <see cref="ARTrackedImageManager.trackedImagesChanged"/> event to attach this anchor to the <see cref="ARTrackedImageManager.referenceLibrary"/>`s image
        /// </summary>
        /// <param name="trackable">The <see cref="ARTrackedImage"/> that has been detected</param>
        public void UpdateAnchor(ARTrackable trackable)
        {
            // Is it ignored when it's already set? That means that we can't simply update the Anchor with the trackable.
            // But isn't it what we want from the function?
            // I guess it waits for PrepareWorldAnchor() to allow updating, but that makes the whole workflow pretty obscure,
            // Better make sure we are calling it at the right time
            if (trackedImage || trackedAnchor)
                return;

            // cannot use referenceImage.name anymore
            // Debug.Log("Attach trackable image " + trackable.referenceImage.name + " to " + anchorType);
            trackedImage = trackable;
        }
        // Trying new method to set target Position and Rotation in a more generic way
        public void UpdateTargetPositionLocation(Vector3 position, Quaternion rotation) {
            targetPositionRotation.isSet = true;
            targetPositionRotation.position = position;
            targetPositionRotation.rotation = rotation;
        }

        /// <summary>
        /// Method fired by <see cref="ARAnchorManager.anchorsChanged"/> event to attach this anchor to the ARSession delivered anchor
        /// </summary>
        /// <param name="anchor">The <see cref="ARAnchor"/> that has been found in the ARSession</param>
        public void UpdateAnchor(ARAnchor anchor)
        {
            Debug.Log("Found anchor " + anchor.trackableId + " to " + anchorType);
            trackedAnchor = anchor;
            PrepareWorldAnchor();
            targetPositionRotation.isSet = false;
        }

        public void RemoveAnchor(ARAnchor anchor = null)
        {
            if (anchor == null)
                anchor = trackedAnchor;
            if (anchor == null)
                return;

            bool result = XRAnchorManager.Instance.arAnchorManager.TryRemoveAnchor(anchor);
            if (result)
            {
                Debug.Log("Removed anchor on " + anchorType);
                trackedAnchor = null;
                // Here PrepareWorldAnchor() is not doing anything, because of
                // if (trackedImage == null || trackedAnchor == null)
                //     return;
                PrepareWorldAnchor();
            }
            else
            {
                Debug.LogWarning("Cant remove anchor on " + anchorType);
                return;
            }
        }

        /// <summary>
        /// UI triggered anchor creation based on the position delivered by <see cref="trackedImage"/>
        /// </summary>
        public void CreateAnchorOnPosition() => CreateAnchorAsync();

        private async void CreateAnchorAsync()
        {
            var result = await XRAnchorManager.Instance.arAnchorManager.TryAddAnchorAsync(new Pose(transform.position, transform.rotation));
            if (result.status.IsSuccess())
            {
                Debug.Log("Created anchor on " + anchorType);
                trackedAnchor = result.value;
                trackedAnchor.destroyOnRemoval = false;
                PrepareWorldAnchor();
                targetPositionRotation.isSet = false;
            }
            else
            {
                Debug.LogWarning("Cant create anchor on " + anchorType);
                return;
            }
        }

        /// <summary>
        /// Prepare and set the current trackable anchor when either finding or creating the ARAnchor component
        /// </summary>
        /// <param name="anchor"></param>
        private void PrepareWorldAnchor()
        {
            // Why do we need those checks? I guess it prevets some unexpected things from happening.
            // Can't it be solved by checking function calls?
            if (trackedImage == null || trackedAnchor == null)
                return;

            trackedImage = null;

            XRAnchorManager.imageWorldAnchors.UpdateAnchorID(anchorType, trackedAnchor.trackableId.ToString());
        }

        private void Update()
        {
#if UNITY_VISIONOS
            if (RuntimeHandler.Settings.showPersonaMarkersOnLobby)
            {
                personaPosition.SetActive(anchorType == AnchorType.Position && XRNetworkManager.match == null);
                personaRotation.SetActive(anchorType == AnchorType.Rotation && XRNetworkManager.match == null);
            }
#endif

            if (!XRAnchorManager.arSupported)
            {
                visuals.SetActive(false);
                return;
            }

            bool isTracking = IsTracking();

            // Should not targetPosition and targetRotation be set in aranchormanager or only here
            // but not in both places, so there is less points of failure and confusion?
            Vector3 targetPosition = anchorType == AnchorType.Rotation ? new Vector3(0,0,1) : Vector3.zero;
            Vector3 targetRotation = Vector3.zero;

            if (trackedImage)
            {
                targetPosition = trackedImage.transform.position;
                targetRotation = trackedImage.transform.rotation.eulerAngles;
            }

            if (targetPositionRotation.isSet) 
            {
                targetPosition = targetPositionRotation.position;
                targetRotation = targetPositionRotation.rotation.eulerAngles;
            }

            if (trackedAnchor)
                targetPosition = trackedAnchor.transform.position;

            if (idText)
            {
                string imageState = trackedImage != null ? trackedImage.trackingState.ToString() : "No tracked image";
                string imageId = trackedImage != null ? trackedImage.trackableId.ToString() : "";
                string anchorState = trackedAnchor != null ? trackedAnchor.trackingState.ToString() : "No tracked anchor";
                string anchorId = trackedAnchor != null ? trackedAnchor.trackableId.ToString() : "";

                idText.text = anchorType +
                    "\nImageID: " + imageId +
                    "\nImage: " + imageState +
                    "\nAnchorID: " + anchorId +
                    "\nAnchor: " + anchorState;
            }

#if !UNITY_IOS
            targetPosition.y = 0;
            targetRotation.x = 0;
            targetRotation.z = 0;
#endif

            transform.SetPositionAndRotation(targetPosition, Quaternion.Euler(targetRotation));

            buttonCanvas.SetActive(!isTracking && RuntimeHandler.Settings.trackingType == Settings.TrackingType.Anchors);

            if (anchorType == AnchorType.Rotation && RuntimeHandler.Settings.trackingType == Settings.TrackingType.Image)
                visuals.SetActive(false);
            else
                visuals.SetActive(!isTracking || UnityEngine.Debug.isDebugBuild);
        }
    }
}