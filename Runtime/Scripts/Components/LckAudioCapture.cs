using Liv.Lck.Collections;
using UnityEngine;

namespace Liv.Lck
{
    internal class LckAudioCapture : MonoBehaviour, ILckAudioSource
    {
        private bool _captureAudio;
        
        private AudioBuffer _audioBuffer = new AudioBuffer(96000);

        private readonly System.Object _audioThreadLock = new System.Object();

        public void GetAudioData(ILckAudioSource.AudioDataCallbackDelegate callback)
        {
            lock (_audioThreadLock)
            {
                callback(_audioBuffer);
                _audioBuffer.Clear();
            }
        }

        public void EnableCapture()
        {
            _audioBuffer.Clear();
            _captureAudio = true;
        }

        public void DisableCapture()
        {
            _audioBuffer.Clear();
            _captureAudio = false;
        }

        public bool IsCapturing()
        {
            return _captureAudio;
        }

        protected virtual void OnAudioFilterRead(float[] data, int channels)
        {
            if (_captureAudio)
            {
                lock(_audioThreadLock)
                {
                    if(!_audioBuffer.TryExtendFrom(data))
                    {
                        LckLog.LogWarning("LCK Audio Capture losing data. Expecting this to be a lag spike.");
                    }
                }
            }
        }
    }
}
