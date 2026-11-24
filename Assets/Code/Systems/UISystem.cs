using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UISystem : MonoBehaviour
{
    public static UISystem Instance { get; private set; }

    private readonly Dictionary<Type, IUIScreen> registeredScreens = new();
    private readonly Stack<IUIScreen> screenStack = new();

    [Header("Screens")]
    [Tooltip("Aceita tanto UIScreen (MonoBehaviour) quanto NetworkUIScreen (NetworkBehaviour).")]
    [SerializeField] private List<MonoBehaviour> screensToRegister;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private bool screenCameraInputLocked;
    private bool presentationCameraInputLocked;
    private bool manualCameraInputLocked;

    private bool cameraInputStateCaptured;
    private bool originalCameraInputEnabled;

    public bool IsScreenAllowedInScene(IUIScreen screen, string sceneName)
    {
        if (screen == null)
            return false;

        return screen.IsAllowedInScene(sceneName);
    }

    public void RegisterScreen(IUIScreen screen)
    {
        if (screen == null)
            return;

        Type screenType = screen.GetType();

        if (registeredScreens.TryGetValue(
            screenType,
            out IUIScreen existing))
        {
            if (existing == screen)
                return;

            Logger.Warning(
                isEnabled: showDebugLogs,
                category: nameof(UISystem),
                message: $"Já existe uma tela registrada do tipo {screenType.Name}. " +
                         $"Substituindo {existing.name} por {screen.name}.",
                context: this
            );
        }

        registeredScreens[screenType] = screen;
    }

    public void UnregisterScreen(IUIScreen screen)
    {
        if (screen == null)
            return;

        Type screenType = screen.GetType();

        if (!registeredScreens.TryGetValue(
            screenType,
            out IUIScreen registeredScreen))
        {
            return;
        }

        if (registeredScreen != screen)
            return;

        registeredScreens.Remove(screenType);
    }

    public T GetScreen<T>() where T : MonoBehaviour, IUIScreen
    {
        Type screenType = typeof(T);

        if (registeredScreens.TryGetValue(
            screenType,
            out IUIScreen registeredScreen))
        {
            return registeredScreen as T;
        }

        T foundScreen = FindFirstObjectByType<T>(
            FindObjectsInactive.Include
        );

        if (foundScreen != null)
        {
            RegisterScreen(foundScreen);
            return foundScreen;
        }

        Logger.Warning(
            isEnabled: showDebugLogs,
            category: nameof(UISystem),
            message: $"A tela do tipo {screenType.Name} não foi encontrada.",
            context: this
        );

        return null;
    }

    public void Open<T>()
        where T : MonoBehaviour, IUIScreen
    {
        T screen = GetScreen<T>();

        if (screen != null)
        {
            Open(screen);
        }
    }

    public void Open(IUIScreen screen)
    {
        if (screen == null)
            return;

        string currentScene = SceneManager.GetActiveScene().name;

        if (!IsScreenAllowedInScene(
            screen,
            currentScene))
        {
            Logger.Warning(
                isEnabled: showDebugLogs,
                category: nameof(UISystem),
                message: $"Não foi possível abrir a tela '{screen.name}'. " +
                         $"A tela não está disponível na cena atual '{currentScene}'.",
                context: this
            );

            return;
        }

        if (screenStack.Count > 0 &&
            screenStack.Peek() == screen)
        {
            return;
        }

        if (screenStack.Count > 0)
        {
            IUIScreen previous = screenStack.Peek();

            if (previous != null)
            {
                if (screen.IsOverlay)
                {
                    previous.SetScreenInteractable(false);
                }
                else
                {
                    previous.Hide();
                }
            }
        }

        screenStack.Push(screen);

        CheckStackAndSwitchInput();

        screen.Show();

        RefreshUIState();
    }

    public void Back()
    {
        if (screenStack.Count == 0)
        {
            CheckStackAndSwitchInput();
            RefreshUIState();

            return;
        }

        IUIScreen current = screenStack.Peek();

        if (current == null)
        {
            screenStack.Pop();

            CheckStackAndSwitchInput();
            RefreshUIState();

            return;
        }

        if (IsRootScreen(current))
            return;

        screenStack.Pop();

        current.Hide();

        if (screenStack.Count > 0)
        {
            IUIScreen previous = screenStack.Peek();

            if (previous != null)
            {
                if (current.IsOverlay)
                {
                    previous.SetScreenInteractable(true);
                }
                else
                {
                    previous.Show();
                }
            }
        }

        CheckStackAndSwitchInput();
        RefreshUIState();

        PrintStack();
    }

    public void Remove(IUIScreen screen)
    {
        if (screen == null ||
            screenStack.Count == 0)
        {
            return;
        }

        if (IsRootScreen(screen))
            return;

        Stack<IUIScreen> temporaryStack = new();

        while (screenStack.Count > 0)
        {
            IUIScreen current = screenStack.Pop();

            if (current != screen)
            {
                temporaryStack.Push(current);
            }
        }

        while (temporaryStack.Count > 0)
        {
            screenStack.Push(temporaryStack.Pop());
        }

        CheckStackAndSwitchInput();
        RefreshUIState();
    }

    public void ClearStack()
    {
        while (screenStack.Count > 0)
        {
            IUIScreen current = screenStack.Peek();

            if (IsRootScreen(current))
                break;

            screenStack.Pop();

            if (current != null)
            {
                current.Hide();
            }
        }

        if (screenStack.Count > 0)
        {
            IUIScreen root = screenStack.Peek();

            if (IsRootScreen(root))
            {
                root.Show();
            }
        }

        CheckStackAndSwitchInput();
        RefreshUIState();

        PrintStack();
    }

    public IUIScreen CurrentScreen()
    {
        return screenStack.Count > 0
            ? screenStack.Peek()
            : null;
    }

    // Manual camera input lock.
    // This no longer disables a CinemachineCamera.
    public void FreezeMainCamera()
    {
        manualCameraInputLocked = true;
    }

    public void UnfreezeCamera()
    {
        manualCameraInputLocked = false;
    }

    // Used by InteractionPresentationController.
    // This lock remains independent of the UI screen's camera lock.
    public void SetPresentationCameraInputLocked(bool locked)
    {
        presentationCameraInputLocked = locked;
    }

    private void HandleConfirmPressed()
    {
        IUIScreen currentScreen = CurrentScreen();

        if (currentScreen == null ||
            currentScreen is GameplayMenuScreen)
        {
            return;
        }
    }

    private void OnSceneChanged(Scene oldScene, Scene newScene)
    {
        ValidateActiveStackForScene(newScene.name);
        RegisterScreensForScene(newScene.name);
        InitializeRootScreen(newScene.name);
        RefreshUIState();
    }

    private void RegisterScreensForScene(string sceneName)
    {
        if (screensToRegister == null)
        {
            Logger.Error(
                category: nameof(UISystem),
                message: "screensToRegister não foi configurado.",
                context: this
            );

            return;
        }

        for (int i = 0; i < screensToRegister.Count; i++)
        {
            MonoBehaviour candidate = screensToRegister[i];

            if (candidate == null)
            {
                Logger.Warning(
                    isEnabled: showDebugLogs,
                    category: nameof(UISystem),
                    message: $"screensToRegister[{i}] está vazio.",
                    context: this
                );

                continue;
            }

            if (candidate is not IUIScreen screen)
            {
                screen = candidate.GetComponent<GameplayMenuScreen>();
            }

            if (screen == null)
            {
                Logger.Warning(
                    isEnabled: showDebugLogs,
                    category: nameof(UISystem),
                    message: $"'{candidate.name}' em screensToRegister não implementa IUIScreen.",
                    context: this
                );

                continue;
            }

            bool allowed = IsScreenAllowedInScene(
                screen,
                sceneName
            );

            if (allowed)
            {
                RegisterScreen(screen);
            }
        }
    }

    private void ValidateActiveStackForScene(string sceneName)
    {
        if (screenStack.Count == 0)
            return;

        while (screenStack.Count > 0)
        {
            IUIScreen current = screenStack.Peek();

            if (current != null &&
                !IsScreenAllowedInScene(
                    current,
                    sceneName))
            {
                screenStack.Pop();
                current.Hide();
            }
            else
            {
                break;
            }
        }

        RefreshUIState();
    }

    private void InitializeRootScreen(string sceneName)
    {
        if (screenStack.Count > 0)
            return;

        MainMenuScreen mainMenuScreen = GetScreen<MainMenuScreen>();

        if (mainMenuScreen != null &&
            IsScreenAllowedInScene(
                mainMenuScreen,
                sceneName))
        {
            PushRootScreen(mainMenuScreen);
            return;
        }

        GameplayMenuScreen gameplayScreen = GetScreen<GameplayMenuScreen>();

        if (gameplayScreen != null && IsScreenAllowedInScene(gameplayScreen, sceneName))
        {
            PushRootScreen(gameplayScreen);
        }
    }

    private void PushRootScreen(IUIScreen rootScreen)
    {
        Logger.Debug(
            isEnabled: showDebugLogs,
            category: nameof(UISystem),
            message: $"Inicializando tela raiz: {rootScreen.name}.",
            context: this
        );

        screenStack.Push(rootScreen);

        rootScreen.Show();

        CheckStackAndSwitchInput();
        RefreshUIState();

        PrintStack();
    }

    private bool IsRootScreen(IUIScreen screen)
    {
        if (screen == null)
            return false;

        return screen is GameplayMenuScreen ||
               screen is MainMenuScreen;
    }

    private void RefreshUIState()
    {
        RefreshCursor();
        RefreshCamera();
    }

    private void RefreshCursor()
    {
        IUIScreen currentScreen = CurrentScreen();

        if (currentScreen == null)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            return;
        }

        Cursor.lockState = currentScreen.ShowCursor
            ? CursorLockMode.None
            : CursorLockMode.Locked;

        Cursor.visible = currentScreen.ShowCursor;
    }

    private void RefreshCamera()
    {
        IUIScreen currentScreen = CurrentScreen();

        screenCameraInputLocked =
            currentScreen != null &&
            currentScreen.FreezeCamera;
    }

    private void CheckStackAndSwitchInput()
    {
        if (PlayerInputHandler.Instance == null)
            return;

        IUIScreen topScreen = CurrentScreen();

        if (topScreen == null ||
            topScreen is GameplayMenuScreen)
        {
            PlayerInputHandler.Instance.SwitchToGameplay();
            return;
        }

        PlayerInputHandler.Instance.SwitchToUI();
    }

    private void HandleReturnPressed()
    {
        IUIScreen currentScreen = CurrentScreen();

        if (currentScreen == null)
        {
            Back();
            return;
        }

        currentScreen.Return();
    }

    private void PrintStack()
    {
        Logger.Debug(
            isEnabled: showDebugLogs,
            category: nameof(UISystem),
            message: $"STACK COUNT -> {screenStack.Count}.",
            context: this
        );

        foreach (IUIScreen screen in screenStack)
        {
            if (screen == null)
                continue;

            Logger.Debug(
                isEnabled: showDebugLogs,
                category: nameof(UISystem),
                message: $"STACK ITEM -> {screen.name} " +
                         $"(Overlay: {screen.IsOverlay}).",
                context: this
            );
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (PlayerInputHandler.Instance != null)
        {
            PlayerInputHandler.Instance.OnReturnPressed += HandleReturnPressed;
            PlayerInputHandler.Instance.OnConfirmPressed += HandleConfirmPressed;
        }
        else
        {
            Logger.Warning(
                isEnabled: showDebugLogs,
                category: nameof(UISystem),
                message: "PlayerInputHandler.Instance está nulo durante o Start.",
                context: this
            );
        }

        SceneManager.activeSceneChanged += OnSceneChanged;

        string currentScene = SceneManager.GetActiveScene().name;

        RegisterScreensForScene(currentScene);
        InitializeRootScreen(currentScene);
        RefreshUIState();
    }

    private void Update()
    {

    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnSceneChanged;

        if (PlayerInputHandler.Instance != null)
        {
            PlayerInputHandler.Instance.OnReturnPressed -= HandleReturnPressed;
            PlayerInputHandler.Instance.OnConfirmPressed -= HandleConfirmPressed;
        }

        screenCameraInputLocked = false;
        presentationCameraInputLocked = false;
        manualCameraInputLocked = false;

        if (Instance == this)
        {
            Instance = null;
        }
    }
}