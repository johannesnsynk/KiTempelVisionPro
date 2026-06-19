public static class RealtimeClientFactory
{
    public static IRealtimeClient Create()
    {
#if UNITY_VISIONOS && !UNITY_EDITOR
        return new VisionOsSwiftBridgeRealtimeClient();
#else
        return new LiveKitFfiRealtimeClient();
#endif
    }
}
