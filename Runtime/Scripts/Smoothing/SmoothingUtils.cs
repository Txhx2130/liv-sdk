using UnityEngine;

namespace Liv.Lck.Smoothing
{
    public static class SmoothingUtils 
    {
        public static Quaternion SmoothDampQuaternion(Quaternion current, Quaternion target, ref Vector3 currentVelocity, float smoothTime)
        {
            return SmoothDampQuaternion(current, target, ref currentVelocity, smoothTime, Time.deltaTime);
        }
        
        internal static Quaternion SmoothDampQuaternion(Quaternion current, Quaternion target, ref Vector3 currentVelocity, float smoothTime, float deltaTime)
        {
            if (deltaTime == 0) return current;
            if (smoothTime == 0) return target;

            Vector3 c = current.eulerAngles;
            Vector3 t = target.eulerAngles;
            return Quaternion.Euler(
                Mathf.SmoothDampAngle(c.x, t.x, ref currentVelocity.x, smoothTime, Mathf.Infinity, deltaTime),
                Mathf.SmoothDampAngle(c.y, t.y, ref currentVelocity.y, smoothTime, Mathf.Infinity, deltaTime),
                Mathf.SmoothDampAngle(c.z, t.z, ref currentVelocity.z, smoothTime, Mathf.Infinity, deltaTime)
            );
        }
    }
}
