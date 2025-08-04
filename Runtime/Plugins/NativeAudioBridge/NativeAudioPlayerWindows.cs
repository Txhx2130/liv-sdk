using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Liv.NativeAudioBridge
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
    public class NativeAudioPlayerWindows : INativeAudioPlayer
    {
        private static byte[] _audioByteDataArray;
        private const int BitsPerSample = 16;
        private const string Lib = "winmm.dll";
        private bool _disposed;
        
        [DllImport(Lib)]
        private static extern int waveOutOpen(out IntPtr hWaveOut, int uDeviceID, WaveFormat lpFormat, WaveOutProc dwCallback, int dwInstance, int dwFlags);

        [DllImport(Lib)]
        private static extern int waveOutPrepareHeader(IntPtr hWaveOut, ref WaveHdr lpWaveOutHdr, int uSize);

        [DllImport(Lib)]
        private static extern int waveOutWrite(IntPtr hWaveOut, ref WaveHdr lpWaveOutHdr, int uSize);

        [DllImport(Lib)]
        private static extern int waveOutUnprepareHeader(IntPtr hWaveOut, ref WaveHdr lpWaveOutHdr, int uSize);

        [DllImport(Lib)]
        private static extern int waveOutClose(IntPtr hWaveOut);

        private delegate void WaveOutProc(IntPtr hwo, int uMsg, int dwInstance, int dwParam1, int dwParam2);

        private Dictionary<int, PreloadedAudio> _audioClips = new Dictionary<int, PreloadedAudio>();

        private struct PreloadedAudio
        {
            public GCHandle DataHandle;
            public WaveFormat Format;
            public int BufferLength;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct WaveFormat
        {
            public short wFormatTag;
            public short nChannels;
            public int nSamplesPerSec;
            public int nAvgBytesPerSec;
            public short nBlockAlign;
            public short wBitsPerSample;
            public short cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WaveHdr
        {
            public IntPtr lpData;
            public int dwBufferLength;
            public int dwBytesRecorded;
            public IntPtr dwUser;
            public int dwFlags;
            public int dwLoops;
            public IntPtr lpNext;
            public int reserved;
        }

        /// <summary>
        /// Native Audio clips can be preloaded to ensure optimal runtime performance
        /// </summary>
        /// <param name="audioClip"></param>
        /// <param name="volume">Volume will be set per clip on preload. This is not modifiable thereafter</param>
        public void PreloadAudioClip(AudioClip audioClip, float volume, bool forceReload = false)
        {
            ValidateAudioClipForPreloading(audioClip);
            
            PreloadAudioClip(audioClip.GetHashCode(), PrepareAudioData(audioClip, volume), audioClip.frequency, audioClip.channels,
                BitsPerSample, forceReload);
        }

        /// <summary>
        /// Play a preloaded audio clip. If a clip is not reloaded, it will be preloaded before playing.
        /// </summary>
        /// <param name="audioClip"></param>
        /// <param name="volume">Volume will be set only when the audioClip is not preloaded</param>
        /// <exception cref="InvalidOperationException"></exception>
        public void PlayAudioClip(AudioClip audioClip, float volume = 1f)
        {
            if (!audioClip)
            {
                throw new InvalidOperationException($"LCK: Native Audio can not play AudioClip, audio clip is null.");
            }

            var audioClipId = audioClip.GetHashCode();

            if (!_audioClips.ContainsKey(audioClipId))
            {
                PreloadAudioClip(audioClipId, PrepareAudioData(audioClip, volume), audioClip.frequency, audioClip.channels, BitsPerSample, false);
            }

            Task.Run(async () => await PlayAudio(audioClipId));
        }

        public void StopAllAudio()
        {
            throw new NotImplementedException();
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
            }

            foreach (var audioClip in _audioClips)
            {
                if (audioClip.Value.DataHandle.IsAllocated)
                {
                    audioClip.Value.DataHandle.Free();
                }
            }
            
            _audioClips.Clear();

            _disposed = true;
        }
        
        ~NativeAudioPlayerWindows()
        {
            Dispose(false);
        }
        
        private void ValidateAudioClipForPreloading(AudioClip audioClip)
        {
            if (!audioClip)
            {
                throw new InvalidOperationException("Native Audio can not preload AudioClip, audio clip is null.");
            }
        }
        
        private void PreloadAudioClip(int key, byte[] audioData, int sampleRate, int channels, int bitsPerSample,
            bool forceReload)
        {
            if (!forceReload && _audioClips.ContainsKey(key))
                return;

            if (forceReload && _audioClips.ContainsKey(key))
            {
                UnloadAudioClip(key);
            }
            
            WaveFormat format = new WaveFormat
            {
                wFormatTag = 1, // PCM
                nChannels = (short)channels,
                nSamplesPerSec = sampleRate,
                wBitsPerSample = (short)bitsPerSample,
                nBlockAlign = (short)(channels * bitsPerSample / 8),
                nAvgBytesPerSec = sampleRate * channels * bitsPerSample / 8,
                cbSize = 0
            };
        
            GCHandle hData = GCHandle.Alloc(audioData, GCHandleType.Pinned);
            _audioClips[key] = new PreloadedAudio()
            {
                DataHandle = hData,
                BufferLength = audioData.Length,
                Format = format
            };
        }
                
        private void UnloadAudioClip(int audioClipKey)
        {
            var audioClipId = audioClipKey.GetHashCode();

            if (!_audioClips.ContainsKey(audioClipId))
            {
                throw new InvalidOperationException($"LCK: Native Audio cannot unload AudioClip ({audioClipKey}), it is not preloaded.");
            }

            var preloadedAudio = _audioClips[audioClipId];
            if (preloadedAudio.DataHandle.IsAllocated)
            {
                preloadedAudio.DataHandle.Free();
            }

            _audioClips.Remove(audioClipId);
        }
        
        private static byte[] PrepareAudioData(AudioClip clip, float volume)
        {
            var audioData = ConvertAudioClipToByteArray(clip, volume);
            return audioData;
        }
        
        private static byte[] ConvertAudioClipToByteArray(AudioClip clip, float volume)
        {
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            var byteArray = new byte[samples.Length * 2];
            var rescaleFactor = 32767;

            for (var i = 0; i < samples.Length; i++)
            {
                var adjustedSample = samples[i] * volume;
                adjustedSample = Mathf.Clamp(adjustedSample, -1f, 1f);

                var value = (short)(adjustedSample * rescaleFactor);
                byteArray[i * 2] = (byte)(value & 0x00ff);
                byteArray[i * 2 + 1] = (byte)((value & 0xff00) >> 8);
            }

            return byteArray;
        }

        private Task PlayAudio(int audioClipId)
        {
            var audioClip = _audioClips[audioClipId];

            var result = waveOutOpen(out var waveOutHandle, -1, audioClip.Format, null, 0, 0);
            if (result != 0)
            {
                throw new InvalidOperationException("Failed to open waveform audio device.");
            }

            var header = new WaveHdr
            {
                lpData = audioClip.DataHandle.AddrOfPinnedObject(),
                dwBufferLength = audioClip.BufferLength,
                dwFlags = 0,
                dwLoops = 0,
                dwUser = GCHandle.ToIntPtr(audioClip.DataHandle)
            };

            waveOutPrepareHeader(waveOutHandle, ref header, Marshal.SizeOf(header));
            waveOutWrite(waveOutHandle, ref header, Marshal.SizeOf(header));

            while ((header.dwFlags & 1) != 1) 
            {
                Thread.Sleep(100);
            }
        
            waveOutUnprepareHeader(waveOutHandle, ref header, Marshal.SizeOf(header));
            waveOutClose(waveOutHandle);
        
            return Task.CompletedTask;
        }

    }
#endif

}
