using Liv.Lck.Settings;
using Liv.Lck.Tablet;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Liv.Lck.UI
{
    public class LckScreenButton : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Header("References")]
        [SerializeField]
        private LckButtonColors _colors;
        [SerializeField]
        private Button _button;
        [SerializeField]
        private Image _icon;

        [Header("Audio")]
        [SerializeField]
        private LckDiscreetAudioController _audioController;

        private bool _isDisabled = false;
        private bool _hasCollided = false;
        private GameObject _clickedObject;

        #region Using ray and poke
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_isDisabled)
                return;

            SetIconColor(_colors.HighlightedColor);
            _audioController.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.HoverSound);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_isDisabled)
                return;

            SetIconColor(_colors.PressedColor);
            _audioController.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.ClickDown);
            _clickedObject = eventData.pointerEnter;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_isDisabled)
                return;

            _audioController.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.ClickUp);

            SetIconColor(_colors.HighlightedColor);

            // If your pointer is away from the original object on the way up, make sure to reset colors
            if (_clickedObject != eventData.pointerEnter)
            {
                SetDefaultButtonColors();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_isDisabled)
                return;

            SetIconColor(_colors.NormalColor);
        }
        #endregion

        #region Using colliders
        private void OnTriggerEnter(Collider other)
        {
            if (_isDisabled)
                return;

            if (other.gameObject.tag == LckSettings.Instance.TriggerEnterTag && IsValidTap(other.ClosestPoint(transform.position)) && LCKCameraController.ColliderButtonsInUse == false)
            {
                LCKCameraController.ColliderButtonsInUse = true;
                _hasCollided = true;

                SetIconColor(_colors.PressedColor);
                _audioController.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.ClickDown);
                _button.onClick.Invoke();
            }
        }
        private void OnTriggerExit(Collider other)
        {
            if (_isDisabled)
                return;

            if (other.gameObject.tag == LckSettings.Instance.TriggerEnterTag && _hasCollided == true)
            {
                SetIconColor(_colors.NormalColor);
                _audioController.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.ClickUp);
                _hasCollided = false;
                LCKCameraController.ColliderButtonsInUse = false;
            }
        }

        private bool IsValidTap(Vector3 tapPosition)
        {
            Vector3 direction = tapPosition - transform.position;
            float angle = Vector3.Angle(-transform.forward, direction);
            return angle < 90;
        }
        #endregion

        //called from LckScreenshotController screenshot event
        public void DisableForDuration(float duration)
        {
            _isDisabled = true;
            SetIconColor(_colors.NormalColor);
            _icon.gameObject.SetActive(false);
            Invoke(nameof(ReEnableButton), duration);
        }

        private void ReEnableButton()
        {
            _icon.gameObject.SetActive(true);
            _isDisabled = false;
        }

        private void SetIconColor(Color color)
        {
            if (_icon != null)
            {
                _icon.color = color;
            }
        }

        public void SetDefaultButtonColors()
        {
            SetIconColor(_colors.NormalColor);
        }
    }
}
