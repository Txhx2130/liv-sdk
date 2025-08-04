using UnityEngine;

namespace Liv.Lck
{
    public class LckMonitor : MonoBehaviour, ILckMonitor
    {
        public delegate void LckMonitorRenderTextureSetDelegate(RenderTexture renderTexture);
        public event LckMonitorRenderTextureSetDelegate OnRenderTextureSet;
        
        [SerializeField]
        protected string _monitorId;

        public string MonitorId => _monitorId;

        protected virtual void OnEnable()
        {
            if (string.IsNullOrEmpty(_monitorId))
            {
                _monitorId = System.Guid.NewGuid().ToString();
            }

            LckMediator.RegisterMonitor(this);
        }
        
        public virtual void SetRenderTexture(RenderTexture renderTexture)
        {
            OnRenderTextureSet?.Invoke(renderTexture);
        }

        protected virtual void OnDestroy()
        {
            LckMediator.UnregisterMonitor(this);
        }
    }
}
