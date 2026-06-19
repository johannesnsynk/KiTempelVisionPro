using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NSYNK.HyperSlides.Runtime
{
    [Serializable]
    public class CameraSettings
    {
        public RuntimePlatform platform;
        public bool renderPostProcessing;
        public AntialiasingMode antialiasingMode;
        public float TAAContrastAdaptiveSharpening;
    }
}