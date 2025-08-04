using System;
using System.Collections.Generic;
using Liv.NGFX;
using Liv.Lck.Recorder;
using Liv.Lck.Settings;
using Liv.Lck.Telemetry;
using Liv.NativeAudioBridge;
using UnityEngine;

namespace Liv.Lck
{
    public class LckDescriptor
    {
        public CameraTrackDescriptor cameraTrackDescriptor;
    }

    public class LckService : IDisposable
    {
        private static LckService _service = null;

        private ILckMixer _mixer;
        private INativeAudioPlayer _nativeAudioPlayer;

        private bool _disposed = false;

        public delegate void LckResultDelegate(LckResult result);
        public delegate void LckResultRecordingDataDelegate(LckResult<RecordingData> result);
        public delegate void LckResultILckCameraDelegate(LckResult<ILckCamera> result);
        public event LckResultDelegate OnRecordingStarted;
        public event LckResultDelegate OnRecordingStopped;
        public event LckResultDelegate OnLowStorageSpace;
        public event LckResultDelegate OnPhotoSaved;
        public event LckResultRecordingDataDelegate OnRecordingSaved;
        public event LckResultILckCameraDelegate OnActiveCameraSet;

        public enum StopReason
        {
            UserStopped,
            LowStorageSpace,
            Error,
            ApplicationLifecycle
        }


        internal LckService(LckDescriptor descriptor)
        {
            _disposed = false;

            _mixer = new LckMixer(
                    descriptor,
                    OnRecordingStartedCallback, 
                    OnRecordingStoppedCallback, 
                    OnLowStorageSpaceCallback,
                    OnPhotoSavedCallback,
                    OnRecordingSavedCallback,
                    OnActiveCameraSetCallback
            );

            _nativeAudioPlayer = NativeAudioPlayerFactory.CreateNativeAudioPlayer();

            NI.SetGlobalLogLevel(LckSettings.Instance.NativeLogLevel, LckSettings.Instance.ShowOpenGLMessages);
        }

        public static LckResult<LckService> GetService()
        {
            if(_service == null)
            {
                return LckResult<LckService>.NewError(LckError.ServiceNotCreated, "Service not created");
            }

            return LckResult<LckService>.NewSuccess(_service);
        }

        public LckResult<TimeSpan> GetRecordingDuration()
        {
            if(_service == null)
            {
                return LckResult<TimeSpan>.NewError(LckError.ServiceNotCreated, "Service not created");
            }

            return _mixer.GetRecordingDuration();
        }

        public static LckResult<LckService> CreateService(LckDescriptor descriptor)
        {
            if(!VerifyPlatform())
            {
                return LckResult<LckService>.NewError(LckError.UnsupportedPlatform, "Unsupported platform");
            }

            if(!VerifyGraphicsApi())
            {
                return LckResult<LckService>.NewError(LckError.UnsupportedGraphicsApi, "Unsupported graphics API");
            }

            if(!VerifyDescriptor(descriptor))
            {
                return LckResult<LckService>.NewError(LckError.InvalidDescriptor, "Invalid descriptor");
            }

            if(_service != null)
            {
                LckLog.LogWarning("LCK service already created, destroying it first");
                DestroyService();
            }

            _service = new LckService(descriptor);
            LckTelemetry.SendTelemetry(new TelemetryEvent(TelemetryEventType.ServiceCreated));
            LckLog.Log("LCK service created");
            return LckResult<LckService>.NewSuccess(_service);
        }

        public static LckResult DestroyService()
        {
            if(_service == null)
            {
                return LckResult.NewError(LckError.ServiceNotCreated, "No existing service to destroy");
            }

            _service.Dispose();
            _service = null;

            LckTelemetry.SendTelemetry(new TelemetryEvent(TelemetryEventType.ServiceDisposed));
            LckLog.Log("LCK service destroyed");
            return LckResult.NewSuccess();
        }

        private void OnRecordingStartedCallback(LckResult result)
        {
            if (!result.Success)
            {
                SendErrorTelemetry(result);
            }

            OnRecordingStarted?.Invoke(result);
        }

        private void OnRecordingStoppedCallback(LckResult result)
        {
            if (!result.Success)
            {
                SendErrorTelemetry(result);
            }

            OnRecordingStopped?.Invoke(result);
        }

        private void OnLowStorageSpaceCallback(LckResult result)
        {
            if (!result.Success)
            {
                SendErrorTelemetry(result);
            }

            OnLowStorageSpace?.Invoke(result);
        }

        private void OnRecordingSavedCallback(LckResult<RecordingData> result)
        {
            OnRecordingSaved?.Invoke(result);
        }
        
        private void OnPhotoSavedCallback(LckResult result)
        {
            if (!result.Success)
            {
                SendErrorTelemetry(result);
            }
            
            OnPhotoSaved?.Invoke(result);
        }
        
        private void OnActiveCameraSetCallback(LckResult<ILckCamera> result)
        {
            OnActiveCameraSet?.Invoke(result);
        }

        private void SendErrorTelemetry(LckResult result)
        {
            var context = new Dictionary<string, object>
            {
                {"error", result.Error},
                {"errorString", result.Error.ToString()},
                {"message", result.Message}
            };

            LckTelemetry.SendTelemetry(new TelemetryEvent(TelemetryEventType.RecorderError, context));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_mixer != null)
                    {
                        _mixer.Dispose();
                        _mixer = null;
                    }

                    if (_nativeAudioPlayer != null)
                    {
                        _nativeAudioPlayer.Dispose();
                        _nativeAudioPlayer = null;
                    }

