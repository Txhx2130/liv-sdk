using System;
using Liv.Lck.Tablet;
using Liv.Lck.Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Liv.Lck.UI
{
    public class LckButton : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Header("Settings")]
        [SerializeField]
        private string _name;

        [SerializeField]
        private LckButtonColors _colors;

        [Header("References")]
        [SerializeField]
        private TMPro.TextMeshProUGUI _labelText;
        [SerializeField] 
        private Image _iconImage;

        [SerializeField]
        private Renderer _renderer;

        [SerializeField]
        private RectTransform _visuals;

        [SerializeField]
        private Button _button;

        [Header("Audio")]
        [SerializeField]
        private LckDiscreetAudioController _audioController;

        private GameObject _clickedObject;
        private bool _hasCollided = false;
        private MaterialPropertyBlock _propertyBlock;
        private int _colorId;
        private bool _isDisabled;

        private void Start()
        {
            _propertyBlock = new MaterialPropertyBlock();
            _colorId = Shader.PropertyToID("_Color");
        }

        public void SetLabelText(string text)
        {
            _labelText.text = text;
        }
        
        public void SetIsDisabled(bool isDisabled)
        {
            _isDisabled = isDisabled;
            _iconImage.color = isDisabled ? _colors.HighlightedColor : Color.white;
            _labelText.color = isDisabled ? _colors.HighlightedColor : Color.white;
            _button.interactable = !isDisabled;
            SetMeshColor(isDisabled ? _colors.DisabledColor : _colors.NormalColor);
        }

        #region Using ray and poke
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_isDisabled) 
                return;
            
            SetMeshColor(_colors.HighlightedColor);
            _audioController.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.HoverSound);
        }
        
        public void OnPointerDown(PointerEventData eventData)
        {
            if (_isDisabled) 
                return;
            
            _visuals.anchoredPosition3D = new Vector3(0, 0, 40f);
            SetMeshColor(_colors.PressedColor);
            _audioController.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.ClickDown);
            _clickedObject = eventData.pointerEnter;
        }
        
        public void OnPointerUp(PointerEventData eventData)
        {
            if (_isDisabled) 
                return;
            
            _visuals.anchoredPosition3D = new Vector3(0, 0, 0f);
            _audioController.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.ClickUp);

            // If you release away from the button on the way up, still send an OnClick event
            if (_clickedObject != eventData.pointerEnter)
            {
                _button.OnPointerClick(eventData);
                return;
            }
            SetMeshColor(_colors.HighlightedColor);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_isDisabled) 
                return;
            
            SetMeshColor(_colors.NormalColor);
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
                _visuals.anchoredPosition3D = new Vector3(0, 0, 40f);
                SetMeshColor(_colors.PressedColor);
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
                _visuals.anchoredPosition3D = new Vector3(0, 0, 0f);
                SetMeshColor(_colors.NormalColor);
                _audioController.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.ClickUp);
                _hasCollided = false;
                LCKCameraController.ColliderButtonsInUse = false;
            }
        }

        private bool IsValidTap(Vector3 tapPosition)
        {
            Vector3 direction = tapPosition - transform.position;
            float angle = Vector3.Angle(-transform.forward, direction);
            return angle < 65;
        }
        #endregion

        private void OnValidate()
        {
            if (_labelText)
                _labelText.text = _name;

            if (_colors)
            {
                if (_button)
                {
                    var colors = _button.colors;
                    colors.normalColor = _colors.NormalColor;
                    colors.highlightedColor = _colors.HighlightedColor;
                    colors.pressedColor = _colors.PressedColor;
                    colors.selectedColor = _colors.SelectedColor;
                    colors.disabledColor = _colors.DisabledColor;

                    if (_button.colors != colors)
                        _button.colors = colors;
                }
            }

            if (!_renderer) return;
            
            _propertyBlock = new MaterialPropertyBlock();
            SetMeshColor(_colors.NormalColor);
        }
        
        private void SetMeshColor(Color color)
        {
            _propertyBlock.SetColor(_colorId, color);
            _renderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
