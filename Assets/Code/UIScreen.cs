using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public abstract class UIScreen : MonoBehaviour, IUIScreen
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
            Debug.LogError($"[{name}] UIScreen não possui um UIDocument atribuído.", this);
            return;
        }

        rootVisualElement = uiDocument.rootVisualElement;
    }

    protected virtual void Start()
    {
        UISystem.Instance?.RegisterScreen(this);
    }

    protected virtual void Update()
    {

    }

    protected virtual void OnDestroy()
    {
        if (UISystem.Instance == null) return;
        UISystem.Instance.UnregisterScreen(this);
        UISystem.Instance.Remove(this);
    }
}