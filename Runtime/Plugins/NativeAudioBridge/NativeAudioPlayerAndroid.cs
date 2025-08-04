#if UNITY_ANDROID && !UNITY_EDITOR
using System.Threading.Tasks;
using UnityEngine;

namespace Liv.NativeAudioBridge.Android
{
    public class NativeAudioPlayerAndroid : INativeAudioPlayer
    {
        private AndroidJavaObject _javaNativeAudioBridgeClass;

        public NativeAudioPlayerAndroid()
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            _javaNativeAudioBridgeClass = new AndroidJavaObject("com.qck.nativeaudiobridge.NativeAudioBridge");
        }

        private AndroidJavaObject GetJavaNativeAudioBridgeClass()
        {
            return _javaNativeAudioBridgeClass ?? (_javaNativeAudioBridgeClass = new AndroidJavaObject("com.qck.nativeaudiobridge.NativeAudioBridge"));
        }

        public void PreloadAudioClip(AudioClip audioClip, float volume, bool forceReload)
        {
            var audioData = NativeAudioUtils.ConvertAudioClipToByteArray(audioClip, volume);
            GetJavaNativeAudioBridgeClass().CallStatic("PreloadAudio", audioClip.name, audioData, audioClip.frequency, audioClip.channels, forceReload);
        }

        public void PlayAudioClip(AudioClip audioClip, float volume = 1f)
        {
            var key = audioClip.name;
            PlayAudio(key);
        }

        public void StopAllAudio()
        {
            GetJavaNativeAudioBridgeClass().CallStatic("StopCurrentAudioPlayback");
        }
        
        public void Dispose()
        {
            GetJavaNativeAudioBridgeClass()?.CallStatic("ReleaseMap");
            _javaNativeAudioBridgeClass?.Dispose();
        }
        
        private void PlayAudio(string key)
        {
            GetJavaNativeAudioBridgeClass().CallStatic("PlayPreloadedAudio", key);
        }
    }
}
#endif
