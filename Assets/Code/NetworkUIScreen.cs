using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public abstract class NetworkUIScreen : NetworkBehaviour, IUIScreen
{
    public IReadOnlyList<string> RequiredSceneNames => requiredSceneNames;
    public bool BlocksPause => blocksPause;
    public bool IsOverlay => isOverlay;
    public bool ShowCursor => showCursor;
    public bool FreezeCamera => freezeCamera;

    [Header("Scene Settings")]
    [Tooltip("Deixe vazio ou adicione 'Any' se a tela puder ser acessada de qualquer cena.")]
    [SerializeField] private List<string> requiredSceneNames = new();

    [Header("UI")]
    [SerializeField] protected UIDocument uiDocument;

    [Header("Behavior")]
    [SerializeField] private bool blocksPause;
    [SerializeField] private bool isOverlay;
    [SerializeField] private bool showCursor;
    [SerializeField] private bool freezeCamera;

    [Header("Debug")]
    [SerializeField] protected bool showDebugLogs = true;

    protected VisualElement rootVisualElement;
    protected VisualElement visualizationRoot;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Adapt UI automatically based on network role
        if (IsHost)
            OnHostSpawn();
        else if (IsClient)
            OnClientSpawn();

        // Register with the local UISystem once replicated
        UISystem.Instance?.RegisterScreen(this);
    }

    public override void OnNetworkDespawn()
    {
        if (UISystem.Instance != null)
        {
            UISystem.Instance.UnregisterScreen(this);
            UISystem.Instance.Remove(this);
        }

        base.OnNetworkDespawn();
    }

    /// <summary>
    /// Override to show Host-specific controls (e.g., Kick player, Start Match).
    /// </summary>
    protected virtual void OnHostSpawn() { }

    /// <summary>
    /// Override to hide Host controls and show Client-specific UI.
    /// </summary>
    protected virtual void OnClientSpawn() { }

    public virtual void Return() => UISystem.Instance?.Back();

    public virtual void Show()
    {
        if (visualizationRoot != null)
        {
            visualizationRoot.style.display = DisplayStyle.Flex;
            SetScreenInteractable(true);
        }
        OnShow();
    }

    public virtual void Hide()
    {
        if (visualizationRoot != null)
        {
            SetScreenInteractable(false);
            visualizationRoot.style.display = DisplayStyle.None;
        }

        OnHide();
    }

    public bool IsAllowedInScene(string sceneName)
    {
        if (requiredSceneNames == null || requiredSceneNames.Count == 0) return true;
        if (requiredSceneNames.Contains("Any")) return true;
        return requiredSceneNames.Contains(sceneName);
    }

    public void SetScreenInteractable(bool interactable)
    {
        if (visualizationRoot == null) return;
        visualizationRoot.pickingMode = interactable ? PickingMode.Position : PickingMode.Ignore;

        foreach (VisualElement child in visualizationRoot.Children())
            SetElementInteractable(child, interactable);
    }

    protected void SetElementInteractable(VisualElement element, bool interactable)
    {
        element.pickingMode = interactable ? PickingMode.Position : PickingMode.Ignore;
        foreach (VisualElement child in element.Children())
            SetElementInteractable(child, interactable);
    }

    protected virtual void OnShow() { }
    protected virtual void OnHide() { }

    protected virtual void CentralizeCursor()
    {
        if (Mouse.current == null) return;
        Mouse.current.WarpCursorPosition(new Vector2(Screen.width / 2f, Screen.height / 2f));
    }

    protected virtual void Awake()
    {
        if (uiDocument == null)
        {
            Debug.LogError($"[{name}] NetworkUIScreen não possui um UIDocument atribuído.", this);
            return;
        }

        rootVisualElement = uiDocument.rootVisualElement;
    }

    protected virtual void Start()
    {

    }

    protected virtual void Update()
    {

    }
}