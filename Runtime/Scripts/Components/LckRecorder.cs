using Liv.NGFX;
using Liv.Lck.Settings;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Liv.Lck.Utilities;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Unity.Profiling;
using System.Threading.Tasks;

namespace Liv.Lck.Recorder
{
    [Serializable]
    public struct CameraTrackDescriptor
    {
        public CameraResolutionDescriptor CameraResolutionDescriptor;
        public uint Bitrate;
        public uint Framerate;
        public uint AudioBitrate;

        public CameraTrackDescriptor(CameraResolutionDescriptor cameraResolutionDescriptor, uint bitrate = 5 << 20, uint framerate = 30, uint audioBitrate = 192000)
        {
            CameraResolutionDescriptor = cameraResolutionDescriptor;
            this.Bitrate = bitrate;
            this.Framerate = framerate;
            this.AudioBitrate = audioBitrate;
        }
    }

    [Serializable]
    public struct CameraResolutionDescriptor
    {
        public uint Width;
        public uint Height;

        public CameraResolutionDescriptor(uint width = 512, uint height = 512)
        {
            this.Width = width;
            this.Height = height;
        }
    }

    public struct RecordingData
    {
        public string RecordingFilePath;
        public float RecordingDuration;
    }

    internal class LckRecorder : ILckRecorder
    {
        static readonly ProfilerMarker _copyOutputFileToNativeGalleryMarker = new ProfilerMarker("LckRecorder.CopyOutputFileToNativeGallery");
        static readonly ProfilerMarker _releaseNativeRenderBufferMarker = new ProfilerMarker("LckRecorder.ReleaseNativeRenderBuffer");
        static readonly ProfilerMarker _captureFrameMarker = new ProfilerMarker("LckRecorder.Capture");
        static readonly ProfilerMarker _allocateFrameSubmissionMarker = new ProfilerMarker("LckRecorder.AllocateFrameSubmission");
        static readonly ProfilerMarker _commandBufferMarker = new ProfilerMarker("LckRecorder.CommandBuffer");
        public enum TrackType : UInt32
        {
            Video,
            Audio,
            Metadata,
        };

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        protected internal struct TrackInfo
        {
            public TrackType type;
            public UInt32 bitrate;
            public UInt32 width;
            public UInt32 height;
            public UInt32 framerate;
            public UInt32 samplerate;
            public UInt32 channels;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        protected internal struct FrameTexture
        {
            public UInt32 id;
            public UInt32 trackIndex;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        protected internal struct AudioTrack
        {
            public UInt32 trackIndex;
            public UInt64 timestampSamples;
            public UInt32 dataSize;
            public IntPtr data;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct FrameSubmission
        {
            public IntPtr recorderContext;
            public IntPtr textureIDs;
            public UInt32 textureIDsSize;
            public UInt64 videoTimestampMilli;

            public UInt32 audioTracksSize;
            public IntPtr audioTracks;

            public UInt32 readyFramesSize;
            public IntPtr readyFrames;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct ResourceData
        {
            public IntPtr recorderContext;
        }

        #region Native Interface

        const string lib = "qck";

        [DllImport(lib)]
        static extern IntPtr GetResourceContext(IntPtr recorderContext);

        [DllImport(lib)]
        static extern IntPtr AllocateFrameSubmission(
            [MarshalAs(UnmanagedType.LPStruct)] FrameSubmission frame,
            [MarshalAs(UnmanagedType.LPArray)] AudioTrack[] audioTracks,
            [MarshalAs(UnmanagedType.LPArray)] bool[] readyFrames);

        [DllImport(lib)]
        static extern IntPtr GetPluginUpdateFunction();

        [DllImport(lib)]
        static extern IntPtr GetInitResourcesFunction();

        [DllImport(lib)]
        static extern IntPtr GetReleaseResourcesFunction();

        [DllImport(lib)]
        static extern UInt32 GetPoolHandlesCount(IntPtr recorderContext, UInt32 track_index);

        [DllImport(lib)]
        static extern UInt32 GetAudioTrackFrameSize(IntPtr recorderContext, UInt32 track_index);

        [DllImport(lib)]
        static extern void SetMicrophoneGain(IntPtr recorderContext, float gain);

        [DllImport(lib)]
        static extern bool GetPoolHandles(IntPtr recorderContext, UInt32 track_index,
            [MarshalAs(UnmanagedType.LPArray)] IntPtr[] out_handles);

        [DllImport(lib)]
        static extern IntPtr CreateRecorder();

        [DllImport(lib)]
        static extern void DestroyRecorder(IntPtr recorderContext);

        [DllImport(lib)]
        static extern bool StartRecorder(IntPtr recorderContext,
            [MarshalAs(UnmanagedType.LPStr)] string path,
            [MarshalAs(UnmanagedType.LPArray)] TrackInfo[] tracks, uint tracksCount);

        [DllImport(lib)]
        static extern void StopRecorder(IntPtr recorderContext);

        [DllImport(lib)]
        static extern void SetRecorderLogLevel(IntPtr recorderContext, LogLevel level);

        #endregion

        public class CaptureData
        {
            public NativeRenderBuffer nativeRenderBuffer;
            public uint trackIndex;
        }

        private List<CaptureData> _cameraRenderData = new List<CaptureData>();

        private IntPtr _recorderContext = IntPtr.Zero;
        private IntPtr _rc = IntPtr.Zero;
        private bool _isRecording = false;
        private float _recordingStartTime;
        private string _lastRecordingFilePath;

        private Handle<FrameTexture[]> _textureIds;
        private List<FrameTexture> _texturesList;
        private Handle<ResourceData> resourceInitData;
        private readonly Action<LckResult<RecordingData>> _onRecordingSavedCallback;

        public LckRecorder(Action<LckResult<RecordingData>> onRecordingSavedCallback)
        {
            _onRecordingSavedCallback = onRecordingSavedCallback;
        }

        public LckResult<bool> IsRecording()
        {
            return LckResult<bool>.NewSuccess(_isRecording);
        }

        public void Start(List<TrackInfo> tracks, RenderTexture renderTexture, int firstVideoTrackIndex, Action<LckResult> onRecordingStartedCallback)
        {
            _ = StartRecording(tracks, renderTexture, firstVideoTrackIndex, onRecordingStartedCallback);
        }

        public void Stop(Action<LckResult> onRecordingStoppedCallback)
        {
            _ = StopRecording(onRecordingStoppedCallback);
        }

        private async Task StartRecording(List<TrackInfo> tracks, RenderTexture cameraTrackTexture, int firstVideoTrackIndex, Action<LckResult> onRecordingStartedCallback)
        {
            try
            {
                LckLog.Log("LCK Starting Recording");

                var indexOfVideoTrack = 0;
                for (var index = 0; index < tracks.Count; index++)
                {
                    var track = tracks[index];
                    if (track.type == TrackType.Video)
                    {
                        indexOfVideoTrack = index;
                        break;
                    }
                }

                _recorderContext = CreateRecorder();
                _rc = GetResourceContext(_recorderContext);
                resourceInitData = new Handle<ResourceData>(new ResourceData() { recorderContext = _recorderContext });

                _cameraRenderData = new List<CaptureData>();

                bool isGL = SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore || SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3;
                _cameraRenderData.Add(new CaptureData
                {
                    nativeRenderBuffer = isGL ?
                        new NativeRenderBuffer(_rc, cameraTrackTexture.colorBuffer, cameraTrackTexture.GetNativeTexturePtr(),
                            cameraTrackTexture.width, cameraTrackTexture.height, 1, GraphicsFormat.R8G8B8A8_UNorm) :
                        new NativeRenderBuffer(_rc, cameraTrackTexture.colorBuffer, 
                            cameraTrackTexture.width, cameraTrackTexture.height, 1, GraphicsFormat.R8G8B8A8_UNorm),
                    trackIndex = (uint)tracks.Count - 1,
                });

                var filename = FileUtility.GenerateFilename("mp4");
                _lastRecordingFilePath = Path.Combine(Application.temporaryCachePath, filename);

                bool successful = false;
                await Task.Run(() =>
                {
                    try
                    {
                        successful = StartRecorder(_recorderContext, _lastRecordingFilePath, tracks.ToArray(), (uint)tracks.Count);
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogException(ex);
                        onRecordingStartedCallback?.Invoke(LckResult.NewError(LckError.RecordingError, ex.Message));
                    }
                });

                if (!successful)
                {
                    LckLog.LogError("LCK Recording could not be started");
                    onRecordingStartedCallback?.Invoke(LckResult.NewError(LckError.RecordingError, "Unknown error starting recording."));
                    return;
                }

                _recordingStartTime = Time.time;

                var cb = new CommandBuffer();
                cb.IssuePluginEventAndData(GetInitResourcesFunction(), 1, resourceInitData.ptr());
                cb.name = "qck InitResource";
                Graphics.ExecuteCommandBuffer(cb);

                _texturesList = new List<FrameTexture>();
                foreach (var data in _cameraRenderData)
                {
                    _texturesList.Add(new FrameTexture()
                    {
                        id = data.nativeRenderBuffer.id,
                        trackIndex = data.trackIndex
                    });
                }

                _textureIds = new Handle<FrameTexture[]>(_texturesList.ToArray());

                _isRecording = true;

                onRecordingStartedCallback?.Invoke(LckResult.NewSuccess());
            }
            catch (Exception e)
            {
                LckLog.LogError("LCK Start Recording Task failed: " + e.Message);
                onRecordingStartedCallback?.Invoke(LckResult.NewError(LckError.RecordingError, e.Message));
            }
        }

        public bool EncodeFrame(float time, bool[] readyTracks, AudioTrack[] audioTracks)
        {
            if (!_isRecording)
            {
                return false;
            }

            _allocateFrameSubmissionMarker.Begin();
            // This ptr will be freed in native after use, it can be null
            IntPtr frame_ptr = AllocateFrameSubmission(new FrameSubmission()
            {
                recorderContext = _recorderContext,
                textureIDs = _textureIds.ptr(),
                textureIDsSize = (uint)_textureIds.data().Length,
                videoTimestampMilli = (ulong)(time * 1000),
                audioTracksSize = (uint)1,
                readyFramesSize = (uint)1,
            }, audioTracks, readyTracks);

            _allocateFrameSubmissionMarker.End();

            _commandBufferMarker.Begin();
            var cb = new CommandBuffer();
            cb.IssuePluginEventAndData(GetPluginUpdateFunction(), 1, frame_ptr);
            cb.name = "qck Recorder";
            Graphics.ExecuteCommandBuffer(cb);
            _commandBufferMarker.End();

            return true;
        }

        public int GetAudioFrameSize()
        {
            // NOTE: this assumes audio track is 0
            return (int)GetAudioTrackFrameSize(_recorderContext, 0);
        }

        private async Task StopRecording(Action<LckResult> onRecordingStoppedCallback)
        {
            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        StopRecorder(_recorderContext);
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogException(ex);
                        onRecordingStoppedCallback?.Invoke(LckResult.NewError(LckError.RecordingError, ex.Message));
                        return;
                    }
                });

                _isRecording = false;
                ReleaseNativeRenderBuffers();
                LckMonoBehaviourMediator.StartCoroutine("CopyRecordingToGalleryWhenReady", CopyRecordingToGalleryWhenReady());
                onRecordingStoppedCallback?.Invoke(LckResult.NewSuccess());

                var cb = new CommandBuffer();
                cb.IssuePluginEventAndData(GetReleaseResourcesFunction(), 1, resourceInitData.ptr());
                cb.name = "qck ReleaseResource";
                Graphics.ExecuteCommandBuffer(cb);

                // DestroyRecorder will be called by ReleaseResource in native from the render thread
                //DestroyRecorder(_recorderContext);
                _recorderContext = IntPtr.Zero;
            }
            catch (Exception e)
            {
                LckLog.LogError("LCK Stop Recording failed: " + e.Message);
                onRecordingStoppedCallback?.Invoke(LckResult.NewError(LckError.RecordingError, e.Message));
            }

        }

