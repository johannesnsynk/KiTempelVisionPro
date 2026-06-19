using NSYNK.HyperSlides.Network;

namespace NSYNK.HyperSlides.Runtime
{
    /// <summary>
    /// Data structure for overriding camera transform via JSON
    /// </summary>
    public class CameraTransformOverride
    {
        public CameraTransformOverride()
        {
            data = new Data
            {
                cameraTransform = new CameraTransform
                {
                    location = new XRNetworkObjects.SerializedVector(),
                    rotation = new XRNetworkObjects.SerializedVector(),
                    scale = new XRNetworkObjects.SerializedVector()
                }
            };
        }

        public Data data { get; set; }

        public class Data
        {
            public string id { get; set; }
            public bool enabled { get; set; }
            public CameraTransform cameraTransform { get; set; }
        }

        public class CameraTransform
        {
            public XRNetworkObjects.SerializedVector location { get; set; }
            public XRNetworkObjects.SerializedVector rotation { get; set; }
            public XRNetworkObjects.SerializedVector scale { get; set; }
        }
    }
}