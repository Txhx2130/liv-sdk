using System;
using UnityEngine;

namespace Liv.Lck
{
    internal static class LckAudioLimiterUtils
    {
        public static float ApplySoftClip(float audioIn)
        {
            return audioIn / (0.75f + Mathf.Abs(audioIn * 0.75f));
        }
        
        public static float CalculateAttackCoefficient(float attackTime, int sampleRate)
        {
            return (float)Math.Exp(-1.0 / (attackTime * sampleRate));
        }

        public static float CalculateReleaseCoefficient(float releaseTime, int sampleRate)
        {
            return (float)Math.Exp(-1.0 / (releaseTime * sampleRate));
        }

        public static float UpdateEnvelope(float gainReduction, float envelope, float attackCoeff, float releaseCoeff)
        {
            if (gainReduction < envelope)
            {
                return attackCoeff * envelope + (1.0f - attackCoeff) * gainReduction;
            }
            else
            {
                return releaseCoeff * envelope + (1.0f - releaseCoeff) * gainReduction;
            }
        }

        public static float ApplyGainReduction(float data, float envelope, float makeUpGain)
        {
            return data * envelope * makeUpGain;
        }
    }
}