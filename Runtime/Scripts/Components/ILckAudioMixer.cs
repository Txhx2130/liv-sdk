using System;
using Liv.Lck.Collections;

namespace Liv.Lck
{
    internal interface ILckAudioMixer : IDisposable
    {
        AudioBuffer GetMixedAudio();
        void EnableCapture();
        void DisableCapture();
        LckResult SetMicrophoneCaptureActive(bool isOpen);
        LckResult<bool> GetMicrophoneCaptureActive();
        LckResult SetGameAudioMute(bool isMute);
        LckResult<bool> IsGameAudioMute();
        float GetMicrophoneOutputLevel();
        float GetGameOutputLevel();
        void SetMicrophoneGain(float gain);
        void SetGameAudioGain(float gain);
    }
}
