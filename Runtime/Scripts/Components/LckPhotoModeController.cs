using Liv.Lck.Recorder;
using Liv.Lck.Tablet;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Liv.Lck.UI
{
    public class LckPhotoModeController : MonoBehaviour
    {
        [SerializeField]
        private Image _photoFlash;
        [SerializeField]
        private GameObject _countdownBG;
        [SerializeField]
        private TMP_Text _countdownText;
        [SerializeField]
        private float _fadeOutDuration = 0.5f;
        [SerializeField]
        private float _delayBeforeFade = 0.3f;
        [SerializeField]
        private LckDiscreetAudioController _audioController;
        [SerializeField]
        private LckNotificationController _notificationController;

        [SerializeField]
        private UnityEvent _onPhotoCaptured;

        private float _flashAlpha = 0.9f;

        private LckService _lckService = null;

        private void Start()
        {
            _photoFlash.gameObject.SetActive(false);
            _countdownBG.SetActive(false);
        }

        private void OnEnable()
        {
            var getService = LckService.GetService();

            if (!getService.Success)
            {
                Debug.LogWarning("Could not get LCK Service" + getService.Error);
                return;
            }

            _lckService = LckService.GetService().Result;

            _lckService.OnRecordingStarted += OnRecordingStarted;
        }

        private void OnDisable()
        {
            _lckService.OnRecordingStarted -= OnRecordingStarted;

            StopAndResetSequence();
        }

        private void OnRecordingStarted(LckResult result)
        {
            StopAndResetSequence();
        }

        public void PlayPhotoSequence()
        {
            StopAndResetSequence();
            StartCoroutine(CountdownSequence());
        }

        private void StopAndResetSequence()
        {
            StopAllCoroutines();
            ResetFlashVisuals();
            ResetCountdownVisuals();
        }

        private void ResetFlashVisuals()
        {
            _photoFlash.gameObject.SetActive(false);

            Color currentColor = _photoFlash.color;
            currentColor.a = _flashAlpha;
            _photoFlash.color = currentColor;
        }

        IEnumerator FadeSequence()
        {
            // screenshot buttons get disabled for 0.25s with this event invoke
            _onPhotoCaptured.Invoke();

            _lckService.CapturePhoto();

            _photoFlash.gameObject.SetActive(true);

            _audioController.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.CameraShutterSound);

            yield return new WaitForSeconds(_delayBeforeFade);

            // Fade Out
            yield return StartCoroutine(FadeImageAlpha(_flashAlpha, 0f, _fadeOutDuration));

            yield return new WaitForSeconds(_fadeOutDuration);

            _photoFlash.gameObject.SetActive(false);



            yield return new WaitForSeconds(0.5f);
            _notificationController.ShowPhotoNotification();
        }

        private void ResetCountdownVisuals()
        {
            _countdownBG.SetActive(false);
        }

        IEnumerator CountdownSequence()
        {
            _countdownText.text = "3";
            _countdownBG.SetActive(true);
            _audioController.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.ScreenshotBeepSound);

            yield return new WaitForSeconds(1);
            _countdownText.text = "2";
            _audioController.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.ScreenshotBeepSound);

            yield return new WaitForSeconds(1);
            _countdownText.text = "1";
            _audioController.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.ScreenshotBeepSound);

            yield return new WaitForSeconds(1);
            _countdownBG.SetActive(false);
            StartCoroutine(FadeSequence());
        }

        IEnumerator FadeImageAlpha(float startAlpha, float endAlpha, float duration)
        {
            float elapsedTime = 0;
            Color currentColor = _photoFlash.color;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / duration);
                currentColor.a = Mathf.Lerp(startAlpha, endAlpha, t);
                _photoFlash.color = currentColor;
                yield return null;
            }

            currentColor.a = endAlpha;
            _photoFlash.color = currentColor;
        }
    }
}
