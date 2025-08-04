#if LCK_FMOD_2_03
#define LCK_FMOD
#endif
using System;
using System.Runtime.InteropServices;
using Liv.Lck.Collections;
using UnityEngine;

namespace Liv.Lck
{
    internal class LckAudioCaptureFMOD : MonoBehaviour, ILckAudioSource
    {

#if LCK_FMOD
        private FMOD.DSP_READ_CALLBACK mReadCallback;
        private FMOD.DSP mCaptureDSP;
        public static int samplerate = 0;
#endif
        private GCHandle mObjHandle;
        private AudioBuffer _tmpDownmixBuffer = new AudioBuffer(98000);
        private AudioBuffer _tmpAudio = new AudioBuffer(98000);
        private AudioBuffer _audioBuffer = new AudioBuffer(98000);
        private const int channels = 2;

        private bool _isCapturing = false;

        private readonly System.Object _audioThreadLock = new System.Object();

        public bool IsCapturing()
        {
            return _isCapturing;
        }


#if LCK_FMOD
        [AOT.MonoPInvokeCallback(typeof(FMOD.DSP_READ_CALLBACK))]
        static FMOD.RESULT CaptureDSPReadCallback(ref FMOD.DSP_STATE dsp_state, IntPtr inbuffer, IntPtr outbuffer, uint numFrames, int numChannels, ref int outchannels)
        {

#if LCK_FMOD_2_03
            FMOD.DSP_STATE_FUNCTIONS functions = dsp_state.functions;
#else
            FMOD.DSP_STATE_FUNCTIONS functions = (FMOD.DSP_STATE_FUNCTIONS)Marshal.PtrToStructure(dsp_state.functions, typeof(FMOD.DSP_STATE_FUNCTIONS));
#endif

            IntPtr userData;
            functions.getuserdata(ref dsp_state, out userData);
            functions.getsamplerate(ref dsp_state, ref samplerate);

            GCHandle objHandle = GCHandle.FromIntPtr(userData);
            LckAudioCaptureFMOD lckCapture = objHandle.Target as LckAudioCaptureFMOD;

            var numSamples = Math.Min(numFrames * numChannels, lckCapture._tmpAudio.Capacity);

            lckCapture._tmpAudio.TryCopyFrom(inbuffer, (int)numSamples);
            Marshal.Copy(lckCapture._tmpAudio.Buffer, 0, outbuffer, (int)numSamples);

            if (lckCapture._isCapturing)
            {
                if (numChannels == LckAudioCaptureFMOD.channels)
                {
                    lock (lckCapture._audioThreadLock) 
                    {
                        lckCapture._audioBuffer.TryExtendFrom(lckCapture._tmpAudio);
                    }
                } 
                else if (numChannels == 1)
                {
                    lckCapture._tmpDownmixBuffer.Clear();
                    for (int i = 0; i < numFrames; i++)
                    {
                        lckCapture._tmpDownmixBuffer.TryAdd(lckCapture._tmpAudio.Buffer[i]);
                        lckCapture._tmpDownmixBuffer.TryAdd(lckCapture._tmpAudio.Buffer[i]);
                    }

                    lock (lckCapture._audioThreadLock)
                    {
                        lckCapture._audioBuffer.TryExtendFrom(lckCapture._tmpDownmixBuffer);
                    }
                }
                else if (numChannels == 6)
                {
                    lckCapture._tmpDownmixBuffer.Clear();
                    for (int i = 0; i < numFrames; i++)
                    {
                        int baseIndex = i * numChannels;

                        float frontLeft = lckCapture._tmpAudio.Buffer[baseIndex];
                        float frontRight = lckCapture._tmpAudio.Buffer[baseIndex + 1];
                        float center = lckCapture._tmpAudio.Buffer[baseIndex + 2];
                        //NOTE: Ignoring LFE at index 3
                        float backLeft = lckCapture._tmpAudio.Buffer[baseIndex + 4];
                        float backRight = lckCapture._tmpAudio.Buffer[baseIndex + 5];

                        float left = 0.707f * frontLeft + 0.5f * center + 0.354f * backLeft;
                        float right = 0.707f * frontRight + 0.5f * center + 0.354f * backRight;

                        lckCapture._tmpDownmixBuffer.TryAdd(left);
                        lckCapture._tmpDownmixBuffer.TryAdd(right);
                    }

                    lock (lckCapture._audioThreadLock)
                    {
                        lckCapture._audioBuffer.TryExtendFrom(lckCapture._tmpDownmixBuffer);
                    }
                }
                else
                {
                    LckLog.LogError("LCK FMOD: LCK only supports Mono, Stereo or 5.1 input at this time. Got: " + numChannels + " channels");
                }
            }

            return FMOD.RESULT.OK;
        }
#endif

        void Start()
        {
#if LCK_FMOD
            // Assign the callback to a member variable to avoid garbage collection
            mReadCallback = CaptureDSPReadCallback;

            uint bufferLength;
            int numBuffers;
            FMODUnity.RuntimeManager.CoreSystem.getDSPBufferSize(out bufferLength, out numBuffers);

            // Get a handle to this object to pass into the callback
            mObjHandle = GCHandle.Alloc(this);
            if (mObjHandle != null)
            {
                // Define a basic DSP that receives a callback each mix to capture audio
                FMOD.DSP_DESCRIPTION desc = new FMOD.DSP_DESCRIPTION();
                desc.numinputbuffers = 1;
                desc.numoutputbuffers = 1;
                desc.read = mReadCallback;
                desc.userdata = GCHandle.ToIntPtr(mObjHandle);

                // Create an instance of the capture DSP and attach it to the master channel group to capture all audio
                FMOD.ChannelGroup masterCG;
                if (FMODUnity.RuntimeManager.CoreSystem.getMasterChannelGroup(out masterCG) == FMOD.RESULT.OK)
                {
                    if (FMODUnity.RuntimeManager.CoreSystem.createDSP(ref desc, out mCaptureDSP) == FMOD.RESULT.OK)
                    {
                        if (masterCG.addDSP(0, mCaptureDSP) != FMOD.RESULT.OK)
                        {
                            LckLog.LogWarning("LCK FMOD: Unable to add mCaptureDSP to the master channel group");
                        }
                    }
                    else
                    {
                        LckLog.LogWarning("LCK FMOD: Unable to create a DSP: mCaptureDSP");
                    }
                }
                else
                {
                    LckLog.LogWarning("LCK FMOD: Unable to create a master channel group: masterCG");
                }
            }
            else
            {
                LckLog.LogWarning("LCK FMOD: Unable to create a GCHandle: mObjHandle");
            }
#endif
        }

        void OnDestroy()
        {
#if LCK_FMOD
            if (mObjHandle != null)
            {
                // Remove the capture DSP from the master channel group
                FMOD.ChannelGroup masterCG;
                if (FMODUnity.RuntimeManager.CoreSystem.getMasterChannelGroup(out masterCG) == FMOD.RESULT.OK)
                {
                    if (mCaptureDSP.hasHandle())
                    {
                        masterCG.removeDSP(mCaptureDSP);

                        // Release the DSP and free the object handle
                        mCaptureDSP.release();
                    }
                }
                mObjHandle.Free();
            }
#endif
        }

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
            _isCapturing = true;
            _audioBuffer.Clear();
        }

        public void DisableCapture()
        {
            _isCapturing = false;
            _audioBuffer.Clear();
        }
    }
}
