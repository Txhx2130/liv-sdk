using Liv.Lck.Tablet;
using Liv.Lck.Settings;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Liv.Lck.UI
{
    public class LckDoubleButtonTrigger : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public event Action<bool> OnDown;
        public event Action<bool> OnEnter;
        public event Action<bool, bool> OnUp;
        public event Action<bool> OnExit;

        [SerializeField]
        private bool _isUsingColliders = false;
        [SerializeField]
        private bool _isIncreaseButton;
        [SerializeField]
        private Image _background;
        [SerializeField]
        private Image _icon;

        private bool _hasCollided = false;

        public void SetBackgroundColor(Color color)
        {
            _background.color = color;
        }

        public void SetIconColor(Color color)
        {
            _icon.color = color;
        }

        #region Using ray and poke
        public void OnPointerDown(PointerEventData eventData)
        {
            if (_isUsingColliders == false)
            {
                OnDown?.Invoke(_isIncreaseButton);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_isUsingColliders == false)
            {
                OnEnter?.Invoke(_isIncreaseButton);
            }
        }
        public void OnPointerUp(PointerEventData eventData)
        {
            if (_isUsingColliders == false)
            {
                OnUp?.Invoke(_isIncreaseButton, false);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_isUsingColliders == false)
            {
                OnExit?.Invoke(_isIncreaseButton);
            }
        }
        #endregion

        #region Using colliders
        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.tag == LckSettings.Instance.TriggerEnterTag && IsValidTap(other.ClosestPoint(transform.position)) && LCKCameraController.ColliderButtonsInUse == false)
            {
                LCKCameraController.ColliderButtonsInUse = true;
                _hasCollided = true;

                OnDown?.Invoke(_isIncreaseButton);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.gameObject.tag == LckSettings.Instance.TriggerEnterTag && _hasCollided == true)
            {
                OnUp?.Invoke(_isIncreaseButton, true);

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
    }
}
