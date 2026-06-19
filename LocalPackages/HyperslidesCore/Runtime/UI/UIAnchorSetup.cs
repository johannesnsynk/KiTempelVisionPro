using NSYNK.HyperSlides.XR;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityMainThreadDispatcher;

#if UNITY_IOS
using UnityEngine.XR.ARKit;
#endif

namespace NSYNK.HyperSlides.UI
{
    public class UIAnchorSetup : Singleton<UIAnchorSetup>
    {
        public enum SetupState
        {
            None = -1,
            Init = 0,
            Searching = 1,
            ImageTrackingPosition = 2,
            ImageTrackingRotation = 3,
            AnchorTracking = 4,
            WorldMapping = 5
        }

        public List<StateEvent> stateEvents;

        [ReadOnly]
        public static SetupState setupState = SetupState.None;

        public static async Awaitable UpdateState(SetupState state)
        {
            setupState = state;

            Dispatcher.Enqueue(() =>
            {
                Debug.Log("Anchor Setup state: " + state, Instance);

                // for (int i = 0; i < Instance.transform.childCount; ++i)
                // {
                //     Transform child = Instance.transform.GetChild(i);
                //     child.gameObject.SetActive(false);
                // }

                StateEvent foundEvent = Instance.stateEvents.Find(se => se.state == state);

                if (foundEvent != null)
                    foundEvent.events?.Invoke();
            });

            await Awaitable.MainThreadAsync();           

            switch (setupState)
            {
                case SetupState.WorldMapping:
#if UNITY_IOS && !UNITY_EDITOR
                    await XRWorldMapManager.LoadWorldMapAsync();

                    await HyperSlidesStateManager.Instance.UpdateStateWithBool("Checking for worldmap support", XRWorldMapManager.Instance.WorldmapSupported);
                    await HyperSlidesStateManager.Instance.UpdateStateWithBool("Please scan the surroundings, your markers are going to be placed at", XRWorldMapManager.Instance.WorldmapMapped);

                    await XRWorldMapManager.SaveWorldMapAsync();

                    await HyperSlidesStateManager.Instance.UpdateStateWithDelay("Worldmap saved");
                    await UpdateState(SetupState.Searching);
#endif
                    break;
                case SetupState.Searching:
                    await HyperSlidesStateManager.Instance.UpdateStateWithDelay("Searching for spatial markers", HyperSlidesStateManager.Instance.Settings.findWorldmapTimeout);

                    if (XRAnchorManager.IsTracking())
                        await HyperSlidesStateManager.Instance.UpdateStateWithDelay("Spatial markers found");
                    else
                        await UpdateState(SetupState.ImageTrackingPosition);
                    break;
                case SetupState.ImageTrackingPosition:
                    await HyperSlidesStateManager.Instance.UpdateStateWithBool("Please scan the position marker", XRAnchorManager.Instance.positionAnchor.IsTracking);

                    if (XRAnchorManager.Instance.positionAnchor.IsTracking())
                    {
                        await Debug.LogQueue("Position anchor is tracking");

                        if (HyperSlidesStateManager.Instance.Settings.trackingType == Settings.TrackingType.Anchors)
                            await UpdateState(SetupState.ImageTrackingRotation);
                        else
                            await UpdateState(SetupState.AnchorTracking);
                    }
                    else
                        await Debug.LogQueue("Position anchor is not tracking");

                    break;
                case SetupState.ImageTrackingRotation:
                    await HyperSlidesStateManager.Instance.UpdateStateWithBool("Please scan the rotation marker", XRAnchorManager.Instance.rotationAnchor.IsTracking);

                    if (XRAnchorManager.Instance.rotationAnchor.IsTracking())
                    {
                        await Debug.LogQueue("Rotation anchor is tracking");

                        await UpdateState(SetupState.AnchorTracking);
                    }
                    else
                        await Debug.LogQueue("Rotation anchor is not tracking");

                    break;
                case SetupState.AnchorTracking:
                    if (!XRAnchorManager.Instance.positionAnchor.IsTracking())
                        await UpdateState(SetupState.ImageTrackingPosition);

                    if (!XRAnchorManager.Instance.rotationAnchor.IsTracking())
                        await UpdateState(SetupState.ImageTrackingRotation);

                    await HyperSlidesStateManager.Instance.UpdateStateWithDelay("Spatial surrounding set up");
                    break;
            }
        }

        [Serializable]
        public class StateEvent
        {
            public SetupState state;
            public UnityEvent events;
        }
    }
}