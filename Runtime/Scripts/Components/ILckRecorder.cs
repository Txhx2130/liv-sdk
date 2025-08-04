using System;
using System.Collections.Generic;
using UnityEngine;

namespace Liv.Lck.Recorder
{
    internal interface ILckRecorder : IDisposable
    {
        void Start(List<LckRecorder.TrackInfo> tracks, RenderTexture renderTexture, int firstVideoTrackIndex, Action<LckResult> onRecordingStartedCallback);
        void Stop(Action<LckResult> onRecordingStoppedCallback);
        void ReleaseNativeRenderBuffers();
        bool EncodeFrame(float time, bool[] readyTracks, LckRecorder.AudioTrack[] audioTracks);
    }
}
