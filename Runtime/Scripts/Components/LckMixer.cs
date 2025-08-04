using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Liv.Lck.Collections;
using Liv.Lck.Recorder;
using Liv.Lck.Settings;
using Liv.Lck.Telemetry;
using Liv.NGFX;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Liv.Lck
{
    internal class LckMixer : ILckMixer, ILckEarlyUpdate
    {
        private ILckCamera _activeCamera;
        private ILckRecorder _recorder;
        private ILckAudioMixer _audioMixer;
        private ILckStorageWatcher _lckStorageWatcher;
        private ILckPhotoCapture _photoCapture;

        private bool _isCapturing;
        private bool _shouldStartRecording;
        private LckService.StopReason _stopReason;
        private bool _shouldStopRecording;
        private RenderTexture _cameraTrackTexture;
        private CameraTrackDescriptor _cameraTrack;
        private LckRecorder.AudioTrack[] _audioTracks = new LckRecorder.AudioTrack[1];
        private bool _frameHasBeenRendered;

        private readonly Action<LckResult> _onRecordingStarted;
        private readonly Action<LckResult> _onRecordingStopped;
        private readonly Action<LckResult> _onLowStorageSpace;
        private readonly Action<LckResult> _onPhotoSaved;
        private readonly Action<LckResult<RecordingData>> _onRecordingSaved;
        private readonly Action<LckResult<ILckCamera>> _onActiveCameraSet;

        private bool[] _readyTracks = new[] { false };
        private float _recordingTime;
        private uint _encodedFrames;
        private UInt64 _audioTimestampFrameCount;
        private RecordingState _recordingState = RecordingState.Idle;
        private bool _shouldCapturePreview = true;
        private int _sampleRate;

        private int _encodingWarmupFramesLeft = 0;

        internal enum RecordingState
        {
            Idle,
            Starting,
            Recording,
            Stopping,
            Blocked
        }

        public LckMixer(LckDescriptor descriptor,
            Action<LckResult> onRecordingStarted,
            Action<LckResult> onRecordingStopped,
            Action<LckResult> onLowStorageSpace,
            Action<LckResult> onPhotoSaved,
            Action<LckResult<RecordingData>> onRecordingSaved,
            Action<LckResult<ILckCamera>> onActiveCameraSet)
        {
            _onRecordingStarted = onRecordingStarted;
            _onRecordingStopped = onRecordingStopped;
            _onLowStorageSpace = onLowStorageSpace;
            _onRecordingSaved = onRecordingSaved;
            _onPhotoSaved = onPhotoSaved;
            _onActiveCameraSet = onActiveCameraSet;

            _sampleRate = LckAudioMixer.GetSampleRate();

            _recorder = new LckRecorder(OnRecordingSavedCallback );
            _audioMixer = new LckAudioMixer(_sampleRate);
            _photoCapture = new LckPhotoCapture(_cameraTrackTexture, OnPhotoSavedCallback);
                
            _lckStorageWatcher = new LckStorageWatcher(OnLowStorageSpace);

            InitTrackTexture(descriptor.cameraTrackDescriptor);

            LckMediator.CameraRegistered += OnCameraRegistered;
            LckMediator.CameraUnregistered += OnCameraUnregistered;
            LckMediator.MonitorRegistered += OnMonitorRegistered;
            LckMediator.MonitorUnregistered += OnMonitorUnregistered;

            LckMonoBehaviourMediator.StartCoroutine("LckMixer:Update", Update());
        }

        private void OnRecordingSavedCallback(LckResult<RecordingData> result)
        {
            _recordingState = RecordingState.Idle;
            _onRecordingSaved?.Invoke(result);
        }
        
        private void OnPhotoSavedCallback(LckResult result)
        {
            _onPhotoSaved?.Invoke(result);
        }

        private void OnLowStorageSpace(LckResult result)
        {
            StopRecording(LckService.StopReason.LowStorageSpace);
            _onLowStorageSpace?.Invoke(result);
        }

        private void InitTrackTexture(CameraTrackDescriptor cameraTrackDescriptor)
        {
            _cameraTrackTexture = InitializeVideoTrack(cameraTrackDescriptor);

            var cameras = LckMediator.GetCameras();
            if (!_cameraTrackTexture) return;

            if (_activeCamera == null)
            {
                foreach (var camera in cameras)
                {
                    ActivateCameraById(camera.CameraId);
                    break;
                }
            }
            else
            {
                ActivateCameraById(_activeCamera.CameraId);
            }
            
            _photoCapture?.SetRenderTexture(_cameraTrackTexture);

            SetMonitorTextureForAllMonitors();
        }

        private RenderTexture InitializeVideoTrack(CameraTrackDescriptor cameraTrackDescriptor)
        {
            ReleaseCameraTrackTextures();

#if UNITY_2020
            RenderTextureDescriptor renderTextureDescriptor =
 new RenderTextureDescriptor((int)cameraTrackDescriptor.CameraResolutionDescriptor.Width, (int)cameraTrackDescriptor.CameraResolutionDescriptor.Height,
                RenderTextureFormat.ARGB32,  LckSettings.Instance.EnableStencilSupport ?  24 : 16)
#else
            RenderTextureDescriptor renderTextureDescriptor = new RenderTextureDescriptor(
                (int)cameraTrackDescriptor.CameraResolutionDescriptor.Width,
                (int)cameraTrackDescriptor.CameraResolutionDescriptor.Height,
                GraphicsFormat.R8G8B8A8_UNorm,
                LckSettings.Instance.EnableStencilSupport ? GraphicsFormat.D24_UNorm_S8_UInt : GraphicsFormat.D16_UNorm)
#endif
            {
                memoryless = RenderTextureMemoryless.None,
                useMipMap = false,
                msaaSamples = 1,
                sRGB = true,
            };

            var renderTexture = new RenderTexture(renderTextureDescriptor);
            renderTexture.antiAliasing = 1;
            renderTexture.filterMode = FilterMode.Point;
            renderTexture.name = "LCK RenderTexture";
            renderTexture.Create();

            //NOTE: These need to be called twice to make sure the ptr is available
            renderTexture.GetNativeTexturePtr();
            renderTexture.GetNativeDepthBufferPtr();

            _cameraTrackTexture = renderTexture;

            _cameraTrack = cameraTrackDescriptor;
            return _cameraTrackTexture;
        }

        private void ReleaseCameraTrackTextures()
        {
            if (_recordingState != RecordingState.Idle)
            {
                LckLog.LogWarning("LCK Can't release render textures while recording.");
                return;
            }

            if (_cameraTrackTexture)
                _cameraTrackTexture.Release();
        }

        public void EarlyUpdate()
        {
            if (_recordingState == RecordingState.Recording && _recorder != null)
            {
                EncodeFrame();
            }
            else
            {
                UnregisterEncodeFrameEarlyUpdate();
            }
        }

        private bool CaptureCanBeCulled()
        {
            if (_recordingState != RecordingState.Idle)
                return false;

            if (_shouldCapturePreview)
                return false;

            return true;
        }

        private IEnumerator Update()
        {
            var overflow = 0.0;
            var renderStopwatch = new Stopwatch();
            renderStopwatch.Start();

            while (true)
            {
                if (_activeCamera != null)
                {
                    var frameTime = 1.0 / (double)_cameraTrack.Framerate;
                    if (renderStopwatch.Elapsed.TotalSeconds + overflow >= frameTime || WaitingForEncodingWarmupFrames())
                    {
                        overflow = (overflow + renderStopwatch.Elapsed.TotalSeconds - frameTime) % frameTime;
                        renderStopwatch.Restart();

                        if (CaptureCanBeCulled())
                        {
                            _frameHasBeenRendered = false;
                            _activeCamera.DeactivateCamera();
                        }
                        else
                        {
                            _frameHasBeenRendered = true;
                            _activeCamera.ActivateCamera(_cameraTrackTexture);
                        }
                    }
                    else
                    {
                        _frameHasBeenRendered = false;
                        _activeCamera.DeactivateCamera();
                    }
                }

                switch (_recordingState)
                {
                    case RecordingState.Idle:
                        if (_shouldStartRecording)
                        {
                            _shouldStartRecording = false;
                            _recordingState = RecordingState.Starting;

                            DoStartRecording();
                        }
                        break;

                    case RecordingState.Recording:
                        if (_shouldStopRecording)
                        {
                            _shouldStopRecording = false;
                            _recordingState = RecordingState.Stopping;

                            DoStopRecording();
                        }
                        break;

                    case RecordingState.Starting:
                    case RecordingState.Stopping:
                        // Wait for callbacks to change state
                        break;
                }

                if (WaitingForEncodingWarmupFrames())
                {
                    _encodingWarmupFramesLeft--;
                    if (_encodingWarmupFramesLeft == 0)
                    {
                        StartEncodingFrames();
                    }
                }

                yield return null;
            }
            // ReSharper disable once IteratorNeverReturns
        }

        private bool WaitingForEncodingWarmupFrames()
        {
            return _encodingWarmupFramesLeft > 0;
        }

        public LckResult ActivateCameraById(string cameraId, string monitorId = null)
        {
            var cameraToActivate = LckMediator.GetCameraById(cameraId);
            if (cameraToActivate != null)
            {
                if (_activeCamera != null)
                {
                    _activeCamera.DeactivateCamera();
                }

                _activeCamera = cameraToActivate;
                _activeCamera.ActivateCamera(_cameraTrackTexture);
                _onActiveCameraSet?.Invoke(LckResult<ILckCamera>.NewSuccess(_activeCamera));
                
                if (!string.IsNullOrEmpty(monitorId))
                {
                    var monitor = LckMediator.GetMonitorById(monitorId);
                    if (monitor != null)
                    {
                        monitor.SetRenderTexture(_cameraTrackTexture);
                    }
                    else
                    {
                        return LckResult.NewError(LckError.MonitorIdNotFound, LckResultMessageBuilder.BuildMonitorIdNotFoundMessage(monitorId, LckMediator.GetMonitors().ToList()));
                    }
                }

                return LckResult.NewSuccess();
            }
            else
            {
                return LckResult.NewError(LckError.CameraIdNotFound, LckResultMessageBuilder.BuildCameraIdNotFoundMessage(cameraId, LckMediator.GetCameras().ToList()));
            }
        }

        public LckResult<ILckCamera> GetActiveCamera()
        {
            return LckResult<ILckCamera>.NewSuccess(_activeCamera);
        }

        public LckResult StopActiveCamera()
        {
            if (_activeCamera != null)
            {
                _activeCamera.DeactivateCamera();
                _activeCamera = null;
                _onActiveCameraSet?.Invoke(LckResult<ILckCamera>.NewSuccess(null));
            }

            _isCapturing = false;
            return LckResult.NewSuccess();
        }

        public LckResult StartRecording()
        {
            if (_recordingState != RecordingState.Idle || _shouldStartRecording || _shouldStopRecording)
            {
                return LckResult.NewError(LckError.RecordingAlreadyStarted, "Recording already started.");
            }

            if (!_lckStorageWatcher.HasEnoughFreeStorage())
            {
                return LckResult.NewError(LckError.NotEnoughStorageSpace, "Not enough storage space.");
            }

            _shouldStartRecording = true;
            return LckResult.NewSuccess();
        }

        private void DoStartRecording()
        {
            List<LckRecorder.TrackInfo> tracks = new List<LckRecorder.TrackInfo>();

            _audioTracks = new[] { new LckRecorder.AudioTrack
            {
                trackIndex = (uint)tracks.Count,
                dataSize = 0,
                timestampSamples = 0,
                data = IntPtr.Zero
            }};

            tracks.Add(new LckRecorder.TrackInfo
            {
                type = LckRecorder.TrackType.Audio,
                bitrate = _cameraTrack.AudioBitrate,
                samplerate = (uint)_sampleRate,
                channels = 2
            });

            int firstVideoTrackIndex = tracks.Count;

            tracks.Add(new LckRecorder.TrackInfo
            {
                type = LckRecorder.TrackType.Video,
                bitrate = _cameraTrack.Bitrate,
                width = _cameraTrack.CameraResolutionDescriptor.Width,
                height = _cameraTrack.CameraResolutionDescriptor.Height,
                framerate = _cameraTrack.Framerate,
            });


            _recorder.Start(tracks, _cameraTrackTexture, firstVideoTrackIndex, OnRecordingStartedCallback);
        }

        private void OnRecordingStartedCallback(LckResult result)
        {
            if (result.Success)
            {
                _recordingState = RecordingState.Recording;
                _encodingWarmupFramesLeft = 3;

                _audioMixer.EnableCapture();
                LckTelemetry.SendTelemetry(new TelemetryEvent(TelemetryEventType.RecordingStarted));
            }
            else
            {
                _recordingState = RecordingState.Idle;
            }


            _onRecordingStarted?.Invoke(result);
        }

        private void StartEncodingFrames()
        {
            _recordingTime = 0;
            _encodedFrames = 0;
            _audioTimestampFrameCount = 0;

            LckUpdateManager.RegisterSingleEarlyUpdate(this);
        }

        private void EncodeFrame()
        {
            var passedTime = Time.unscaledDeltaTime;

            try
            {
                var audioData = _audioMixer.GetMixedAudio();

                // Handle case where audioData is empty on the first frame
                if (audioData != null && audioData.Count == 0 && _audioTimestampFrameCount == 0)
                {
                    for (int i = 0; i < 1024; i++)
                    {
                        audioData.TryAdd(0);
                    }
                }

                if (!IsAudioDataValid(audioData)) return;
                
                using (var nativeGameAudio = new Handle<float[]>(audioData.Buffer))
                {
                    float audioDataDuration = (float)audioData.Count / (_sampleRate * 2); 

                    if (passedTime > 1f)
                    {
                        passedTime = audioDataDuration;
                    }

                    _audioTracks[0].data = nativeGameAudio.ptr();
                    _audioTracks[0].dataSize = (uint)audioData.Count;
                    _audioTracks[0].timestampSamples = _audioTimestampFrameCount; 
                    _audioTracks[0].trackIndex = 0;

                    _readyTracks[0] = _frameHasBeenRendered;

                    // Encode frame
                    if (!_recorder.EncodeFrame(_recordingTime, _readyTracks, _audioTracks))
                    {
                        HandleEncodeFrameError(
                            "LCK EncodeFrame returned false. This indicates a critical error.",
                            new Dictionary<string, object>
                            {
                                { "errorString", "EncodeFrameFailed" },
                                { "message", "LCK EncodeFrame returned false. This indicates a critical error." },
                                { "recordingTime", _recordingTime },
                                { "audioTimestampSamples", _audioTimestampFrameCount }
                            });

                        return;
                    }

                    _audioTimestampFrameCount += (ulong)audioData.Count / 2;

                    if (_readyTracks[0])
                    {
                        _encodedFrames++;
                    }
                }
            }
            catch (Exception e)
            {
                HandleEncodeFrameError(
                    "LCK EncodeFrame failed: " + e.Message,
                    new Dictionary<string, object>
                    {
                        { "errorString", "EncodeFrameFailed" },
                        { "message", e.Message }
                    });
            }

            _recordingTime += passedTime;
        }

        private bool IsAudioDataValid(AudioBuffer audioData)
        {
            if (audioData != null) return true;
            
            HandleEncodeFrameError(
                "LCK Audio data is null",
                new Dictionary<string, object>
                {
                    { "errorString", "EncodeFrameFailed" },
                    { "message", "LCK Audio data is null" },
                    { "recordingTime", _recordingTime },
                    { "audioTimestampSamples", _audioTimestampFrameCount }
                });

            return false;
        }

        private void HandleEncodeFrameError(string errorMessage, Dictionary<string, object> telemetryData)
        {
            LckLog.LogError(errorMessage);
            LckTelemetry.SendTelemetry(new TelemetryEvent(TelemetryEventType.RecorderError, telemetryData));
            StopRecording(LckService.StopReason.Error);
        }

        private void UnregisterEncodeFrameEarlyUpdate()
        {
            LckUpdateManager.UnregisterSingleEarlyUpdate(this);
        }

        public LckResult SetTrackResolution(CameraResolutionDescriptor cameraResolutionDescriptor)
        {
            if (_recordingState != RecordingState.Idle)
            {
                return LckResult.NewError(LckError.CantEditSettingsWhileRecording, "Can't change resolution while recording.");
            }

            _cameraTrack.CameraResolutionDescriptor = cameraResolutionDescriptor;

            try
            {
                InitTrackTexture(_cameraTrack);
            }
            catch (Exception e)
            {
                LckTelemetry.SendTelemetry(new TelemetryEvent(TelemetryEventType.RecorderError, new Dictionary<string, object> { { "errorString", "SetTrackResolutionFailed" }, { "message", e.Message } }));
                return LckResult.NewError(LckError.UnknownError, e.Message);
            }

            return LckResult.NewSuccess();
        }

        public LckResult SetTrackAudioBitrate(uint audioBitrate)
        {
            if (_recordingState != RecordingState.Idle)
            {
                return LckResult.NewError(LckError.CantEditSettingsWhileRecording, "Can't change audio bitrate while recording.");
            }

            _cameraTrack.AudioBitrate = audioBitrate;

            return LckResult.NewSuccess();
        }

        public LckResult SetTrackFramerate(uint framerate)
        {
            if (_recordingState != RecordingState.Idle)
            {
                return LckResult.NewError(LckError.CantEditSettingsWhileRecording, "Can't change framerate while recording.");
            }

            _cameraTrack.Framerate = framerate;
            return LckResult.NewSuccess();
        }

        public LckResult StopRecording(LckService.StopReason stopReason)
        {
            if (_recordingState != RecordingState.Recording || _shouldStartRecording)
            {
                return LckResult.NewError(LckError.NotCurrentlyRecording, "No recording currently in progress to stop.");
            }
            LckLog.Log($"LCK StopRecording triggered with stopreason: {stopReason}");

            _stopReason = stopReason;
            _shouldStopRecording = true;
            
            UnregisterEncodeFrameEarlyUpdate();

            return LckResult.NewSuccess();
        }

        private void DoStopRecording()
        {
            LckLog.Log("LCK Stopping Recording");

            var context = new Dictionary<string, object> {
                { "recording.duration", _recordingTime },
                { "recording.encodedFrames", _encodedFrames },
                { "recording.stopReason", _stopReason.ToString() },
                { "recording.targetFramerate", _cameraTrack.Framerate },
                { "recording.targetBitrate", _cameraTrack.Bitrate },
                { "recording.targetAudioBitrate", _cameraTrack.AudioBitrate },
                { "recording.targetResolutionX", _cameraTrack.CameraResolutionDescriptor.Width },
                { "recording.targetResolutionY", _cameraTrack.CameraResolutionDescriptor.Height },
                { "recording.actualFramerate", (float)_encodedFrames / _recordingTime }
            };
            LckTelemetry.SendTelemetry(new TelemetryEvent(TelemetryEventType.RecordingStopped, context));

            _audioMixer.DisableCapture();
            _recordingTime = 0;
            _encodedFrames = 0;

            _recorder.Stop(OnRecordingStoppedCallback);
        }

        private void OnRecordingStoppedCallback(LckResult result)
        {
            _recordingState = RecordingState.Idle;

            _onRecordingStopped?.Invoke(result);
        }

        public bool IsRecording()
        {
            return _recordingState != RecordingState.Idle;
        }

        public bool IsCapturing()
        {
            return _isCapturing;
        }

        public LckResult CapturePhoto()
        {
            if (_photoCapture == null)
            {
                return LckResult.NewError(LckError.MicrophoneError, $"Failed to Capture Photo, LckPhotoCapture is null");
            }
            
            var context = new Dictionary<string, object> {
                { "photo.targetResolutionX", _cameraTrack.CameraResolutionDescriptor.Width },
                { "photo.targetResolutionY", _cameraTrack.CameraResolutionDescriptor.Height },
                { "photo.format", LckSettings.Instance.ImageCaptureFileFormat.ToString() },
            };
            LckTelemetry.SendTelemetry(new TelemetryEvent(TelemetryEventType.PhotoCaptured, context));
            
            return _photoCapture.Capture();
        }

        public LckResult SetMicrophoneCaptureActive(bool isActive)
        {
            return _audioMixer.SetMicrophoneCaptureActive(isActive);
        }

        public LckResult SetGameAudioMute(bool isMute)
        {
            return _audioMixer.SetGameAudioMute(isMute);
        }

        public LckResult<bool> IsGameAudioMute()
        {
            return _audioMixer.IsGameAudioMute();
        }

        public float GetMicrophoneOutputLevel()
        {
            return _audioMixer.GetMicrophoneOutputLevel();
        }

        public float GetGameOutputLevel()
        {
            return _audioMixer.GetGameOutputLevel();
        }

        private void SetMonitorTextureForAllMonitors()
        {
            foreach (var monitor in LckMediator.GetMonitors())
            {
                SetMonitorRenderTexture(monitor);
            }
        }

        private void SetMonitorRenderTexture(ILckMonitor monitor)
        {
            if (_cameraTrackTexture != null && monitor != null)
            {
                monitor.SetRenderTexture(_cameraTrackTexture);
                _isCapturing = true;
            }
            else
            {
                if (_cameraTrackTexture == null)
                {
                    LckLog.LogWarning($"LCK Camera track texture not found.");
                }
                if (monitor == null)
                {
                    LckLog.LogWarning($"LCK Monitor not found.");
                }
            }
        }

        private void OnCameraRegistered(ILckCamera camera)
        {

        }

        private void OnCameraUnregistered(ILckCamera camera)
        {
            if (_activeCamera == camera)
            {
                StopActiveCamera();
            }
        }

        private void OnMonitorRegistered(ILckMonitor monitor)
        {
            SetMonitorRenderTexture(monitor);
        }

        private static void OnMonitorUnregistered(ILckMonitor monitor)
        {
            monitor?.SetRenderTexture(null);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_recorder != null)
                {
                    _recorder.Dispose();
                    _recorder = null;
                }

                if (_audioMixer != null)
                {
                    _audioMixer.Dispose();
                    _audioMixer = null;
                }

                if (_lckStorageWatcher != null)
                {
                    _lckStorageWatcher.Dispose();
                    _lckStorageWatcher = null;
                }

                LckMediator.CameraRegistered -= OnCameraRegistered;
                LckMediator.CameraUnregistered -= OnCameraUnregistered;
                LckMediator.MonitorRegistered -= OnMonitorRegistered;
                LckMediator.MonitorUnregistered -= OnMonitorUnregistered;
            }
        }

        public LckResult<TimeSpan> GetRecordingDuration()
        {
            if (_recordingState == RecordingState.Idle)
            {
                return LckResult<TimeSpan>.NewError(LckError.NotCurrentlyRecording, "Recording has not been started.");
            }

            return LckResult<TimeSpan>.NewSuccess(TimeSpan.FromSeconds(_recordingTime));
        }

        public LckResult SetTrackBitrate(uint bitrate)
        {
            if (_recordingState != RecordingState.Idle)
            {
                return LckResult.NewError(LckError.CantEditSettingsWhileRecording, "Can't change bitrate while recording.");
            }

            _cameraTrack.Bitrate = bitrate;
            return LckResult.NewSuccess();
        }

        public LckResult SetTrackDescriptor(CameraTrackDescriptor cameraTrackDescriptor)
        {
            if (_recordingState != RecordingState.Idle)
            {
                return LckResult.NewError(LckError.CantEditSettingsWhileRecording, "Can't change track settings while recording.");
            }

            _cameraTrack = cameraTrackDescriptor;

            return SetTrackResolution(cameraTrackDescriptor.CameraResolutionDescriptor);
        }

        public void SetMicrophoneGain(float gain)
        {
            _audioMixer.SetMicrophoneGain(gain);
        }

        public void SetGameAudioGain(float gain)
        {
            _audioMixer.SetGameAudioGain(gain);
        }

        public void SetPreviewActive(bool isActive)
        {
            _shouldCapturePreview = isActive;
        }

        public LckResult<LckDescriptor> GetCurrentTrackDescriptor()
        {
            var descriptor = new LckDescriptor();
            descriptor.cameraTrackDescriptor = _cameraTrack;

            return LckResult<LckDescriptor>.NewSuccess(descriptor);
        }

        ~LckMixer()
        {
            Dispose(false);
        }
    }
}
