using System;
using UnityEngine;

namespace Liv.Lck.Collections
{
    public class AudioBuffer
    {
        private float[] _buffer;
        private int _logicalCount;

        public int Count => _logicalCount;
        public int Capacity => _buffer.Length;

        public float this[int index] => _buffer[index];


        // TODO: Constructor that doesn't init the entire buffer
        public AudioBuffer(int maxCapacity)
        {
            _buffer = new float[maxCapacity];
            _logicalCount = 0;
        }

        public float[] Buffer => _buffer;

        public void Clear()
        {
            _logicalCount = 0;
        }

        public bool TryAdd(float value)
        {
            if (_logicalCount >= _buffer.Length)
            {
                return false;
            }

            _buffer[_logicalCount] = value;
            _logicalCount++;
            return true;
        }

        public bool TryCopyFrom(float[] source, int sourceIndex, int count)
        {
            if (count > _buffer.Length)
            {
                return false;
            }

            System.Array.Copy(source, sourceIndex, _buffer, 0, count);
            _logicalCount = count;
            return true;
        }

        public bool TryCopyFrom(IntPtr source, int count)
        {
            if (count > _buffer.Length)
            {
                return false;
            }

            System.Runtime.InteropServices.Marshal.Copy(source, _buffer, 0, count);
            _logicalCount = count;
            return true;
        }

        public bool TryCopyFrom(AudioBuffer source)
        {
            if (source._logicalCount > _buffer.Length)
            {
                return false;
            }

            System.Array.Copy(source._buffer, 0, _buffer, 0, source._logicalCount);
            _logicalCount = source._logicalCount;
            return true;
        }

        public bool TryExtendFrom(float[] source)
        {
            if (_logicalCount + source.Length > _buffer.Length)
            {
                return false;
            }

            System.Array.Copy(source, 0, _buffer, _logicalCount, source.Length);
            _logicalCount += source.Length;
            return true;
        }

        public bool TryExtendFrom(AudioBuffer source)
        {
            if (_logicalCount + source._logicalCount > _buffer.Length)
            {
                return false;
            }

            System.Array.Copy(source._buffer, 0, _buffer, _logicalCount, source._logicalCount);
            _logicalCount += source._logicalCount;
            return true;
        }

        public void OverrideCount(int newCount)
        {
            _logicalCount = newCount;
        }
        
        public void PadAudioBuffer(int samplesToPad)
        {
            for (int i = 0; i < samplesToPad; i++)
            {
                TryAdd(0.0f);
            }
        }
        
        public void SkipAudioSamples(int samplesToSkip)
        {
            if (samplesToSkip >= Count)
            {
                Clear();
            }
            else
            {
                var remainingSamples = Count - samplesToSkip;
                Array.Copy(Buffer, samplesToSkip, Buffer, 0, remainingSamples);
                TryCopyFrom(Buffer, 0, remainingSamples);
            }
        }

    }
}
