using Liv.Lck.Settings;

namespace Liv.Lck
{
  public enum LogLevel : int
  {
    None,
    Error,
    Warning,
    Info,
  }

  internal static class LckLog
  {
    public static void Log(string message)
    {
      if(ShouldPrint(LogLevel.Info))
      {
        UnityEngine.Debug.Log(message);
      }
    }

    public static void LogWarning(string message)
    {
      if(ShouldPrint(LogLevel.Warning))
      {
        UnityEngine.Debug.LogWarning(message);
      }
    }

    public static void LogError(string message)
    {
      if(ShouldPrint(LogLevel.Error))
      {
        UnityEngine.Debug.LogError(message);
      }
    }

    [System.Diagnostics.Conditional("LCK_TRACE")]
    public static void LogTrace(string message)
    {
      UnityEngine.Debug.Log(message);
    }

    private static bool ShouldPrint(LogLevel level)
    {
      return (int)LckSettings.Instance.BaseLogLevel >= (int)level;
    }
  }
}
