using System;
using Liv.Lck.Recorder;

namespace Liv.Lck
{
    internal interface ILckMixer : IDisposable
    {
        LckResult ActivateCameraById(string cameraId, string monitorId = null);
        void SetPreviewActive(bool isActive);
        public LckResult<ILckCamera> GetActiveCamera();
        public LckResult StopActiveCamera();
        LckResult StartRecording();
        LckResult StopRecording(LckService.StopReason stopReason);
        bool IsRecording();
        bool IsCapturing();
        LckResult CapturePhoto();
        LckResult SetMicrophoneCaptureActive(bool isActive);
        LckResult SetGameAudioMute(bool isMute);
        LckResult<bool> IsGameAudioMute();
        float GetMicrophoneOutputLevel();
        float GetGameOutputLevel();
        LckResult SetTrackResolution(CameraResolutionDescriptor cameraResolutionDescriptor);
        LckResult SetTrackFramerate(uint framerate);
        LckResult<TimeSpan> GetRecordingDuration();
        LckResult SetTrackBitrate(uint bitrate);
        LckResult SetTrackDescriptor(CameraTrackDescriptor cameraTrackDescriptor);
        LckResult SetTrackAudioBitrate(uint audioBitrate);
        void SetMicrophoneGain(float gain);
        void SetGameAudioGain(float gain);
        LckResult<LckDescriptor> GetCurrentTrackDescriptor();
    }
}
