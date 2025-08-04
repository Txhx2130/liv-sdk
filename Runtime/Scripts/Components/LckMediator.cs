using System;
using System.Collections.Generic;

namespace Liv.Lck
{
    internal static class LckMediator
    {
        private static readonly Dictionary<string, ILckCamera> _cameras = new Dictionary<string, ILckCamera>();
        private static readonly Dictionary<string, ILckMonitor> _monitors = new Dictionary<string, ILckMonitor>();

        public static event Action<ILckCamera> CameraRegistered;
        public static event Action<ILckCamera> CameraUnregistered;
        public static event Action<ILckMonitor> MonitorRegistered;
        public static event Action<ILckMonitor> MonitorUnregistered;
        public static event Action<string, string> MonitorToCameraAssignment;
        public static void RegisterCamera(ILckCamera camera)
        {
            if (!_cameras.ContainsKey(camera.CameraId))
            {
                _cameras.Add(camera.CameraId, camera);
                CameraRegistered?.Invoke(camera);
            }
        }

        public static void UnregisterCamera(ILckCamera camera)
        {
            if (_cameras.ContainsKey(camera.CameraId))
            {
                _cameras.Remove(camera.CameraId);
                CameraUnregistered?.Invoke(camera);
            }
        }

        public static void RegisterMonitor(ILckMonitor monitor)
        {
            if (!_monitors.ContainsKey(monitor.MonitorId))
            {
                _monitors.Add(monitor.MonitorId, monitor);
                MonitorRegistered?.Invoke(monitor);
            }
        }

        public static void UnregisterMonitor(ILckMonitor monitor)
        {
            if (_monitors.ContainsKey(monitor.MonitorId))
            {
                _monitors.Remove(monitor.MonitorId);
                MonitorUnregistered?.Invoke(monitor);
            }
        }

        public static ILckCamera GetCameraById(string id)
        {
            _cameras.TryGetValue(id, out ILckCamera camera);
            return camera;
        }

        public static ILckMonitor GetMonitorById(string id)
        {
            _monitors.TryGetValue(id, out ILckMonitor monitor);
            return monitor;
        }

        public static IEnumerable<ILckCamera> GetCameras()
        {
            return _cameras.Values;
        }

        public static IEnumerable<ILckMonitor> GetMonitors()
        {
            return _monitors.Values;
        }
        
        public static void NotifyMixerAboutMonitorForCamera(string monitorId, string cameraId)
        {
            MonitorToCameraAssignment?.Invoke(monitorId, cameraId);
        }
    }
}
