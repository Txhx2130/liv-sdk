using UnityEngine;
#if UNITY_URP
using UnityEngine.Rendering.Universal;
#endif

// this script fixes an issue with the Meta Interaction SDK,
// it doesn't allow FOV changes when a camera component 'Target Eye' value is set to 'Both' so we set it to 'None' here
namespace Liv.Lck
{
    [RequireComponent(typeof(Camera))]
    public class LckTargetEyeSetter : MonoBehaviour
    {
        private void OnValidate()
        {
            Camera camera = GetComponent<Camera>();
#if UNITY_URP
            if (GetComponent<UniversalAdditionalCameraData>() != null)
            {
                camera.GetComponent<UniversalAdditionalCameraData>().allowXRRendering = false;
                camera.stereoTargetEye = StereoTargetEyeMask.None;
            }
#else
            camera.stereoTargetEye = StereoTargetEyeMask.None;
#endif

        }
    }
}
