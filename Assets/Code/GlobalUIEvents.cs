using System;

public static class GlobalUIEvents
{
    // Fade Menu
    public static Action<float> OnFadeIn;
    public static Action<float> OnFadeOut;

    // Interaction Menu
    public static Action<Action> OnInteractionMenuOpened;
    public static Action<int> OnInteractionMenuNavigation;
}