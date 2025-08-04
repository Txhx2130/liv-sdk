using System;
using UnityEditor;
using UnityEngine;

namespace Liv.Lck
{
    public class LckDebugWindow : EditorWindow
    {
        [MenuItem("LCK/Debug/Debug Window")]
        private static void Init()
        {
            LckDebugWindow window = (LckDebugWindow)EditorWindow.GetWindow(typeof(LckDebugWindow));
            window.Show();
        }

        private void OnGUI()
        {
            GUI.enabled = Application.isPlaying;

            if (!GUI.enabled)
                GUILayout.Label("This tool is only available in play mode.");

            GUILayout.Space(10);
            GUILayout.Label("LCK Service", EditorStyles.boldLabel);

            if (GUI.enabled)
            {
                DrawServiceInfo();
            }
            else
            {
                GUILayout.Label("Waiting to enter playmode.");
            }
        }

        private static void DrawServiceInfo()
        {
            var getService = LckService.GetService();
            if (getService.Success)
            {
                DrawAvailableService(getService);
            }
            else
            {
                DrawUnavailableService(getService);
            }
        }

        private static void DrawUnavailableService(LckResult<LckService> getService)
        {
            GUILayout.Label("Service is not available.");

            GUILayout.Label("Error: " + getService.Error);
            GUILayout.Label("Message: " + getService.Message);
        }

        private static void DrawAvailableService(LckResult<LckService> getService)
        {
            var service = getService.Result;

            GUILayout.Label("Service is available");

            GUILayout.Space(10);

            DrawTrackInfo(service);

            GUILayout.Space(10);

            DrawIsCapturing(service);
            DrawIsRecording(service);
        }

        private static void DrawTrackInfo(LckService service)
        {
            var getDescriptor = service.GetDescriptor();
            GUILayout.Label("Track Info", EditorStyles.boldLabel);
            if(getDescriptor.Success)
            {
                var descriptor = getDescriptor.Result;
                GUILayout.Label($"Resolution: {descriptor.cameraTrackDescriptor.CameraResolutionDescriptor.Width}x{descriptor.cameraTrackDescriptor.CameraResolutionDescriptor.Height}");
                GUILayout.Label($"Framerate: {descriptor.cameraTrackDescriptor.Framerate}");
                GUILayout.Label($"Bitrate: {descriptor.cameraTrackDescriptor.Bitrate} ({descriptor.cameraTrackDescriptor.Bitrate / 1048576}mbit)");
            }
            else
            {
                GUILayout.Label("Error: " + getDescriptor.Error);
                GUILayout.Label("Message: " + getDescriptor.Message);
            }
        }

        private static void DrawIsCapturing(LckService service)
        {
            GUILayout.Label("Is Capturing", EditorStyles.boldLabel);
            var getIsCapturing = service.IsCapturing();
            if(getIsCapturing.Success)
            {
                GUILayout.Label(getIsCapturing.Result.ToString());
            }
            else
            {
                GUILayout.Label("Error: " + getIsCapturing.Error);
                GUILayout.Label("Message: " + getIsCapturing.Message);
            }
        }

        private static void DrawIsRecording(LckService service)
        {
            GUILayout.Label("Is Recording", EditorStyles.boldLabel);
            var getIsRecording = service.IsRecording();
            if(getIsRecording.Success)
            {
                GUILayout.Label(getIsRecording.Result.ToString());
            }
            else
            {
                GUILayout.Label("Error: " + getIsRecording.Error);
                GUILayout.Label("Message: " + getIsRecording.Message);
            }
        }
    }
}