                    LckMonoBehaviourMediator.StopAllActiveCoroutines();
                }

                _disposed = true;
            }
        }

        ~LckService()
        {
            Dispose(false);
        }

        public LckResult StartRecording()
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return _mixer.StartRecording();
        }

        internal LckResult StopRecording(StopReason stopReason = StopReason.UserStopped)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return _mixer.StopRecording(stopReason);
        }

        public LckResult StopRecording()
        {
            return StopRecording(StopReason.UserStopped);
        }

        public LckResult SetTrackResolution(CameraResolutionDescriptor cameraResolutionDescriptor)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return _mixer.SetTrackResolution(cameraResolutionDescriptor);
        }

        public LckResult SetTrackFramerate(uint framerate)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return _mixer.SetTrackFramerate(framerate);
        }

        public LckResult SetPreviewActive(bool isActive)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            _mixer.SetPreviewActive(isActive);

            return LckResult.NewSuccess();
        }

        public LckResult SetTrackDescriptor(CameraTrackDescriptor cameraTrackDescriptor)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return _mixer.SetTrackDescriptor(cameraTrackDescriptor);
        }

        public LckResult SetTrackBitrate(uint bitrate)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return _mixer.SetTrackBitrate(bitrate);
        }

        public LckResult SetTrackAudioBitrate(uint audioBitrate)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return _mixer.SetTrackAudioBitrate(audioBitrate);
        }

        public LckResult<bool> IsRecording()
        {
            if(_disposed)
            {
                return LckResult<bool>.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return LckResult<bool>.NewSuccess(_mixer.IsRecording());
        }

        public LckResult<bool> IsCapturing()
        {
            if(_disposed)
            {
                return LckResult<bool>.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return LckResult<bool>.NewSuccess(_mixer.IsCapturing());
        }

        public LckResult SetGameAudioCaptureActive(bool isActive)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return _mixer.SetGameAudioMute(!isActive);
        }

        public LckResult SetMicrophoneCaptureActive(bool isActive)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return _mixer.SetMicrophoneCaptureActive(isActive);
        }

        public LckResult<float> GetMicrophoneOutputLevel()
        {
            if(_disposed)
            {
                return LckResult<float>.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return LckResult<float>.NewSuccess(_mixer.GetMicrophoneOutputLevel());
        }

        public LckResult SetMicrophoneGain(float gain)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            _mixer.SetMicrophoneGain(gain);

            return LckResult.NewSuccess();
        }

        public LckResult SetGameAudioGain(float gain)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            _mixer.SetGameAudioGain(gain);

            return LckResult.NewSuccess();
        }

        public LckResult<float> GetGameOutputLevel()
        {
            if(_disposed)
            {
                return LckResult<float>.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return LckResult<float>.NewSuccess(_mixer.GetGameOutputLevel());
        }

        public LckResult<bool> IsGameAudioMute()
        {
            if(_disposed)
            {
                return LckResult<bool>.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return _mixer.IsGameAudioMute();
        }

        public LckResult SetActiveCamera(string cameraId, string monitorId = null)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return _mixer.ActivateCameraById(cameraId, monitorId);
        }
        
        public LckResult<ILckCamera> GetActiveCamera()
        {
            if(_disposed)
            {
                return LckResult<ILckCamera>.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return _mixer.GetActiveCamera();
        }

        public LckResult PreloadDiscreetAudio(AudioClip audioClip, float volume, bool forceReload = false)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            _nativeAudioPlayer?.PreloadAudioClip(audioClip, volume, forceReload);
            return LckResult.NewSuccess();  //TODO: Approach to error handling
        }

        public LckResult PlayDiscreetAudioClip(AudioClip audioClip)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            _nativeAudioPlayer?.PlayAudioClip(audioClip, 1);
            return LckResult.NewSuccess();  //TODO: Approach to error handling
        }

        public LckResult StopAllDiscreetAudio()
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            _nativeAudioPlayer?.StopAllAudio();
            return LckResult.NewSuccess();  //TODO: Approach to error handling
        }

        public LckResult<LckDescriptor> GetDescriptor()
        {
            if(_disposed)
            {
                return LckResult<LckDescriptor>.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return _mixer.GetCurrentTrackDescriptor();
        }
        
        public LckResult CapturePhoto()
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return _mixer.CapturePhoto();
        }

        internal static bool VerifyGraphicsApi()
        {
            var graphicsApi = SystemInfo.graphicsDeviceType;
            switch (Application.platform)
            {
                case RuntimePlatform.Android:
                    if (graphicsApi == UnityEngine.Rendering.GraphicsDeviceType.Vulkan || graphicsApi == UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3)
                    {
                        return true;
                    }
                    LckLog.LogError("LCK requires Vulkan or OpenGLES3 graphics API on Android. Any other api is not supported.");
                    return false;
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                    if (graphicsApi == UnityEngine.Rendering.GraphicsDeviceType.Vulkan || graphicsApi == UnityEngine.Rendering.GraphicsDeviceType.Direct3D11
                        || graphicsApi == UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore)
                    {
                        return true;
                    }
                    LckLog.LogError("LCK requires the Vulkan, OpenGLCore or DirectX 11 graphics API on Windows. Any other api is not supported.");
                    return false;
            }
            return false;
        }

        internal static bool VerifyPlatform()
        {
            var isValidPlatform = (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor);
            if(!isValidPlatform)
            {
                LckLog.LogError($"LCK is not supported on {Application.platform}.");
            }

            return isValidPlatform;
        }

        //TODO:
        private static bool VerifyDescriptor(LckDescriptor descriptor)
        {
            return true;
        }
    }
}
