using UnityEngine;
using UnityEditor.Android;
using System.IO;
using Liv.Lck.Settings;

namespace Liv.Lck
{
  public class LckAndroidManifestPostProcessor : IPostGenerateGradleAndroidProject
  {
      public int callbackOrder => 0;

      public void OnPostGenerateGradleAndroidProject(string path)
      {
          if (LckSettings.Instance.AddPermissionsToAndroidManifest)
          {
              string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");

              if (File.Exists(manifestPath))
              {
                  var manifest = File.ReadAllText(manifestPath);

                  // Add the RECORD_AUDIO permission if it's not already present
                  if (!manifest.Contains("android.permission.RECORD_AUDIO"))
                  {
                      int insertPosition = manifest.IndexOf("<application");
                      if (insertPosition > 0)
                      {
                          manifest = manifest.Insert(insertPosition,
                                  "    <uses-permission android:name=\"android.permission.RECORD_AUDIO\" />\n");
                          File.WriteAllText(manifestPath, manifest);
                          Debug.Log("LCK Microphone permission added to AndroidManifest.xml");
                      }
                  }
                  else
                  {
                      Debug.Log("LCK Microphone permission already present in AndroidManifest.xml");
                  }
              }
              else
              {
                  Debug.LogError("LCK Could not add permissions to android manifest. AndroidManifest.xml not found at path: " + manifestPath);
              }
          }
          else
          {
                Debug.Log("LCK Not adding permissions to AndroidManifest.xml");
          }
      }
  }
}
 
