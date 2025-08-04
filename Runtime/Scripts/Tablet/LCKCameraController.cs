using Liv.Lck.Recorder;
using Liv.Lck.Smoothing;
using Liv.Lck.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Liv.Lck.Tablet
{
    public class LCKCameraController : MonoBehaviour
    {
        [Header("Options")]
        [SerializeField]
        [Tooltip("If true, any objects in the list ObjectsOnTabletRenderingLayer will have their layer set to the Tablet Rendering Layer, and its cameras will have their culling masks modified for the selfie camera to hide it and first/third person to show it.")]
        private bool _modifyRenderLayerAndCullingMasks = true;
        [SerializeField]
        private string _tabletRenderingLayer = "LCK Tablet";
        [FormerlySerializedAs("_objectsOnTabletRenderingLayer")]
        [SerializeField] 
        [Tooltip("All game objects which should be hidden during selfie recording.")]
        private List<GameObject> _objectsHiddenFromSelfieCamera = new List<GameObject>();

        [SerializeReference]
        private ScriptableObject _qualityConfig;

        [SerializeField]
        [Tooltip("The transform used as the HMD transform for third person camera. If null, the main camera transform will be used.")]
        private Transform _hmdTransform;

        [SerializeField]
        private float _thirdPersonDistanceMultiplier = 0.75f;
        [SerializeField]
        private float _thirdPersonHeightAngle = 25;

        [Header("Main References")]
        [SerializeField]
        private LCKSettingsButtonsController _settingsButtonsController;
        [SerializeField]
        private RectTransform _monitorTransform;
        [SerializeField]
        private LckQualitySelector _qualitySelector;

        [Header("Button References")]
        [Header("Selfie")]
        [SerializeField]
        private LckDoubleButton _selfieFOVDoubleButton;
        [SerializeField]
        private LckDoubleButton _selfieSmoothingDoubleButton;

        [Header("First Person")]
        [SerializeField]
        private LckDoubleButton _firstPersonFOVDoubleButton;
        [SerializeField]
        private LckDoubleButton _firstPersonSmoothingDoubleButton;

        [Header("Third Person")]
        [SerializeField]
        private LckDoubleButton _thirdPersonFOVDoubleButton;
        [SerializeField]
        private LckDoubleButton _thirdPersonSmoothingDoubleButton;
        [SerializeField]
        private LckDoubleButton _thirdPersonDistanceDoubleButton;

        [Header("Portrait Landscape Toggle")]
        [SerializeField]
        private LckButton _orientationButton;

        [Header("Camera Modes")]
        [Header("Selfie")]
        [SerializeField]
        private LckCamera _selfieCamera;
        [SerializeField]
        private LckStabilizer _selfieStabilizer;

        [Header("First Person")]
        [SerializeField]
        private LckCamera _firstPersonCamera;
        [SerializeField]
        private LckStabilizer _firstPersonStabilizer;

        [Header("Third Person")]
        [SerializeField]
        private LckCamera _thirdPersonCamera;

        [SerializeField]
        private LckStabilizer _thirdPersonStabilizer;


        private float _thirdPersonDistance = 1;
        private bool _isThirdPersonFront = true;
        private bool _isThirdPersonFrontPrev;


        private bool _isSelfieFront = true;
        private bool _isHorizontalMode = true;
        private bool _justTransitioned = false;
        private bool _gameAudioRecordingEnabled = true;

        LckService _lckService;

        public static bool ColliderButtonsInUse = false;

        private CameraMode _currentCameraMode = CameraMode.Selfie;

        private CameraTrackDescriptor _horizontalCameraTrackDescriptor;
        private CameraTrackDescriptor _verticalCameraTrackDescriptor;

        private CameraTrackDescriptor CurrentCameraTrackDescriptor
        {
            get => _isHorizontalMode ? _horizontalCameraTrackDescriptor : _verticalCameraTrackDescriptor;
            set
            {
                var resolutionDescriptor = value.CameraResolutionDescriptor;
                if (resolutionDescriptor.Width < resolutionDescriptor.Height)
                {
                    _verticalCameraTrackDescriptor = value;
                    _horizontalCameraTrackDescriptor = GetRotatedCameraTrackDescriptor(value);
                }
                else
                {
                    _verticalCameraTrackDescriptor = GetRotatedCameraTrackDescriptor(value);
                    _horizontalCameraTrackDescriptor = value;
                }
                
                _lckService.SetTrackDescriptor(CurrentCameraTrackDescriptor);
            }
        }
        
        private void OnValidate()
        {
            if(_qualityConfig != null && !(_qualityConfig is ILckQualityConfig))
            {
                Debug.LogError($"LCK Quality Config must implement ILckQualityConfig interface");
            }
        }

        #region UNITY METHODS
        private void Start()
        {
            if(_modifyRenderLayerAndCullingMasks)
            {
                SetTabletLayer();
            }

            if (_hmdTransform == null)
            {
                _hmdTransform = Camera.main.transform;
            }

            var getService = LckService.GetService();

            if (!getService.Success)
            {
                LckLog.LogError($"LCK Could not get Service: {getService.Error}, {getService.Message}");
                _lckService = null;
                return;
            }

            _lckService = getService.Result;

            _qualitySelector.OnQualityOptionSelected += OnQualityOptionSelected;
            _qualitySelector.InitializeOptions((_qualityConfig as ILckQualityConfig).GetQualityOptionsForSystem());


            SetActiveLckCamera(_selfieCamera.CameraId);
            SetSelfieCameraOrientation(Vector3.zero, new Vector3(0, 0, 0));


            var micResult = _lckService.SetMicrophoneCaptureActive(true);
            if (!micResult.Success)
            {
                LckLog.LogError($"LCK Could not enable microphone capture: {micResult.Error}");
            }

            _lckService.OnRecordingStopped += OnRecordingStoppedExternally;
            _lckService.OnRecordingSaved += OnRecordingSaved;
        }

        private void SetTabletLayer()
        {
            int tabletLayer = LayerMask.NameToLayer(_tabletRenderingLayer);
            if (tabletLayer == -1)
            {
                LckLog.LogError($"LCK Tablet layer '{_tabletRenderingLayer}' not found in project layers. Please add it to the project layers or disable ModifyRenderLayerAndCullingMasks");
                return;
            }

            foreach (var objectToHide in _objectsHiddenFromSelfieCamera)
            {
                objectToHide.layer = tabletLayer;
            }

            _selfieCamera.GetCameraComponent().cullingMask &= ~(1 << tabletLayer);

            _firstPersonCamera.GetCameraComponent().cullingMask |= 1 << tabletLayer;
            _thirdPersonCamera.GetCameraComponent().cullingMask |= 1 << tabletLayer;
        }

        private void OnQualityOptionSelected(CameraTrackDescriptor descriptor)
        {
            CurrentCameraTrackDescriptor = descriptor;
        }

        private void OnEnable()
        {        
            var getService = LckService.GetService();

            if(_lckService == null)
            {
                if (!getService.Success)
                {
                    LckLog.LogError($"LCK Could not get Service: {getService.Error}, {getService.Message}");
                    _lckService = null;
                    return;
                }

                _lckService = getService.Result;
            }

            _lckService.SetTrackDescriptor(CurrentCameraTrackDescriptor);

            _settingsButtonsController.OnCameraModeChanged += CameraModeChanged;

            // Selfie
            _selfieFOVDoubleButton.OnValueChanged += ProcessSelfieFov;
            _selfieSmoothingDoubleButton.OnValueChanged += ProcessSelfieSmoothness;

            // First Person
            _firstPersonFOVDoubleButton.OnValueChanged += ProcessFirstPersonFov;
            _firstPersonSmoothingDoubleButton.OnValueChanged += ProcessFirstPersonSmoothness;

            // Third Person
            _thirdPersonFOVDoubleButton.OnValueChanged += ProcessThirdPersonFov;
            _thirdPersonSmoothingDoubleButton.OnValueChanged += ProcessThirdPersonSmoothness;
            _thirdPersonDistanceDoubleButton.OnValueChanged += ProcessThirdPersonDistance;
        }

        private void OnDisable()
        {
            _settingsButtonsController.OnCameraModeChanged -= CameraModeChanged;

            // Selfie
            _selfieFOVDoubleButton.OnValueChanged -= ProcessSelfieFov;
            _selfieSmoothingDoubleButton.OnValueChanged -= ProcessSelfieSmoothness;

            // First Person
            _firstPersonFOVDoubleButton.OnValueChanged -= ProcessFirstPersonFov;
            _firstPersonSmoothingDoubleButton.OnValueChanged -= ProcessFirstPersonSmoothness;

            // Third Person
            _thirdPersonFOVDoubleButton.OnValueChanged -= ProcessThirdPersonFov;
            _thirdPersonSmoothingDoubleButton.OnValueChanged -= ProcessThirdPersonSmoothness;
            _thirdPersonDistanceDoubleButton.OnValueChanged -= ProcessThirdPersonDistance;
        }

        private void OnDestroy()
        {
            if (_lckService != null)
            {
                _lckService.StopRecording();
                _lckService.OnRecordingStopped -= OnRecordingStoppedExternally;
                _lckService.OnRecordingSaved -= OnRecordingSaved;
            }
        }

        private void Update()
        {
            if (_lckService == null)
            {
                return;
            }

            switch (_currentCameraMode)
            {
                case CameraMode.FirstPerson:
                    ProcessFirstCameraPosition();
                    break;
                case CameraMode.ThirdPerson:
                    ProcessThirdCameraPosition();
                    break;
                case CameraMode.Selfie:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        #endregion

        #region SELFIE METHODS
        private void SetSelfieCameraOrientation(Vector3 position, Vector3 rotation)
        {
            _selfieStabilizer.transform.localPosition = position;
            _selfieStabilizer.transform.localRotation = Quaternion.Euler(rotation);
            _selfieStabilizer.ReachTargetInstantly();
        }

        private void ProcessSelfieFov(float value)
        {
            _selfieCamera.GetCameraComponent().fieldOfView = CalculateCorrectFOV(value);
        }

        private void ProcessSelfieSmoothness(float value)
        {
            _selfieStabilizer.PositionalSmoothing = (value * 0.1f * 0.3f);
            _selfieStabilizer.RotationalSmoothing = (value * 0.1f * 0.8f);
        }

        #endregion

        #region FIRST PERSON METHODS
        private void ProcessFirstPersonFov(float value)
        {
            _firstPersonCamera.GetCameraComponent().fieldOfView = CalculateCorrectFOV(value);
        }

        private void ProcessFirstPersonSmoothness(float value)
        {
            _firstPersonStabilizer.PositionalSmoothing = (value * 0.1f * 0.3f);
            _firstPersonStabilizer.RotationalSmoothing = (value * 0.1f * 0.8f);
        }

        private void ProcessFirstCameraPosition()
        {
            _firstPersonStabilizer.transform.position =
                _hmdTransform.transform.position + (_hmdTransform.transform.forward * 0.05f);
            _firstPersonStabilizer.transform.rotation = _hmdTransform.transform.rotation;

            if (_justTransitioned == true)
            {
                _firstPersonStabilizer.ReachTargetInstantly();
                _justTransitioned = false;
            }
        }

        #endregion

        #region THIRD PERSON METHODS
        private void ProcessThirdPersonFov(float value)
        {
            _thirdPersonCamera.GetCameraComponent().fieldOfView = CalculateCorrectFOV(value);
        }

        private void ProcessThirdPersonSmoothness(float value)
        {
            _thirdPersonStabilizer.PositionalSmoothing = (value * 0.1f * 0.3f);
            _thirdPersonStabilizer.RotationalSmoothing = (value * 0.1f * 0.8f);
        }

        private void ProcessThirdPersonDistance(float value)
        {
            _thirdPersonDistance = value;
            _justTransitioned = true;
        }


        private void ProcessThirdCameraPosition()
        {
            Vector3 forward = new Vector3(_hmdTransform.transform.forward.x, 0, _hmdTransform.transform.forward.z);
            forward.Normalize();

            if (!_isThirdPersonFront)
            {
                forward *= -1;
            }

            float forwardAngle = Vector3.SignedAngle(Vector3.forward, forward, Vector3.up);

            Vector3 offset =
                Quaternion.AngleAxis(forwardAngle, Vector3.up)
                * Quaternion.AngleAxis(_thirdPersonHeightAngle, -Vector3.right)
                * new Vector3(0, 0, _thirdPersonDistance * _thirdPersonDistanceMultiplier);

            _thirdPersonStabilizer.transform.position = _hmdTransform.transform.position + offset;
            _thirdPersonStabilizer.transform.LookAt(_hmdTransform.transform.position);

            if (_justTransitioned == true)
            {
                _thirdPersonStabilizer.ReachTargetInstantly();
                _justTransitioned = false;
            }
        }
        #endregion

        private void SetFOV(CameraMode mode, float fov)
        {
            switch (mode)
            {
                case CameraMode.Selfie:
                    _selfieCamera.GetCameraComponent().fieldOfView = fov;
                    break;
                case CameraMode.FirstPerson:
                    _firstPersonCamera.GetCameraComponent().fieldOfView = fov;
                    break;
                case CameraMode.ThirdPerson:
                    _thirdPersonCamera.GetCameraComponent().fieldOfView = fov;
                    break;
                default:
                    break;
            }
        }

        public void ToggleMicrophoneRecording(bool isMicOn)
        {
            var lckService = LckService.GetService();
            if (!lckService.Success)
            {
                LckLog.LogWarning($"LCK Could not get Service {lckService.Error}");
                return;
            }

            var setMicActive = lckService.Result.SetMicrophoneCaptureActive(isMicOn);

            if (
                !setMicActive.Success
                && setMicActive.Error == LckError.MicrophonePermissionDenied
            )
            {
                // set no permission visual here
            }
        }

        public void ToggleGameAudio()
        {
            _gameAudioRecordingEnabled = !_gameAudioRecordingEnabled;

            var getService = LckService.GetService();
            if (!getService.Success)
            {
                LckLog.LogWarning("Could not get LCK Service" + getService.Error);
                return;
            }

            getService.Result.SetGameAudioCaptureActive(_gameAudioRecordingEnabled);
        }

        public void ToggleRecording()
        {
            var lckService = LckService.GetService();

            if (!lckService.Success)
            {
                LckLog.LogWarning($"LCK Could not get Service {lckService.Error}");
                return;
            }

            if (lckService.Result.IsRecording().Result)
            {
                //WARN: will also trigger RecordingStoppedExternally
                lckService.Result.StopRecording();
            }
            else
            {
                lckService.Result.StartRecording();
                RecordingStartedFeedback();
            }
        }
        
        private void RecordingStartedFeedback()
        {
            _orientationButton.SetIsDisabled(true);
        }
        
        private void OnRecordingSaved(LckResult<RecordingData> result)
        {
            RecordingSaved();
        }

        private void RecordingSaved()
        {
            _orientationButton.SetIsDisabled(false);
        }

        private void OnRecordingStoppedExternally(LckResult result)
        {
            _orientationButton.SetIsDisabled(false);
        }
        
        public void ToggleOrientation()
        {
            var lckService = LckService.GetService();
            if (!lckService.Success)
            {
                LckLog.LogError($"LCK Could not get Service {lckService.Error}");
                return;
            }

            if (lckService.Result.IsRecording().Result)
            {
                return;
            }

            _isHorizontalMode = !_isHorizontalMode;
            
            lckService.Result.SetTrackResolution(CurrentCameraTrackDescriptor.CameraResolutionDescriptor);

            // 1109x624 is physical size in mm, a scaled 1920x1080
            if (_isHorizontalMode)
            {
                _monitorTransform.sizeDelta = new Vector2(1109, 624);
            }
            else
            {
                _monitorTransform.sizeDelta = new Vector2(352, 624);
            }

            GetCurrentModeCamera().fieldOfView = CalculateCorrectFOV(GetCurrentModeFOV());
        }

        private Camera GetCurrentModeCamera()
        {
            switch (_currentCameraMode)
            {
                case CameraMode.Selfie:
                    return _selfieCamera.GetCameraComponent();
                case CameraMode.FirstPerson:
                    return _firstPersonCamera.GetCameraComponent();
                case CameraMode.ThirdPerson:
                    return _thirdPersonCamera.GetCameraComponent();
                default:
                    throw new System.Exception("Invalid Camera Mode");
            }
        }

        private float CalculateCorrectFOV(float incomingVerticalFOV)
        {
            if (_isHorizontalMode)
            {
                return incomingVerticalFOV;
            }
            else
            {
                float aspect = (float)_horizontalCameraTrackDescriptor.CameraResolutionDescriptor.Width / (float)_horizontalCameraTrackDescriptor.CameraResolutionDescriptor.Height;
                float horizontalFOV = Camera.VerticalToHorizontalFieldOfView(
                    incomingVerticalFOV,
                    aspect
                );
                return horizontalFOV;
            }
        }

        private float GetCurrentModeFOV()
        {
            switch (_currentCameraMode)
            {
                case CameraMode.Selfie:
                    return _selfieFOVDoubleButton.Value;
                case CameraMode.FirstPerson:
                    return _firstPersonFOVDoubleButton.Value;
                case CameraMode.ThirdPerson:
                    return _thirdPersonFOVDoubleButton.Value;
                default:
                    throw new System.Exception("Invalid Camera Mode");
            }
        }

        public void ProcessSelfieFlip()
        {
            _isSelfieFront = !_isSelfieFront;
            SetMonitorScale(CameraMode.Selfie);

            if (_isSelfieFront)
            {
                SetSelfieCameraOrientation(Vector3.zero, Vector3.zero);
            }
            else
            {
                SetSelfieCameraOrientation(Vector3.zero, new Vector3(0, 180, 0));          
            }

            _selfieStabilizer.ReachTargetInstantly();
        }
        private void SetMonitorScale(CameraMode mode)
        {
            Vector3 negative = new Vector3(-1, 1, 1);
            Vector3 positive = Vector3.one;

            switch (mode)
            {
                case CameraMode.Selfie:
                    _monitorTransform.localScale = _isSelfieFront ? positive : negative;
                    break;
                case CameraMode.FirstPerson:
                    _monitorTransform.localScale = negative;
                    break;
                case CameraMode.ThirdPerson:
                    _monitorTransform.localScale = _isThirdPersonFront ? positive : negative;
                    break;
                default:
                    break;
            }
        }

        public void ProcessThirdPersonPosition()
        {
            _isThirdPersonFront = !_isThirdPersonFront;
            _justTransitioned = true;
            SetMonitorScale(CameraMode.ThirdPerson);
        }

        private void CameraModeChanged(CameraMode mode)
        {
            _currentCameraMode = mode;
            _justTransitioned = true;

            float adjustedFOV = CalculateCorrectFOV(GetCurrentModeFOV());
            SetFOV(_currentCameraMode, adjustedFOV);

            SetMonitorScale(mode);
            switch (mode)
            {
                case CameraMode.Selfie:
                    SetActiveLckCamera(_selfieCamera.CameraId);
                    break;
                case CameraMode.FirstPerson:
                    SetActiveLckCamera(_firstPersonCamera.CameraId);
                    break;
                case CameraMode.ThirdPerson:
                    SetActiveLckCamera(_thirdPersonCamera.CameraId);
                    break;
                default:
                    break;
            }
        }

        private void SetActiveLckCamera(string cameraId)
        {
            LckResult<LckService> getService = LckService.GetService();
            if (!getService.Success)
            {
                LckLog.LogWarning("LCK Could not get Service" + getService.Error);
                return;
            }

            getService.Result?.SetActiveCamera(cameraId);
        }

        private static CameraTrackDescriptor GetRotatedCameraTrackDescriptor(CameraTrackDescriptor cameraTrackDescriptor)
        {
            var origResolution = cameraTrackDescriptor.CameraResolutionDescriptor;
            cameraTrackDescriptor.CameraResolutionDescriptor = new CameraResolutionDescriptor(origResolution.Height, origResolution.Width);
            return cameraTrackDescriptor;
        }
    }
}
