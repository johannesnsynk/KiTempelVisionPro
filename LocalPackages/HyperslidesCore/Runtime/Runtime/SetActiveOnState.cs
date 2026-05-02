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

        private void OnEnable() => HyperSlidesStateManager.stateUpdate += HandleSetActive;
        private void OnDisable() => HyperSlidesStateManager.stateUpdate -= HandleSetActive;

        private void HandleSetActive(HyperSlidesStateManager.AppState state)
        {
            switch (showType)
            {
                case ShowType.ON_STATE:
                    gameObjects.ForEach(g => g.SetActive(state == appState));
                    break;
                case ShowType.EXCLUDE_STATE:
                    gameObjects.ForEach(g => g.SetActive(state != appState));
                    break;
                case ShowType.TO_STATE:
                    gameObjects.ForEach(g => g.SetActive(state <= appState));
                    break;
                case ShowType.FROM_STATE:
                    gameObjects.ForEach(g => g.SetActive(state >= appState));
                    break;
            }
        }
    }
}