using System.Collections.Generic;
using UnityEngine;

namespace Liv.Lck
{
    public class LckDiscreetAudioController : MonoBehaviour
    {
        private LckService _lckService;

        private Dictionary<AudioClip, UnityEngine.AudioClip> _allAudioClips = new();
        [Header("Audio Volume")]
        [SerializeField] private float _volume = 0.2f;

        [Header("Audio Clips")]
        [SerializeField] private UnityEngine.AudioClip _recordingStart;
        [SerializeField] private UnityEngine.AudioClip _recordingSaved;
        [SerializeField] private UnityEngine.AudioClip _clickDown;
        [SerializeField] private UnityEngine.AudioClip _clickUp;
        [SerializeField] private UnityEngine.AudioClip _hoverSound;
        [SerializeField] private UnityEngine.AudioClip _cameraShutterSound;
        [SerializeField] private UnityEngine.AudioClip _screenshotBeepSound;

        public enum AudioClip 
        { 
            RecordingStart = 0,
            RecordingSaved = 1,
            ClickDown = 2,
            ClickUp = 3,
            HoverSound = 4,
            CameraShutterSound = 5,
            ScreenshotBeepSound = 6,
        }

        private void Awake()
        {
            InitializeAudioClipDictionary();
        }

        private void InitializeAudioClipDictionary()
        {
            _allAudioClips = new() {
                { AudioClip.RecordingStart, _recordingStart },
                { AudioClip.RecordingSaved, _recordingSaved },
                { AudioClip.ClickDown, _clickDown },
                { AudioClip.ClickUp, _clickUp },
                { AudioClip.HoverSound, _hoverSound },
                { AudioClip.CameraShutterSound, _cameraShutterSound },
                { AudioClip.ScreenshotBeepSound, _screenshotBeepSound },
            };
        }

        private void Start()
        {
            var lckService = LckService.GetService();

            if (!lckService.Success)
            {
                LckLog.LogWarning($"LCK Could not get Service {lckService.Error}");
                return;
            }

            _lckService = lckService.Result;

            foreach (KeyValuePair<AudioClip, UnityEngine.AudioClip> pair in _allAudioClips)
            {
                _lckService.PreloadDiscreetAudio(pair.Value, _volume);
            }
        }

        public void PlayDiscreetAudioClip(AudioClip clip)
        {
            _lckService.PlayDiscreetAudioClip(_allAudioClips[clip]);
        }
    }
}
