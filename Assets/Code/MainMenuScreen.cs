using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class MainMenuScreen : UIScreen
{
    private Button createLobbyButton;
    private Button joinLobbyButton;
    private Button quitButton;

    // Join-by-code row (hidden until "Join Lobby" is pressed)
    private VisualElement joinPanel;
    private TextField joinCodeField;
    private Button confirmJoinButton;
    private Button cancelJoinButton;
    private Label joinErrorLabel;

    private bool _isBusy;

    protected override void Awake()
    {
        base.Awake();

        createLobbyButton = rootVisualElement.Q<Button>("createLobbyButton");
        joinLobbyButton = rootVisualElement.Q<Button>("joinLobbyButton");
        quitButton = rootVisualElement.Q<Button>("quitButton");

        joinPanel = rootVisualElement.Q<VisualElement>("joinPanel");
        joinCodeField = rootVisualElement.Q<TextField>("joinCodeField");
        confirmJoinButton = rootVisualElement.Q<Button>("confirmJoinButton");
        cancelJoinButton = rootVisualElement.Q<Button>("cancelJoinButton");
        joinErrorLabel = rootVisualElement.Q<Label>("joinErrorLabel");

        if (createLobbyButton != null) createLobbyButton.clicked += CreateLobby;
        if (joinLobbyButton != null) joinLobbyButton.clicked += ShowJoinPanel;
        if (quitButton != null) quitButton.clicked += QuitGame;

        if (confirmJoinButton != null) confirmJoinButton.clicked += JoinLobby;
        if (cancelJoinButton != null) cancelJoinButton.clicked += HideJoinPanel;

        // ENTER submits, ESC closes
        joinCodeField?.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                JoinLobby();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                HideJoinPanel();
                evt.StopPropagation();
            }
        });

        // Clicking the dark backdrop (not the card) closes the popup
        joinPanel?.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.target == joinPanel)
                HideJoinPanel();
        });

        HideJoinPanel();
    }

    // --- CREATE ---
    private async void CreateLobby()
    {
        if (_isBusy) return;
        SetBusy(true);

        bool success = await TryCreateLobbyAsync();

        SetBusy(false);

        if (!success)
        {
            Debug.LogWarning("[MainMenu] Failed to create lobby.");
            return;
        }

        OpenLobbyOverlay();
    }

    private async Task<bool> TryCreateLobbyAsync()
    {
        if (LobbyManager.Instance == null)
        {
            Debug.LogError("[MainMenu] LobbyManager.Instance is null.");
            return false;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Debug.LogWarning("[MainMenu] NetworkManager is already running.");
            return false;
        }

        return await LobbyManager.Instance.CreateHost();
    }

    // --- JOIN ---
    private void ShowJoinPanel()
    {
        if (joinPanel == null) return;

        joinPanel.style.display = DisplayStyle.Flex;
        joinCodeField?.Focus();
    }

    private void HideJoinPanel()
    {
        if (joinPanel == null) return;

        joinPanel.style.display = DisplayStyle.None;
        if (joinCodeField != null) joinCodeField.value = string.Empty;
        ShowJoinError(string.Empty);
    }

    private void ShowJoinError(string message)
    {
        if (joinErrorLabel == null) return;

        joinErrorLabel.text = message;
        joinErrorLabel.style.display = string.IsNullOrEmpty(message)
            ? DisplayStyle.None
            : DisplayStyle.Flex;
    }

    private async void JoinLobby()
    {
        if (_isBusy) return;

        ShowJoinError(string.Empty);

        string code = joinCodeField?.value?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code))
        {
            ShowJoinError("Enter a lobby code first.");
            return;
        }

        SetBusy(true);

        bool success = await TryJoinLobbyAsync(code);

        SetBusy(false);

        if (!success)
        {
            ShowJoinError($"Could not join lobby '{code}'.");
            return;
        }

        HideJoinPanel();
        OpenLobbyOverlay();
    }

    private async Task<bool> TryJoinLobbyAsync(string code)
    {
        if (LobbyManager.Instance == null)
        {
            Debug.LogError("[MainMenu] LobbyManager.Instance is null.");
            return false;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Debug.LogWarning("[MainMenu] NetworkManager is already running.");
            return false;
        }

        return await LobbyManager.Instance.JoinLobbyByCode(code);
    }

    // --- SHARED ---
    private void OpenLobbyOverlay()
    {
        if (UISystem.Instance == null)
        {
            Debug.LogError("[MainMenu] UISystem.Instance is null.");
            return;
        }

        UISystem.Instance.Open<LobbyOverlayMenuScreen>();
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;

        createLobbyButton?.SetEnabled(!busy);
        joinLobbyButton?.SetEnabled(!busy);
        confirmJoinButton?.SetEnabled(!busy);
    }

    private void QuitGame()
    {
        Application.Quit();
    }
}