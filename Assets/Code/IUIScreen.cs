using System.Collections.Generic;
using UnityEngine;

public interface IUIScreen
{
    string name { get; }
    IReadOnlyList<string> RequiredSceneNames { get; }
    bool BlocksPause { get; }
    bool IsOverlay { get; }
    bool ShowCursor { get; }
    bool FreezeCamera { get; }

    void Show();
    void Hide();
    void Return();
    bool IsAllowedInScene(string sceneName);
    void SetScreenInteractable(bool interactable);
}