using System;
using UnityEngine;
using static NSYNK.HyperSlides.HyperSlidesStateManager;

namespace NSYNK.HyperSlides
{
    /// <summary>
    /// Class to hold app state icons for UI representation
    /// </summary>
    [Serializable]
    public class AppStateIcon
    {
        public AppState state;
        public Sprite icon;
    }
}