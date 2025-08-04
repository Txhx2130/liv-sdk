using Liv.Lck.UI;
using System.Collections.Generic;
using UnityEngine;

namespace Liv.Lck.Tablet
{
    public class LckOnScreenUIController : MonoBehaviour
    {
        private LckService _lckService;

        [SerializeField]
        private List<GameObject> _allOnscreenUI = new List<GameObject>();

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

            SetAllOnscreenButtonsState(true);
        }

        private void OnRecordingStarted(LckResult result)
        {
            SetAllOnscreenButtonsState(true);
        }

        public void OnNotificationStarted()
        {
            SetAllOnscreenButtonsState(false);
        }

        public void OnNotificationEnded()
        {
            SetAllOnscreenButtonsState(true);
            SetAllOnscreenButtonsToDefaultVisual(_allOnscreenUI);
        }

        private void SetAllOnscreenButtonsState(bool state)
        {
            SetObjectsState(_allOnscreenUI, state);
        }

        private void SetObjectsState(List<GameObject> objectList, bool state)
        {
            foreach (GameObject gameObj in objectList)
            {
                gameObj.SetActive(state);
            }
        }

        private void SetAllOnscreenButtonsToDefaultVisual(List<GameObject> objectList)
        {
            foreach (GameObject gameObj in objectList)
            {
                if (gameObj.TryGetComponent<LckScreenButton>(out LckScreenButton screenButton))
                {
                    screenButton.SetDefaultButtonColors();
                }
            }
        }    
    }
}
