using System.Collections.Generic;
using Liv.Lck.Collections;
using UnityEngine;

namespace Liv.Lck
{
    internal class LckAudioCaptureWwise : MonoBehaviour, ILckAudioSource
    {
        private bool _captureAudio;
        private AudioBuffer _audioBuffer = new AudioBuffer(96000);
#if LCK_WWISE
        private bool initialized = false;
        private ulong OutputDeviceId = 0;
        private uint sampleRate = 0;
#endif

        public bool IsCapturing()
        {
            return _captureAudio;
        }

        // Start is called before the first frame update
        System.Collections.IEnumerator Start()
        {
#if LCK_WWISE
            //OutputDeviceId = AkSoundEngine.GetOutputID(AkSoundEngine.AK_INVALID_UNIQUE_ID, 0);
            while (!AkSoundEngine.IsInitialized())
            {
                yield return null;
            }

            AkOutputSettings outputSettings = new AkOutputSettings
            {
                channelConfig = AkChannelConfig.Standard(AkSoundEngine.AK_SPEAKER_SETUP_STEREO),
                idDevice = 0,
                audioDeviceShareset = AkSoundEngine.AK_INVALID_UNIQUE_ID,
                ePanningRule = AkPanningRule.AkPanningRule_Speakers
            };
            var outputResult = AkSoundEngine.AddOutput(outputSettings, out OutputDeviceId);
            if (outputResult == AKRESULT.AK_Success)
            {
                LckLog.Log($"Wwise device id {OutputDeviceId}");
            }
            else
            {
                LckLog.Log($"Wwise device id FAILED with result: {outputResult.ToString()}");
                yield return null;
            }

            AkAudioSettings audioSettings = new AkAudioSettings();
            while (AkSoundEngine.GetAudioSettings(audioSettings) != AKRESULT.AK_Success)
            {
                yield return null;
            }

            initialized = true;

            sampleRate = AkSoundEngine.GetSampleRate();
            var audioSinkCapabilities = new Ak3DAudioSinkCapabilities();
            AkChannelConfig channelConfig = new AkChannelConfig();
            AkSoundEngine.GetOutputDeviceConfiguration(OutputDeviceId, channelConfig, audioSinkCapabilities);
            LckLog.Log($"Wwise config - Sample Rate: {sampleRate}, Channels: {channelConfig.uNumChannels}");
            LckLog.Log($"Wwise Unity audio samplerate {AudioSettings.outputSampleRate}");
#else
            yield return null;
#endif
        }

        public virtual void EnableCapture()
        {
            LckLog.Log("Wwise: enable capture");
#if LCK_WWISE
            if (!initialized)
                return;
            AkSoundEngine.ClearCaptureData();
            AkSoundEngine.StartDeviceCapture(OutputDeviceId);
#if UNITY_EDITOR
            // Ensure that the editor update does not call AkSoundEngine.RenderAudio().
            AkSoundEngineController.Instance.DisableEditorLateUpdate();
#endif
            _captureAudio = true;
#endif
        }

        public virtual void DisableCapture()
        {
            LckLog.Log("Wwise: disable capture");
#if LCK_WWISE
            if (!initialized)
                return;
            AkSoundEngine.StopDeviceCapture(OutputDeviceId);
#if UNITY_EDITOR
            // Bring back editor update calls to AkSoundEngine.RenderAudio().
            AkSoundEngineController.Instance.EnableEditorLateUpdate();
#endif
            _captureAudio = false;
#endif
        }

        void OnDestroy()
        {
#if LCK_WWISE
            if (!initialized)
                return;
            // TODO: check how to reliably disable the capture when the object is disposed
            // in a networked environment OnDestroy may be called in unpredictable ways
            //DisableCapture();
            //initialized = false;
#endif
            LckLog.Log($"Wwise destroyed");
        }

        public void GetAudioData(ILckAudioSource.AudioDataCallbackDelegate callback)
        {
#if LCK_WWISE
            _audioBuffer.Clear();

            if (initialized && _captureAudio)
            {
                var sampleCount = AkSoundEngine.UpdateCaptureSampleCount(OutputDeviceId);

                var count = AkSoundEngine.GetCaptureSamples(OutputDeviceId, _audioBuffer.Buffer, (uint)sampleCount);
                _audioBuffer.OverrideCount(count);
            }

            callback(_audioBuffer);
#endif
        }
    }
}
