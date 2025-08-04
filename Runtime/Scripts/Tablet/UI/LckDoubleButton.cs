using System;
using TMPro;
using UnityEngine;

namespace Liv.Lck.UI
{
    public class LckDoubleButton : MonoBehaviour
    {
        public event Action<float> OnValueChanged;
        public float Value => _currentValue;

        [Header("Settings")]
        [SerializeField]
        private LckButtonColors _colors;
        [SerializeField]
        private int _maxValue;
        [SerializeField]
        private int _minValue;
        [SerializeField]
        private int _currentValue;
        [SerializeField]
        private int _increment;

        [Header("References")]
        [SerializeField]
        private LckDoubleButtonTrigger _increase;
        [SerializeField]
        private LckDoubleButtonTrigger _decrease;
        [SerializeField]
        private Transform _visuals;
        [SerializeField]
        private TMP_Text _valueText;

        [Header("Audio")]
        [SerializeField]
        private LckDiscreetAudioController _audioController;

        private void OnEnable()
        {
            SetMinMaxVisuals();

            _increase.OnEnter += OnEnter;
            _increase.OnDown += OnPressDown;
            _increase.OnUp += OnPressUp;
            _increase.OnExit += OnExit;

            _decrease.OnEnter += OnEnter;
            _decrease.OnDown += OnPressDown;
            _decrease.OnUp += OnPressUp;
            _decrease.OnExit += OnExit;
        }

        private void OnDisable()
        {
            _increase.OnEnter -= OnEnter;
            _increase.OnDown -= OnPressDown;
            _increase.OnUp -= OnPressUp;
            _increase.OnExit -= OnExit;

            _decrease.OnEnter -= OnEnter;
            _decrease.OnDown -= OnPressDown;
            _decrease.OnUp -= OnPressUp;
            _decrease.OnExit -= OnExit;
        }


        public void OnEnter(bool isIncrease)
        {
            if (CheckIfValueIsMinOrMax(isIncrease) == true) return;

            if (isIncrease)
            {
                _increase.SetBackgroundColor(_colors.HighlightedColor);
            }
            else
            {
                _decrease.SetBackgroundColor(_colors.HighlightedColor);
            }

            _audioController?.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.HoverSound);
        }

        public void OnPressDown(bool isIncrease)
        {
            if (CheckIfValueIsMinOrMax(isIncrease) == true) return;

            if (isIncrease)
            {
                _increase.SetBackgroundColor(_colors.PressedColor);

                if (_currentValue != _maxValue)
                {
                    _currentValue += _increment;
                    if (_currentValue == _maxValue)
                    {
                        UpdateValueText("MAX");
                        _increase.SetIconColor(_colors.DisabledColor);
                    }
                    else
                    {
                        UpdateValueText(_currentValue.ToString());
                    }
                }

                _visuals.localRotation = Quaternion.Euler(0, -8f, 0);
            }
            else
            {
                _decrease.SetBackgroundColor(_colors.PressedColor);

                if (_currentValue != _minValue)
                {
                    _currentValue -= _increment;
                    if (_currentValue == _minValue)
                    {
                        _decrease.SetIconColor(_colors.DisabledColor);
                    }

                    UpdateValueText(_currentValue.ToString());
                }

                _visuals.localRotation = Quaternion.Euler(0, 8f, 0);
            }       

            _audioController?.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.ClickDown);

            OnValueChanged?.Invoke(_currentValue);
        }

        public void OnPressUp(bool isIncrease, bool usingCollider = false)
        {
            if (CheckIfValueIsMinOrMax(isIncrease) == true)
            {
                _visuals.localRotation = Quaternion.Euler(0, 0, 0);
                SetMinMaxVisuals();
                return;
            }

            if (_currentValue != _maxValue)
            {
                _increase.SetIconColor(Color.white);
            }
            if (_currentValue != _minValue)
            {
                _decrease.SetIconColor(Color.white);
            }

            if (isIncrease)
            {
                if (usingCollider)
                {
                    _increase.SetBackgroundColor(_colors.NormalColor);
                }
                else
                {
                    _increase.SetBackgroundColor(_colors.HighlightedColor);
                }                      
            }
            else
            {
                if (usingCollider)
                {
                    _decrease.SetBackgroundColor(_colors.NormalColor);
                }
                else
                {
                    _decrease.SetBackgroundColor(_colors.HighlightedColor);
                }
            }

            _audioController?.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.ClickUp);

            _visuals.localRotation = Quaternion.Euler(0, 0, 0);
        }

        public void OnExit(bool isIncrease)
        {
            if (CheckIfValueIsMinOrMax(isIncrease) == true) return;

            if (isIncrease)
            {
                _increase.SetBackgroundColor(_colors.NormalColor);
            }
            else
            {
                _decrease.SetBackgroundColor(_colors.NormalColor);
            }         
        }

        private void UpdateValueText(string text)
        {
            _valueText.text = text;
        }

        private bool CheckIfValueIsMinOrMax(bool isIncrease)
        {
            if (isIncrease && _currentValue == _maxValue)
            {
                return true;
            }

            if (isIncrease == false && _currentValue == _minValue)
            {
                return true;
            }

            return false;
        }

        private void SetMinMaxVisuals()
        {
            if (_currentValue == _maxValue)
            {
                _increase.SetIconColor(_colors.DisabledColor);
                _increase.SetBackgroundColor(_colors.NormalColor);
            }

            if (_currentValue == _minValue)
            {
                _decrease.SetIconColor(_colors.DisabledColor);
                _decrease.SetBackgroundColor(_colors.NormalColor);
            }
        }
    }
}
