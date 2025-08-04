#if UNITY_ANDROID && !UNITY_EDITOR
using Liv.NativeAudioBridge.Android;
#endif

namespace Liv.NativeAudioBridge
{
    public static class NativeAudioPlayerFactory
    {
        public static INativeAudioPlayer CreateNativeAudioPlayer()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
            return new NativeAudioPlayerWindows();
#elif UNITY_ANDROID
            return new NativeAudioPlayerAndroid();
#else
            throw new PlatformNotSupportedException("NativeAudioManager is not supported on this platform.");
#endif
        }
    }
}
