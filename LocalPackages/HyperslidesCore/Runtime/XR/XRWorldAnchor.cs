using NSYNK.HyperSlides.Network;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace NSYNK.HyperSlides.XR
{
    /// <summary>
    /// Class representing a world anchor in AR
    /// </summary>
    public class XRWorldAnchor : MonoBehaviour
    {
        public enum WorldAnchorType
        {
            Default = 0,
            Position = 1,
            Rotation = 2
        }

        public WorldAnchorType AnchorType = WorldAnchorType.Default;
        public GameObject Visuals;
        public GameObject MarkerPositionVisual, MarkerRotationVisual;
        public GameObject ButtonCanvas;
        public TMPro.TextMeshProUGUI ButtonText;

        [HideInInspector]
        public ARAnchor trackedAnchor;
        [HideInInspector]
        //name should change to trackable
        public ARTrackable trackedImage;

        // trying to make it more generic so I can do custom logic for calculating the target position and rotation
        struct TargetPositionRotation
        {
            public bool isSet;
            public Vector3 position;
            public Quaternion rotation;
        }
        TargetPositionRotation targetPositionRotation;

        private void Awake()
        {
            MarkerPositionVisual.SetActive(false);
            MarkerRotationVisual.SetActive(false);
        }

        private void OnEnable()
        {
            if (!ButtonCanvas.GetComponent<Canvas>().worldCamera)
                ButtonCanvas.GetComponent<Canvas>().worldCamera = Camera.main;
        }

        public bool IsTracking()
        {
            if (HyperSlidesStateManager.Instance.Settings.trackingType == Settings.TrackingType.Anchors)
                return trackedAnchor != null;
            if (HyperSlidesStateManager.Instance.Settings.trackingType == Settings.TrackingType.Image)
                return AnchorType != WorldAnchorType.Position || trackedImage != null || targetPositionRotation.isSet;
            if (HyperSlidesStateManager.Instance.Settings.trackingType == Settings.TrackingType.Free)
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
        public void UpdateTargetPositionLocation(Vector3 position, Quaternion rotation)
        {
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
            Debug.Log("Found anchor " + anchor.trackableId + " to " + AnchorType);
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
                Debug.Log("Removed anchor on " + AnchorType);
                trackedAnchor = null;
                // Here PrepareWorldAnchor() is not doing anything, because of
                // if (trackedImage == null || trackedAnchor == null)
                //     return;
                PrepareWorldAnchor();
            }
            else
            {
                Debug.LogWarning("Cant remove anchor on " + AnchorType);
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
                Debug.Log("Created anchor on " + AnchorType);
                trackedAnchor = result.value;
                trackedAnchor.destroyOnRemoval = false;
                PrepareWorldAnchor();
                targetPositionRotation.isSet = false;
            }
            else
            {
                Debug.LogWarning("Cant create anchor on " + AnchorType);
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

            XRAnchorManager.imageWorldAnchors.UpdateAnchorID(AnchorType, trackedAnchor.trackableId.ToString());
        }

        private void Update()
        {
#if UNITY_VISIONOS
            if (HyperSlidesStateManager.Instance.Settings.showMarkerVisualsOnLobby)
            {
                MarkerPositionVisual.SetActive(AnchorType == WorldAnchorType.Position && XRNetworkManager.Instance.Match == null);
                MarkerRotationVisual.SetActive(AnchorType == WorldAnchorType.Rotation && XRNetworkManager.Instance.Match == null);
            }
#endif

            if (!XRAnchorManager.arSupported)
            {
                transform.SetPositionAndRotation(targetPositionRotation.position, targetPositionRotation.rotation);
                Visuals.SetActive(false);
                return;
            }

            bool isTracking = IsTracking();

            // Should not targetPosition and targetRotation be set in aranchormanager or only here
            // but not in both places, so there is less points of failure and confusion?
            Vector3 targetPosition = AnchorType == WorldAnchorType.Rotation ? new Vector3(0, 0, 1) : Vector3.zero;
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

            if (ButtonText)
            {
                string imageState = trackedImage != null ? trackedImage.trackingState.ToString() : "No tracked image";
                string imageId = trackedImage != null ? trackedImage.trackableId.ToString() : "";
                string anchorState = trackedAnchor != null ? trackedAnchor.trackingState.ToString() : "No tracked anchor";
                string anchorId = trackedAnchor != null ? trackedAnchor.trackableId.ToString() : "";

                if (UnityEngine.Debug.isDebugBuild)
                    ButtonText.text = AnchorType +
                        "\nImageID: " + imageId +
                        "\nImage: " + imageState +
                        "\nAnchorID: " + anchorId +
                        "\nAnchor: " + anchorState;
                else
                    ButtonText.text = $"Place {AnchorType.ToString()} anchor";
            }

#if !UNITY_IOS
            targetPosition.y = 0;
            targetRotation.x = 0;
            targetRotation.z = 0;
#endif

            transform.SetPositionAndRotation(targetPosition, Quaternion.Euler(targetRotation));

            ButtonCanvas.SetActive(!isTracking && HyperSlidesStateManager.Instance.Settings.trackingType == Settings.TrackingType.Anchors);

            if (AnchorType == WorldAnchorType.Rotation && HyperSlidesStateManager.Instance.Settings.trackingType == Settings.TrackingType.Image)
                Visuals.SetActive(false);
            else
                Visuals.SetActive(!isTracking || UnityEngine.Debug.isDebugBuild);
        }
    }
}