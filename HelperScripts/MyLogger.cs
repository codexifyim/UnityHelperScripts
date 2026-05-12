
using UnityEngine;
public static class MyLogger
{
    public static void Log(string message, object caller = null)
    {
#if UNITY_EDITOR
        string className = caller != null ? caller.GetType().Name : "Unknown";
        Debug.Log($"[{className}] == {message}");
#endif
    }
    public static void LogError(string message, object caller = null)
    {
#if UNITY_EDITOR
        string className = caller != null ? caller.GetType().Name : "Unknown";
        Debug.LogError($"[{className}] == {message}");
#endif

    }
    public static void LogWarning(string message, object caller = null)
    {
#if UNITY_EDITOR
        string className = caller != null ? caller.GetType().Name : "Unknown";
        Debug.LogWarning($"[{className}] == {message}");
#endif

    }

}
