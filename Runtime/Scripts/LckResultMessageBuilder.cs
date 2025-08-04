using System.Collections.Generic;

namespace Liv.Lck
{
    internal static class LckResultMessageBuilder
    {
        public static string BuildCameraIdNotFoundMessage(string missingCameraId, List<ILckCamera> existingCameras)
        {
            var listOfCameras = "";
            for (var index = 0; index < existingCameras.Count; index++)
            {
                var existingCamera = existingCameras[index];
                listOfCameras = index != existingCameras.Count - 1 ? listOfCameras + $"{existingCamera}, " : listOfCameras + $"{existingCamera}";
            }

            var message = $"Camera with ID {missingCameraId} not found. The known Camera IDs are: {listOfCameras}. " +
                          $"Have you miss-spelt, or forgotten to set the ID on your LckCamera component?";
            
            return message;
        }
        
        public static string BuildMonitorIdNotFoundMessage(string missingMonitorId, List<ILckMonitor> existingMonitors)
        {
            var listOfMonitors = "";
            for (var index = 0; index < existingMonitors.Count; index++)
            {
                var existingMonitor = existingMonitors[index];
                listOfMonitors = index != existingMonitors.Count - 1 ? listOfMonitors + $"{existingMonitor}, " : listOfMonitors + $"{existingMonitor}";
            }

            var message = $"Monitor with ID {missingMonitorId} not found. The known Monitor IDs are: {listOfMonitors}. " +
                          $"Have you miss-spelt, or forgotten to set the ID on your LckMonitor component?";
            
            return message;
        }
    }
}
