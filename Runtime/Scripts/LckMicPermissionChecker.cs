using UnityEngine;
#if PLATFORM_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace Liv.Lck
{
    public class LckMicPermissionChecker : MonoBehaviour
    {
        void Awake()
        {
#if PLATFORM_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                Permission.RequestUserPermissions(new string[]
                {
                    Permission.Microphone,
                });
            }
#endif
        }
    }
}
