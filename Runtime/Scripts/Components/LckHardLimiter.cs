using System;

namespace Liv.Lck
{
    internal class LckHardLimiter : ILckAudioLimiter
    {
        private readonly float _threshold;
        private readonly float _ratio;
        private readonly float _makeUpGain;
        private readonly float _attackTime;
        private readonly float _releaseTime;

        private float _envelope;

        public LckHardLimiter(
            float threshold = 0.6f,
            float ratio = 2f,
            float makeUpGain = 1.0f,
            float attackTime = 0.01f,
            float releaseTime = 0.1f)
        {
            _threshold = threshold;
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

            float gainReduction = absSample > _threshold ? _threshold / absSample : 1.0f;
            _envelope = LckAudioLimiterUtils.UpdateEnvelope(gainReduction, _envelope, attackCoeff, releaseCoeff);

            return LckAudioLimiterUtils.ApplyGainReduction(audioIn, _envelope, _makeUpGain);
        }
    }
}