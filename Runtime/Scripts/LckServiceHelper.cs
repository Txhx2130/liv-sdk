using Liv.Lck.Recorder;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif


namespace Liv.Lck
{
    [DefaultExecutionOrder(-1000)]
    public class LckServiceHelper : MonoBehaviour
    {
        [SerializeField]
        private uint _framerate = 30;
        [SerializeField]
        private uint _bitrate = 5 << 20;
        [SerializeField]
        private uint _height = 1280;
        [SerializeField]
        private uint _width = 720;
        [SerializeField]
        private uint _audioBitrate = 192000;

#pragma warning disable CS0414 // Removes warning about variable only used on android
        [SerializeField]
        [Tooltip("Ask for microphone permissions on Android")]
        private bool _askForMicPermissions = true;
#pragma warning restore CS0414

        private void Awake()
        {
            var track = new CameraTrackDescriptor {
                CameraResolutionDescriptor = new CameraResolutionDescriptor(_width, _height),
                      Bitrate = _bitrate,
                      Framerate = _framerate,
                      AudioBitrate = _audioBitrate,
            };

            var result = LckService.CreateService(new LckDescriptor {cameraTrackDescriptor = track});

            if (!result.Success)
            {
                Debug.LogError("LCK Could not create Service:" + result.Error + " " + result.Message);
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            if (_askForMicPermissions && !Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                LckLog.Log("Requesting Microphone Permission");
                Permission.RequestUserPermission(Permission.Microphone);
            }
#endif
        }

        private void OnDestroy()
        {
            LckService.DestroyService();
        }
    }
}
