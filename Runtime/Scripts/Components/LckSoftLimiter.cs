using System;

namespace Liv.Lck
{
    internal class LckSoftLimiter : ILckAudioLimiter
    {
        private readonly float _threshold;
        private readonly float _kneeWidth;
        private readonly float _ratio;
        private readonly float _makeUpGain;
        private readonly float _attackTime;
        private readonly float _releaseTime;

        private float _envelope;

        public LckSoftLimiter(
            float threshold = 0.6f,
            float kneeWidth = 0.2f,
            float ratio = 2f,
            float makeUpGain = 1.0f,
            float attackTime = 0.01f,
            float releaseTime = 0.1f)
        {
            _threshold = threshold;
            _kneeWidth = kneeWidth;
            _ratio = ratio;
            _makeUpGain = makeUpGain;
            _attackTime = attackTime;
            _releaseTime = releaseTime;
            _envelope = 0.0f;
        }

        public float ApplyLimiter(float audioIn, int sampleRate)
        {
            float absSample = Math.Abs(audioIn);
            float attackCoeff = LckAudioLimiterUtils.CalculateAttackCoefficient(_attackTime, sampleRate);
            float releaseCoeff = LckAudioLimiterUtils.CalculateReleaseCoefficient(_releaseTime, sampleRate);

            float kneeStart = _threshold - _kneeWidth / 2;
            float kneeEnd = _threshold + _kneeWidth / 2;

            float gainReduction = CalculateSoftKneeGainReduction(absSample, kneeStart, kneeEnd);
            _envelope = LckAudioLimiterUtils.UpdateEnvelope(gainReduction, _envelope, attackCoeff, releaseCoeff);

            return LckAudioLimiterUtils.ApplyGainReduction(audioIn, _envelope, _makeUpGain);
        }

        private float CalculateSoftKneeGainReduction(float absSample, float kneeStart, float kneeEnd)
        {
            if (absSample > kneeStart && absSample < kneeEnd)
            {
                float x = (absSample - kneeStart) / _kneeWidth;
                float softGainReduction = x * x * (3 - 2 * x);
                float compressedLevel = kneeStart + softGainReduction * (absSample - kneeStart) / _ratio;
                return compressedLevel / absSample;
            }

            if (absSample >= kneeEnd)
            {
                float excess = absSample - _threshold;
                float compressedExcess = excess / _ratio;
                float compressedLevel = _threshold + compressedExcess;
                return compressedLevel / absSample;
            }

            return 1.0f;
        }
    }
}