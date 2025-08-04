using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Liv.Lck.Settings
{
    public static class LckSettingsMenu
    {
        [MenuItem("LCK/Open Settings")]
        public static void OpenLckSettings()
        {
            SettingsService.OpenProjectSettings("Project/LCK");
        }
        
        [MenuItem("LCK/Open Documentation")]
        public static void OpenLckDocumentation()
        {
            Application.OpenURL(LckSettingsProvider.LCKDocumenationURL);
        }
    }
    
    public class LckSettingsProvider : SettingsProvider
    {
        public static string LCKDocumenationURL = "https://lck-docs.liv.tv/references";
        
        Editor _editor;
        private SerializedObject _serializedObject;

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new LckSettingsProvider("Project/LCK", SettingsScope.Project, new HashSet<string>(new[] { "LCK", "LIV", "Capture" }));
        }

        public LckSettingsProvider(string path, SettingsScope scopes, IEnumerable<string> keywords = null) : base(path, scopes, keywords) {
            var settings = LckSettings.Instance;
            _editor = UnityEditor.Editor.CreateEditor(settings);
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            base.OnActivate(searchContext, rootElement);
        }

        public override void OnGUI(string searchContext)
        {
            base.OnGUI(searchContext);
            UnityEditor.EditorGUILayout.LabelField($"LCK v{LckSettings.Version}#{LckSettings.Build}");
            
            if (GUILayout.Button("Open Documentation"))
            {
                Application.OpenURL(LCKDocumenationURL);
            }
            
#if UNITY_ANDROID

            int minSdkVersion = (int)PlayerSettings.Android.minSdkVersion;

            if (minSdkVersion < 32)
            {
                EditorGUILayout.HelpBox("Your current Minimum API Level for Android is below the recommended API Level for Quest and LCK development (32).", MessageType.Warning);

                if (GUILayout.Button("Set API Level to 32"))
                {
                    PlayerSettings.Android.minSdkVersion =  (AndroidSdkVersions)32; 
                    Debug.Log("Minimum API Level set to 30.");
                }
            }
#endif
            
            EditorGUILayout.Space();
            
            var settings = LckSettings.Instance;

            _editor.OnInspectorGUI();
        }
    }
}
