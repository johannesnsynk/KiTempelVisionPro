using System;
using System.Collections.Generic;
using UnityEngine;

namespace NSYNK.HyperSlides.Runtime
{
    public class SetActiveOnState : MonoBehaviour
    {
        public HyperSlidesStateManager.AppState appState;
        public ShowType showType = ShowType.ON_STATE;
        public List<GameObject> gameObjects;

        public enum ShowType
        {
            ON_STATE = 0,
            EXCLUDE_STATE = 1,
            TO_STATE = 2,
            FROM_STATE = 3
        }

        private void OnEnable() => HyperSlidesStateManager.Instance.OnStateUpdate += HandleSetActive;
        private void OnDisable()
        {
            if (HyperSlidesStateManager.Instance == null)
                return;

            HyperSlidesStateManager.Instance.OnStateUpdate -= HandleSetActive;
        }

        private void HandleSetActive(HyperSlidesStateManager.AppState state)
        {
            foreach (GameObject g in gameObjects)
            {
                if (g == null)
                {
                    Debug.LogWarning("SetActiveOnState: One of the GameObjects is null. Please check the list.");
                    continue;
                }

                switch (showType)
                {
                    case ShowType.ON_STATE:
                        g.SetActive(state == appState);
                        break;
                    case ShowType.EXCLUDE_STATE:
                        g.SetActive(state != appState);
                        break;
                    case ShowType.TO_STATE:
                        g.SetActive(state <= appState);
                        break;
                    case ShowType.FROM_STATE:
                        g.SetActive(state >= appState);
                        break;
                }
            }
        }
    }
}