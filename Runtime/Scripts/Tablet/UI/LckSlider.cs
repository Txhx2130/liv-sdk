using System;
using UnityEngine;
using UnityEngine.UI;

namespace Liv.Lck.UI
{
    public class LckSlider : MonoBehaviour
    {
        [SerializeField]
        private string _name;

        [SerializeField]
        private Slider _slider;

        [SerializeField]
        private float _defaultValue;

        [SerializeField]
        private float _minValue = 0;

        [SerializeField]
        private float _maxValue = 1;

        [SerializeField]
        private bool _isInt = false;

        [SerializeField]
        private int _precision = 2;

        [SerializeField]
        private float _valueMultiplier = 1;

        [SerializeField]
        private TMPro.TextMeshProUGUI _valueText;

        [SerializeField]
        private TMPro.TextMeshProUGUI _typeText;

        public event Action<float> OnValueChanged;
        public float Value => GetValue();

        void Start()
        {
            _slider.value = _defaultValue;
            _slider.minValue = _minValue;
            _slider.maxValue = _maxValue;
            _slider.wholeNumbers = _isInt;
            _slider.onValueChanged.AddListener(ChangeValue);
            _valueText.text = _slider.value.ToString();
            _typeText.text = _name;
        }

        private void OnValidate()
        {
            if (_typeText)
                _typeText.text = _name;

            if (_slider)
            {
                _slider.minValue = _minValue;
                _slider.maxValue = _maxValue;
                _slider.wholeNumbers = _isInt;
                _slider.value = _defaultValue;
            }

            UpdateValueText();
        }

        private void UpdateValueText()
        {
            if (_slider && _valueText)
            {
                if (_isInt)
                    _valueText.text = ((int)GetValue()).ToString();
                else
                    _valueText.text = GetValue().ToString($"N{_precision}");
            }
        }

        private float GetValue()
        {
            return _slider.value * _valueMultiplier;
        }

        public void ChangeValue(float value)
        {
            UpdateValueText();
            OnValueChanged?.Invoke(value);
        }
    }
}
