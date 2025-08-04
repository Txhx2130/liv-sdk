using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Liv.Lck.Settings
{
    public class LckSettings : ScriptableObject
    {
        public const string SettingsPath = "Assets/Resources/LckSettings.asset";

        [SerializeField]
        public string TrackingId = "";
        
        [SerializeField]
        public string GameName = "MyGame";
        
        [Space(10)]
        [SerializeField]
        public string RecordingFilenamePrefix = "MyGamePrefix";

        [SerializeField]
        public string RecordingAlbumName = "MyGameAlbum";

        [SerializeField]
        public string RecordingDateSuffixFormat = "yyyy-MM-dd_HH-mm-ss";

        [Space(10)]
        [Header("Advanced")]
        [SerializeField]
        [Tooltip("Allow LCK to modify the AndroidManifest.xml file to add Microphone permissions. Disable if you want to manually add permissions.")]
        public bool AddPermissionsToAndroidManifest = true;

        [SerializeField]
        [Tooltip(
            "Enabling stencil buffer support allows for advanced rendering effects, such as masking and outlining, to be recorded in the recording. "
                + "UI elements may often utilise the stencil buffer and may otherwise appear incorrect in the recordings. "
                + "Disable to optimise performance if stencil effects are not needed."
        )]
        public bool EnableStencilSupport = true;

        [Space(10)]
        [Header("Logging")]
        [SerializeField]
        public LogLevel BaseLogLevel = LogLevel.Error;

        [SerializeField]
        public Liv.Lck.NativeMicrophone.LogLevel MicrophoneLogLevel = NativeMicrophone.LogLevel.Error;

        [SerializeField]
        public Liv.NGFX.LogLevel NativeLogLevel = Liv.NGFX.LogLevel.Error;

        [SerializeField]
        [Tooltip("OpenGL messages can be useful to debug errors happening at graphics API level.")]
        public bool ShowOpenGLMessages = false;

        [Header("Audio")] 
        [SerializeField] 
        [Tooltip(
            "Game audio may appear ahead or behind the game visuals in your game recordings. This property allows for Game Audio to be shifted forward or backwards " +
            "by the provided milliseconds. Positive values will move the audio forward in time, negative backwards."
        )]
        public float GameAudioSyncTimeOffsetInMS = 250;

        [SerializeField]
        [Tooltip("Enabling the audio limiter results in limiter compression applied to the recordings audio.")]
        public LimiterType AudioLimiter = LimiterType.SoftClip;

        [Serializable]
        public enum LimiterType
        {
            SoftClip,
            None,
        }

        [SerializeField]
        [Tooltip(
            "The sample rate used by LCK if it can't get the samplerate from other sources"
        )]
        public int FallbackSampleRate = 48000;

        [Header("Photo")] 
        [SerializeField] 
        [Tooltip(
            "The format Photo images will be saved in."
        )]
        public ImageFileFormat ImageCaptureFileFormat = ImageFileFormat.PNG;
        [Serializable]
        public enum ImageFileFormat
        {
            EXR = 0,
            JPG = 1,
            TGA = 2,
            PNG = 3
        }
        
        [Header("Telemetry")]
        [SerializeField]
        public bool AllowLocationTelemetry = true;
        [SerializeField]
        public bool AllowDeviceTelemetry = true;

        [Space(10)]
        [Header("Tablet Using Collider Settings")]
        [Tooltip(
            "When using the 'LCK Tablet Using Collider' prefab. Trigger events will check this tag. "
                + "Make sure to add this tag on your XR Rig Direct Interactors for both controllers"
        )]
        [SerializeField]
        public string TriggerEnterTag = "Hand";

        [HideInInspector]
        public const string Version = "1.3.3";

        [HideInInspector]
        public const int Build = 1936;

        private static LckSettings _instance;

        public static LckSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<LckSettings>("LckSettings");
                    if (_instance != null)
                    {
#if !UNITY_EDITOR
                        Debug.Log($"LCK Settings loaded from Resources");
#endif
                        var idIsValid = System.Guid.TryParse(
                            _instance.TrackingId,
                            out System.Guid id
                        );
                        if (!idIsValid)
                        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                            Debug.LogWarning(
                                "LCK TrackingId has not been set. This is only valid in development builds. Please set it in the LCK settings"
                            );
#else
                            Debug.LogError(
                                "LCK TrackingId has not been set. This is only valid in development builds. Please set it in the LCK settings"
                            );
#endif
                        }
                    }
#if !UNITY_EDITOR
                    else
                    {
                        Debug.LogError(
                            "LCK not able to load settings. LckSettings.asset expected to exist in Resources"
                        );
                    }
#endif
                }

#if UNITY_EDITOR
                if (_instance == null)
                {
                    try
                    {
                        LckSettings scriptableObject =
                            ScriptableObject.CreateInstance<LckSettings>();

                        var parentFolder = System.IO.Path.GetDirectoryName(SettingsPath);
                        if (!System.IO.Directory.Exists(parentFolder))
                        {
                            System.IO.Directory.CreateDirectory(parentFolder);
                        }

                        UnityEditor.AssetDatabase.CreateAsset(scriptableObject, SettingsPath);
                        UnityEditor.AssetDatabase.SaveAssets();
                        _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<LckSettings>(
                            SettingsPath
                        );
                        if (_instance != null)
                        {
                            Debug.Log("LCK settings asset created at " + SettingsPath);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError("LCK failed to create settings asset: " + ex.Message);
                    }
                }
#else
                if (_instance == null)
                {
                    _instance = CreateInstance<LckSettings>();

                    Debug.LogError("LCK using default settings because LckSettings.asset not found");
                }
#endif

                return _instance;
            }
        }
    }
}
