using Liv.Lck.Collections;

namespace Liv.Lck
{
    public interface ILckAudioSource
    {
        void GetAudioData(AudioDataCallbackDelegate callback);
        void EnableCapture();
        void DisableCapture();
        bool IsCapturing();

        delegate void AudioDataCallbackDelegate(AudioBuffer audioBuffer);
    }
}
