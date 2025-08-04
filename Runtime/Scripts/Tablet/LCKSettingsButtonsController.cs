using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Liv.Lck.Tablet
{
    public enum CameraMode
    {
        Selfie = 0,
        FirstPerson = 1,
        ThirdPerson = 2,
    }

    public class LCKSettingsButtonsController : MonoBehaviour
    {
        public Action<CameraMode> OnCameraModeChanged { get; set; }

        [Header("Camera Mode Settings Groups")]
        [SerializeField]
        private GameObject _selfieSettings;
        [SerializeField]
        private GameObject _firstPersonSettings;
        [SerializeField]
        private GameObject _thirdPersonSettings;

        [Header("Toggle References")]
        [SerializeField]
        private ToggleGroup _toggleGroup;
        [SerializeField]
        private Toggle _selfieToggle;
        [SerializeField]
        private Toggle _firstPersonToggle;
        [SerializeField]
        private Toggle _thirdPersonToggle;

        private Dictionary<CameraMode, GameObject> _settingsDictionary;

        private void Awake()
        {
            _settingsDictionary = new Dictionary<CameraMode, GameObject>
            {
                {CameraMode.Selfie, _selfieSettings},
                {CameraMode.FirstPerson,_firstPersonSettings},
                {CameraMode.ThirdPerson,_thirdPersonSettings}
            };
        }

        private void OnEnable()
        {
            _selfieToggle.group = _toggleGroup;
            _firstPersonToggle.group = _toggleGroup;
            _thirdPersonToggle.group = _toggleGroup;
        }

        public void SwitchCameraModes(CameraMode mode)
        {
            foreach (KeyValuePair<CameraMode, GameObject> entry in _settingsDictionary)
            {
                if (entry.Key.Equals(mode))
                {
                    entry.Value.SetActive(true);
                    OnCameraModeChanged?.Invoke(mode);
                }
                else
                {
                    entry.Value.SetActive(false);
                }
            }
        }
    }
}
