using System;
using System.Collections.Generic;
using Liv.Lck.Recorder;
using UnityEngine;

namespace Liv.Lck.UI
{
    public class LckQualitySelector : MonoBehaviour
    {
        [SerializeField]
        private LckButton _qualityTogglerButton;

        private CameraTrackDescriptor _currentTrackDescriptor;

        private int _currentQualityIndex = 0;

        private List<QualityOption> _qualityOptions = new List<QualityOption>();

        public Action<CameraTrackDescriptor> OnQualityOptionSelected;

        private LckService _lckService;

        public void InitializeOptions(List<QualityOption> qualityOptions)
        {
            _qualityOptions = qualityOptions;

            var defaultOption = _qualityOptions.FindIndex(x => x.IsDefault);

            if (defaultOption != -1)
            {
                _currentQualityIndex = defaultOption;
            }
            else
            {
                _currentQualityIndex = 0;
            }

            UpdateCurrentTrackDescriptor(_currentQualityIndex);
        }

        private void Start()
        {
            var getService = LckService.GetService();

            if (!getService.Success)
            {
                LckLog.LogError($"LCK Could not get Service: {getService.Error}, {getService.Message}");
                _lckService = null;
                return;
            }

            _lckService = getService.Result;

            _lckService.OnRecordingStarted += OnRecordingStarted;
            _lckService.OnRecordingStopped += OnRecordingStopped;
        }

        public void GoToNextOption()
        {
            if (_currentQualityIndex == _qualityOptions.Count - 1)
            {
                _currentQualityIndex = 0;
            }
            else
            {
                _currentQualityIndex++;
            }

            UpdateCurrentTrackDescriptor(_currentQualityIndex);

        }

        private void UpdateCurrentTrackDescriptor(int index)
        {
            if (_qualityOptions.Count > index)
            {
                _currentTrackDescriptor = _qualityOptions[_currentQualityIndex].CameraTrackDescriptor;
                OnQualityOptionSelected?.Invoke(_currentTrackDescriptor);
                _qualityTogglerButton.SetLabelText(_qualityOptions[_currentQualityIndex].Name);
            }
        }

        private void OnDestroy()
        {
            _lckService.OnRecordingStarted -= OnRecordingStarted;
            _lckService.OnRecordingStopped -= OnRecordingStopped;
        }

        private void OnRecordingStarted(LckResult result)
        {
            _qualityTogglerButton.SetIsDisabled(true);
        }

        private void OnRecordingStopped(LckResult result)
        {
            _qualityTogglerButton.SetIsDisabled(false);
        }
    }       
}
