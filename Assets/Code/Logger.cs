using UnityEngine;

public static class Logger
{
    private static readonly Color DefaultColor = new Color(0.4f, 0.8f, 1f);

    public static void Debug(bool isEnabled, string category, string message, Color color = default, UnityEngine.Object context = null)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!isEnabled) return;

        if (color == default)
        {
            color = DefaultColor;
        }

        string hexColor = ColorUtility.ToHtmlStringRGB(color);
        string formattedMessage = $"<color=#{hexColor}>[{category}] {message}</color>";

        if (context != null)
        {
            UnityEngine.Debug.Log(formattedMessage, context);
        }
        else
        {
            UnityEngine.Debug.Log(formattedMessage);
        }
#endif
    }

    public static void Warning(bool isEnabled, string category, string message, UnityEngine.Object context = null)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!isEnabled) return;

        string formattedMessage = $"[{category}] {message}";

        if (context != null)
        {
            UnityEngine.Debug.LogWarning(formattedMessage, context);
        }
        else
        {
            UnityEngine.Debug.LogWarning(formattedMessage);
        }
#endif
    }

    public static void Error(string category, string message, UnityEngine.Object context = null)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Errors usually bypass the toggle because critical configuration issues should always be seen
        string formattedMessage = $"[{category}] {message}";

        if (context != null)
        {
            UnityEngine.Debug.LogError(formattedMessage, context);
        }
        else
        {
            UnityEngine.Debug.LogError(formattedMessage);
        }
#endif
    }
}