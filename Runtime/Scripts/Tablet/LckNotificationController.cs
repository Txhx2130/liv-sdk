using System.Collections;
using Liv.Lck.Recorder;
using UnityEngine;

namespace Liv.Lck.Tablet
{
    public class LckNotificationController : MonoBehaviour
    {
        [SerializeField]
        private GameObject _ui;

        [SerializeField]
        private GameObject _questMessage;

        [SerializeField]
        private GameObject _pcVideosMessage;

        [SerializeField]
        private GameObject _pcPhotosMessage;

        [SerializeField]
        private LckOnScreenUIController _onScreenUIController;

        [field: SerializeField] public float NotificationShowDuration { get; private set; } = 3f;

        private LckService _lckService;

        private bool _isAndroidPlatform = false;

        private void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _isAndroidPlatform = true;
#else
            _isAndroidPlatform = false;
#endif
        }

        private void OnEnable()
        {
            _ui.SetActive(false);

            var getService = LckService.GetService();

            if (!getService.Success)
            {
                Debug.LogWarning("Could not get LCK Service" + getService.Error);
                return;
            }

            _lckService = LckService.GetService().Result;

            _lckService.OnRecordingStarted += OnRecordingStarted;
            _lckService.OnRecordingSaved += OnRecordingSaved;
        }

        private void OnDisable()
        {
            _lckService.OnRecordingStarted -= OnRecordingStarted;
            _lckService.OnRecordingSaved -= OnRecordingSaved;
            
            // if tablet disabled, reset notification
            StopAllCoroutines();
            _ui.SetActive(false);
        }

        private void OnRecordingStarted(LckResult result)
        {
            _ui.SetActive(false);
            StopAllCoroutines();
        }

        private void OnRecordingSaved(LckResult<RecordingData> result)
        {
            if (!result.Success)
            {
                Debug.LogWarning(
                    "Failed to create notification. Error: "
                        + result.Error
                        + " Message: "
                        + result.Message
                );
                return;
            }

            ShowVideosNotification();
        }

        public void ShowVideosNotification()
        {
            StopAllCoroutines();
            ConfigureNotificationVisuals(false);
            StartCoroutine(NotificationTimer());
        }

        public void ShowPhotoNotification()
        {
            StopAllCoroutines();
            ConfigureNotificationVisuals(true);
            StartCoroutine(NotificationTimer());
        }

        private void ConfigureNotificationVisuals(bool isPhotosNotification)
        {
            _questMessage.SetActive(false);
            _pcVideosMessage.SetActive(false);
            _pcPhotosMessage.SetActive(false);

            if (_isAndroidPlatform == true)
            {
                _questMessage.SetActive(true);
                return;
            }

            if (isPhotosNotification == true)
            {
                _pcPhotosMessage.SetActive(true);
            }
            else
            {
                _pcVideosMessage.SetActive(true);
            }
        }

        private IEnumerator NotificationTimer()
        {
            _onScreenUIController.OnNotificationStarted();
            _ui.SetActive(true);
            yield return new WaitForSeconds(NotificationShowDuration);
            _ui.SetActive(false);
            _onScreenUIController.OnNotificationEnded();
        }

        // Working implementation for opening the PC videos folder if ever needed
        //public void OpenPCVideosFolder()
        //{
        //#if UNITY_ANDROID && !UNITY_EDITOR
        //    return;
        //#endif
        //    Application.OpenURL(
        //        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        //        LckSettings.Instance.RecordingAlbumName));
        //}
    }
}
