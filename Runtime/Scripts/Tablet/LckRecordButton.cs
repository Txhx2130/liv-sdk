using UnityEngine;
using TMPro;
using Liv.Lck.UI;
using Liv.Lck.Recorder;
using System.Threading.Tasks;

namespace Liv.Lck.Tablet
{
    public class LckRecordButton : MonoBehaviour
    {
        [SerializeField]
        private LckDiscreetAudioController _audioController;
        [SerializeField]
        private TMP_Text _recordButtonText;
        [SerializeField]
        private LckToggle _recordToggle;

        private LckService _lckService;

        private enum State
        {
            Idle,
            Saving,
            Recording,
            Error,
        }

        private State _state = State.Idle;

        private void Start()
        {
            EnsureLckService();

            if (_lckService != null)
            {
                _lckService.OnRecordingStarted += OnRecordingStarted;
                _lckService.OnRecordingStopped += OnRecordingStopped;
                _lckService.OnRecordingSaved += OnRecordingSaved;
            }
        }

        private void Update()
        {
            EnsureLckService();

            if (_state == State.Recording && _lckService != null)
            {
                UpdateRecordDurationText();
            }
        }

        private void UpdateRecordDurationText()
        {
            var getRecordingDuration = _lckService.GetRecordingDuration();
            if (!getRecordingDuration.Success)
            {
                return;
            }

            var span = getRecordingDuration.Result;

            int hours = Mathf.FloorToInt(span.Hours);
            int minutes = Mathf.FloorToInt(span.Minutes);
            int seconds = Mathf.FloorToInt(span.Seconds);

            _recordButtonText.text =
                hours == 0 ? $"{minutes:00}:{seconds:00}" : $"{hours:00}:{minutes:00}:{seconds:00}";
        }

        private void OnError()
        {
            _state = State.Error;
            _recordButtonText.text = "ERROR";
            _recordToggle.enabled = false;
            
            _ = ResetAfterError();
        }

        private async Task ResetAfterError()
        {
            await Task.Delay(2000);
            _state = State.Idle;
            ResetButtonVisuals();
        }

        private void OnRecordingStarted(LckResult result)
        {
            if (!result.Success)
            {
                OnError();
                return;
            }

            _audioController.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.RecordingStart);
            _state = State.Recording;
        }

        private void OnRecordingStopped(LckResult result)
        {
            if (!result.Success)
            {
                OnError();
                return;
            }

            _state = State.Saving;

            if (_recordButtonText == null || _recordToggle == null)
                return;
            
            _recordButtonText.text = "SAVING";
            _recordToggle.SetToggleVisualsOff();
            _recordToggle.enabled = false;
        }

        private void OnRecordingSaved(LckResult<RecordingData> result)
        {
            if (!result.Success)
            {
                OnError();
                return;
            }

            _state = State.Idle;
            _audioController.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.RecordingSaved);

            if (_recordButtonText == null || _recordToggle == null)
                return;

            ResetButtonVisuals();
        }

        private void ResetButtonVisuals()
        {
            _recordButtonText.text = "RECORD";
            _recordToggle.SetToggleVisualsOff();
            _recordToggle.enabled = true;
        }

        private void EnsureLckService()
        {
            if (_lckService == null)
            {
                var lckServiceResult = LckService.GetService();

                if (!lckServiceResult.Success)
                {
                    LckLog.LogWarning($"LCK Could not get Service {lckServiceResult.Error}");
                    return;
                }

                _lckService = lckServiceResult.Result;
            }
        }

        private void OnDestroy()
        {
            if (_lckService != null)
            {
                _lckService.OnRecordingStarted -= OnRecordingStarted;
                _lckService.OnRecordingStopped -= OnRecordingStopped;
                _lckService.OnRecordingSaved -= OnRecordingSaved;
            }
        }
    }
}