        WaitForSeconds _copyVideoSpinWait = new WaitForSeconds(0.1f);
        private IEnumerator CopyRecordingToGalleryWhenReady()
        {
            while (FileUtility.IsFileLocked(_lastRecordingFilePath) && File.Exists(_lastRecordingFilePath))
            {
                yield return _copyVideoSpinWait;
            }

            var recordingDuration = Time.time - _recordingStartTime;
            using (_copyOutputFileToNativeGalleryMarker.Auto())
            {
                Task task = FileUtility.CopyToGallery(_lastRecordingFilePath, LckSettings.Instance.RecordingAlbumName,
                    (success, path) =>
                    {
                        LckMonoBehaviourMediator.Instance.EnqueueMainThreadAction(() =>
                        {
                            if (success)
                            {
                                LckLog.Log("LCK Recording saved to gallery: " + path);
                                _onRecordingSavedCallback.Invoke(LckResult<RecordingData>.NewSuccess(new RecordingData
                                    { RecordingFilePath = path, RecordingDuration = recordingDuration }));
                            }
                            else
                            {
                                _onRecordingSavedCallback.Invoke(
                                    LckResult<RecordingData>.NewError(LckError.FailedToCopyRecordingToGallery,
                                        "Failed to copy recording to Gallery"));
                                LckLog.LogError("LCK Failed to save recording to gallery");
                            }
                        });
                    });

                yield return new WaitUntil(() => task.IsCompleted);
            }
        }

        public void Dispose()
        {
            if (_isRecording)
            {
                _ = StopRecording(null);
            }

            ReleaseNativeRenderBuffers();
        }

        public void ReleaseNativeRenderBuffers()
        {
            using (_releaseNativeRenderBufferMarker.Auto())
            {
                if (_isRecording)
                {
                    LckLog.LogWarning("LCK Can't release native render buffers while recording.");
                    return;
                }

                foreach (var data in _cameraRenderData)
                {
                    data.nativeRenderBuffer.Dispose();
                }
            }
        }
    }
}
