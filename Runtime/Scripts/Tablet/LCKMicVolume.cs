using UnityEngine;

namespace Liv.Lck.Tablet
{
    [DefaultExecutionOrder(1000)]
    public class LCKMicVolume : MonoBehaviour
    {
        [SerializeField]
        private float _incomingVolume = 0;

        [SerializeField]
        private UnityEngine.UI.Image _micVolumeImage;

        LckService _lckService;

        private void Awake()
        {
            if (_micVolumeImage)
            {
                _micVolumeImage.transform.SetSiblingIndex(0);
            }
        }

        private void OnEnable()
        {
            var lckResult = LckService.GetService();

            if (!lckResult.Success)
            {
                LckLog.LogError($"LCK Could not get Service: {lckResult.Error}, {lckResult.Message}");
                return;
            }

            _lckService = lckResult.Result;
        }

        void Update()
        {
            if(_lckService == null)
            {
                return;
            }

            _incomingVolume = Mathf.Clamp01(_lckService.GetMicrophoneOutputLevel().Result * 10f);

            if (_micVolumeImage)
            {
                _micVolumeImage.fillAmount = _incomingVolume;
            }
        }
    }
}
