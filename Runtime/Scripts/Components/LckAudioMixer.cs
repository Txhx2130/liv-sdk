#if LCK_FMOD_2_03
#define LCK_FMOD
#endif
using System.Collections.Generic;
using Liv.Lck.NativeMicrophone;
using UnityEngine;
using Liv.Lck.Settings;
using Unity.Profiling;
using Liv.Lck.Collections;
#if PLATFORM_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace Liv.Lck
{
    internal class LckAudioMixer : ILckAudioMixer, ILckLateUpdate
    {
        private ILckAudioSource _gameAudioSource;
        private bool _isGameAudioMuted = false;
        private float _gameAudioGain = 1.0f;
        private Queue<float> _gameAudioQueue = new Queue<float>();

        private ILckAudioSource _nativeMicrophoneCapture;
        private bool _isMicrophoneMuted = false;
        private float _microphoneGain = 1.0f;
        private Queue<float> _microphoneQueue = new Queue<float>();

        private AudioBuffer _micAudioBuffer = new AudioBuffer(96000);
        private float _lastMicrophoneLevel;

        private AudioBuffer _gameAudioBuffer = new AudioBuffer(96000);
        private float _lastGameAudioLevel;

        private AudioBuffer _mixedAudioBuffer = new AudioBuffer(96000);
        private int _remainingGameAudioSamplesToAdjust = 0;
        static readonly ProfilerMarker _lateUpdateProfileMarker = new ProfilerMarker("LckAudioMixer.LateUpdate");
        private readonly int _sampleRate;
        private Component _audioCaptureMarker;

        private const int _targetAudioBufferLength = 1024;

        private ILckAudioLimiter _lckAudioLimiterHard;
        private ILckAudioLimiter _lckAudioLimiterSoft;
        private ILckAudioLimiter _lckAudioLimiterCurve;
        
        public LckAudioMixer(int sampleRate)
        {
            _sampleRate = sampleRate;

            VerifyAudioCaptureComponent();

            _nativeMicrophoneCapture = new LckNativeMicrophone(_sampleRate);

            var settings = AudioSettings.GetConfiguration();

            LckUpdateManager.RegisterSingleLateUpdate(this);
            
            _lckAudioLimiterHard = new LckHardLimiter(threshold: 0.65f, ratio: 6f);
            _lckAudioLimiterSoft = new LckSoftLimiter(threshold: 0.6f, kneeWidth: 0.8f, ratio: 12f);
        }

        public AudioBuffer GetMixedAudio()
        {
            return MixAudioArrays();
        }
        
        public void EnableCapture()
        {
            VerifyAudioCaptureComponent();

            if(_gameAudioSource != null)
            {
                _gameAudioSource.EnableCapture();

                _microphoneQueue.Clear();
                _gameAudioQueue.Clear();

                // HACK: This is a workaround for the delay we are observing in the game audio.
                //       Ideally we resolve this delay at its source
                //

                // Calculate the number of samples to adjust for game audio
                _remainingGameAudioSamplesToAdjust = Mathf.CeilToInt((LckSettings.Instance.GameAudioSyncTimeOffsetInMS / 1000f) * _sampleRate) * 2;
            }
        }

        public void DisableCapture()
        {
            if(_gameAudioSource != null)
            {
                _gameAudioSource.DisableCapture();
                _microphoneQueue.Clear();
                _gameAudioQueue.Clear();
            }
        }
        
        public static int GetSampleRate()
        {
#if LCK_WWISE
            return (int)AkSoundEngine.GetSampleRate();
#elif LCK_FMOD
            FMODUnity.RuntimeManager.CoreSystem.getSoftwareFormat(out int sampleRate, out _, out _);
            return sampleRate;
#endif

#if LCK_NOT_UNITY_AUDIO
            return LckSettings.Instance.FallbackSampleRate;
#else
            return AudioSettings.outputSampleRate;
#endif
        }
        
        private AudioBuffer MixAudioArrays()
        {
            if(_gameAudioSource == null)
            {
                LckLog.LogError("LCK No game audio source found");
                return null;
            }
            
            if (_remainingGameAudioSamplesToAdjust > 0)
            {
                while (_remainingGameAudioSamplesToAdjust > 0 && _gameAudioQueue.Count < _remainingGameAudioSamplesToAdjust)
                {
                    _gameAudioQueue.Enqueue(0f);
                    _remainingGameAudioSamplesToAdjust--;
                }
            }

            for(var i = 0; i < _gameAudioBuffer.Count; i++)
            {
                _gameAudioQueue.Enqueue(_gameAudioBuffer[i] * _gameAudioGain * (_isGameAudioMuted ? 0.0f : 1.0f));
            }
                
            if (_remainingGameAudioSamplesToAdjust < 0)
            {
                int samplesToRemove = Mathf.Min(Mathf.Abs(_remainingGameAudioSamplesToAdjust), _gameAudioQueue.Count);
                for (int i = 0; i < samplesToRemove; i++)
                {
                    _gameAudioQueue.Dequeue();
                }
            
                _remainingGameAudioSamplesToAdjust += samplesToRemove;
            }
            
            var blocksAvailable = _gameAudioQueue.Count / _targetAudioBufferLength;

            if (_nativeMicrophoneCapture.IsCapturing())
            {
                if (_micAudioBuffer != null)
                {
                    for(var i = 0; i < _micAudioBuffer.Count; i++)
                    {
                        _microphoneQueue.Enqueue(_micAudioBuffer[i] * _microphoneGain * (_isMicrophoneMuted ? 0.0f : 1.0f));
                    }
                }

                blocksAvailable = Mathf.Min(blocksAvailable, _microphoneQueue.Count / _targetAudioBufferLength);
            }
            else
            {
                _microphoneQueue.Clear();
            }

            var outputLength = blocksAvailable * _targetAudioBufferLength;

            _mixedAudioBuffer.Clear();

            if(_nativeMicrophoneCapture.IsCapturing())
            {
                for(int i = 0; i < outputLength; i++)
                {
                    var mixedAudioRaw = _microphoneQueue.Dequeue() + _gameAudioQueue.Dequeue();
                    var finalMixedAudio = ApplyLimiter(mixedAudioRaw);

                    if(!_mixedAudioBuffer.TryAdd(finalMixedAudio))
                    {
                        LckLog.LogWarning("LCK Mixed audio buffer overflow");
                        break;
                    }
                }
            }
            else
            {
                for(int i = 0; i < outputLength; i++)
                {
                    var mixedAudioRaw = _gameAudioQueue.Dequeue();
                    var finalMixedAudio = ApplyLimiter(mixedAudioRaw);
                    
                    if(!_mixedAudioBuffer.TryAdd(finalMixedAudio))
                    {
                        LckLog.LogWarning("LCK Mixed audio buffer overflow");
                        break;
                    }
                }
            }
            
            return _mixedAudioBuffer;
        }

        private float ApplyLimiter(float mixedAudioRaw)
        {
            var finalMixedAudio = mixedAudioRaw;

            switch (LckSettings.Instance.AudioLimiter)
            {
                case LckSettings.LimiterType.SoftClip:
                    finalMixedAudio = LckAudioLimiterUtils.ApplySoftClip(mixedAudioRaw);
                    break;
                case LckSettings.LimiterType.None:
                    finalMixedAudio = mixedAudioRaw;
                    break;
            }

            return finalMixedAudio;
        }

        public void LateUpdate()
        {
            using (_lateUpdateProfileMarker.Auto())
            {
                if(!VerifyAudioCaptureComponent())
                {
                    return;
                }

                _nativeMicrophoneCapture.GetAudioData(MicrophoneAudioDataCallback);
                _gameAudioSource.GetAudioData(GameAudioDataCallback);
            }
        }

        private void MicrophoneAudioDataCallback(AudioBuffer audioBuffer)
        {
            _micAudioBuffer.Clear();

            if(audioBuffer.Count > 0)
            {
                if(!_micAudioBuffer.TryCopyFrom(audioBuffer))
                {
                    LckLog.LogError("LCK Mic audio data copy failed");
                    return;
                }

                _lastMicrophoneLevel = (_lastMicrophoneLevel + CalculateRootMeanSquare(_micAudioBuffer)) / 2.0f;
            }
        }

        private void GameAudioDataCallback(AudioBuffer audioBuffer)
        {
            _gameAudioBuffer.Clear();

            if(audioBuffer.Count > 0)
            {
                if(!_gameAudioBuffer.TryCopyFrom(audioBuffer))
                {
                    LckLog.LogError("LCK Game audio data copy failed");
                    return;
                }
                                
                _lastGameAudioLevel = (_lastGameAudioLevel + CalculateRootMeanSquare(audioBuffer)) / 2.0f;
                
                if (float.IsNaN(_lastGameAudioLevel))
                    _lastGameAudioLevel = 0;
            }
        }

        private bool VerifyAudioCaptureComponent()
        {
            if (_audioCaptureMarker == null)
            {
                var allListeners = LckMonoBehaviourMediator.FindObjectsOfType<AudioListener>(false);
                var activeListeners = new List<AudioListener>();

                for (var index = 0; index < allListeners.Length; index++)
                {
                    var listener = allListeners[index];
                    if (listener.enabled)
                    {
                        activeListeners.Add(listener);
                    }
                }

                if (activeListeners.Count == 0)
                {
                    LckLog.Log("LCK Found no audio listener in the scene, looking for AudioCaptureMarker");

                    var markers = LckMonoBehaviourMediator.FindObjectsOfType<LckAudioMarker>(false);

                    if (markers.Length > 0)
                    {
                        _audioCaptureMarker = markers[0];
                    }

                    if (markers.Length > 1)
                    {
                        LckLog.LogError("LCK found more than one AudioCaptureMarker in the scene. This is not valid");
                    }
                }
                else
                {
                    if (activeListeners.Count > 0)
                    {
                        _audioCaptureMarker = activeListeners[0];
                    }

                    if (activeListeners.Count > 1)
                    {
                        LckLog.LogError("LCK found more than one active AudioListener in the scene. This is not valid");
                    }
                }
            }

            if (_gameAudioSource == null)
            {
                _gameAudioSource = _audioCaptureMarker.gameObject.GetComponent<ILckAudioSource>();

                if (_gameAudioSource == null)
                {
#if LCK_FMOD
                    _gameAudioSource = _audioCaptureMarker.gameObject.AddComponent<LckAudioCaptureFMOD>();
#elif LCK_WWISE
                    _gameAudioSource = _audioCaptureMarker.gameObject.AddComponent<LckAudioCaptureWwise>();
#else
                    _gameAudioSource = _audioCaptureMarker.gameObject.AddComponent<LckAudioCapture>();
#endif
                }

            }

            return true;
        }

        private bool CheckMicAudioPermissions()
        {
#if PLATFORM_ANDROID && !UNITY_EDITOR
            return Permission.HasUserAuthorizedPermission(Permission.Microphone);
#else
            return true;
#endif
        }

        public LckResult SetMicrophoneCaptureActive(bool active)
        {
            _lastMicrophoneLevel = 0;

            if (!CheckMicAudioPermissions())
            {
                return LckResult.NewError(LckError.MicrophonePermissionDenied,
                    "The app has not been granted microphone permissions.");
            }

            if (active)
                _nativeMicrophoneCapture.EnableCapture();
            else
                _nativeMicrophoneCapture.DisableCapture();

            // TODO: Find a better way to make use of the error codes from NativeMicrophone
            if ( _nativeMicrophoneCapture.IsCapturing() != active)
            {
                return LckResult.NewError(LckError.MicrophoneError, $"Failed to set microphone capture state to {active}");
            }

            return LckResult.NewSuccess();
        }

        public LckResult<bool> GetMicrophoneCaptureActive()
        {
            return LckResult<bool>.NewSuccess(_nativeMicrophoneCapture.IsCapturing());
        }

        public LckResult SetGameAudioMute(bool isMute)
        {
            _isGameAudioMuted = isMute;

            return LckResult.NewSuccess();
        }
        
        public LckResult<bool> IsGameAudioMute()
        {
            return LckResult<bool>.NewSuccess(_isGameAudioMuted);
        }

        public void SetMicrophoneGain(float gain)
        {
            _microphoneGain = gain;
        }

        public void SetGameAudioGain(float gain)
        {
            _gameAudioGain = gain;
        }

        public float GetMicrophoneOutputLevel()
        {
            return _lastMicrophoneLevel;
        }
        
        public float GetGameOutputLevel()
        {
            return _lastGameAudioLevel;
        }

        private static float CalculateRootMeanSquare(AudioBuffer audioBuffer)
        {
            if (audioBuffer == null || audioBuffer.Count == 0)
            {
                return 0;
            }

            float sum = 0;
            for (int i = 0; i < audioBuffer.Count; i++)
            {
                sum += audioBuffer[i] * audioBuffer[i];
            }

            return Mathf.Sqrt(sum / audioBuffer.Count);
        }

        public void Dispose()
        {
            LckUpdateManager.UnregisterSingleLateUpdate(this);

            // TODO: This is not ideal. Could possibly add IDIsposible to ILckAudioSource?
            ( _nativeMicrophoneCapture as LckNativeMicrophone ).Dispose();
        }
    }
}
