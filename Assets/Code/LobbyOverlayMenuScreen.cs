using System.Collections.Generic;
using Steamworks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public enum LobbyTab
{
    Lobby,
    LobbySettings, // Host only
    Character,
    Rolima
}

[RequireComponent(typeof(LobbyNetworkState))]
public class LobbyOverlayMenuScreen : NetworkUIScreen
{
    [Header("Templates & Maps")]
    [SerializeField] private VisualTreeAsset playerCardTemplate;
    [SerializeField] private List<string> allAvailableMaps = new() { "Arena_01" };
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private LobbyNetworkState _networkState;
    private LobbyPlayerListController _playerList;
    private LobbySettingsPanelController _settingsController;
    private PlayerInputHandler _playerInputHandler;

    // Header Tabs
    private LobbyTab _currentTab = LobbyTab.Lobby;
    private readonly List<LobbyTab> _orderedTabs = new();
    private readonly Dictionary<LobbyTab, Label> _tabLabels = new();
    private readonly Dictionary<LobbyTab, VisualElement> _tabPanels = new();

    private Image _cycleLeftIcon;
    private Image _cycleRightIcon;
    private Label _lobbySettingsTabLabel;

    // Header Lobby Code
    private Label _lobbyCodeLabel;
    private Button _copyLobbyCodeButton;
    private Button _toggleCodeVisibilityButton;
    private bool _isCodeHidden;

    // Footer Legend
    private VisualElement _legendNavigateGroup;
    private Image _legendNavigateIcon;
    private Image _legendConfirmIcon;
    private Image _legendTabsIcon;
    private Image _legendBackIcon;
    private Label _legendBackLabel;

    private bool _isLeavingLobby;

    protected override void Awake()
    {
        base.Awake();
        _networkState = GetComponent<LobbyNetworkState>();

        if (rootVisualElement == null) return;
        visualizationRoot = rootVisualElement.Q<VisualElement>("root") ?? rootVisualElement;

        // Header
        _cycleLeftIcon = rootVisualElement.Q<Image>("cycleLeftIcon");
        _cycleRightIcon = rootVisualElement.Q<Image>("cycleRightIcon");
        _lobbySettingsTabLabel = rootVisualElement.Q<Label>("lobbySettingsTabLabel");

        _lobbyCodeLabel = rootVisualElement.Q<Label>("lobbyCodeLabel");
        _copyLobbyCodeButton = rootVisualElement.Q<Button>("copyLobbyCodeButton");
        _toggleCodeVisibilityButton = rootVisualElement.Q<Button>("toggleCodeVisibilityButton");

        _copyLobbyCodeButton?.RegisterCallback<ClickEvent>(_ => CopyLobbyCodeToClipboard());
        _toggleCodeVisibilityButton?.RegisterCallback<ClickEvent>(_ => ToggleLobbyCodeVisibility());

        // Panels
        VisualElement lobbyPanel = rootVisualElement.Q<VisualElement>("lobbyPanel");
        VisualElement lobbySettingsPanel = rootVisualElement.Q<VisualElement>("lobbySettingsPanel");

        _tabPanels[LobbyTab.Lobby] = lobbyPanel;
        _tabPanels[LobbyTab.LobbySettings] = lobbySettingsPanel;
        _tabPanels[LobbyTab.Character] = rootVisualElement.Q<VisualElement>("characterPanel");
        _tabPanels[LobbyTab.Rolima] = rootVisualElement.Q<VisualElement>("rolimaPanel");

        RegisterTab(LobbyTab.Lobby, rootVisualElement.Q<Label>("lobbyTabLabel"));
        RegisterTab(LobbyTab.Character, rootVisualElement.Q<Label>("characterTabLabel"));
        RegisterTab(LobbyTab.Rolima, rootVisualElement.Q<Label>("rolimaTabLabel"));

        // Footer
        _legendNavigateGroup = rootVisualElement.Q<VisualElement>("legendNavigate");
        _legendNavigateIcon = rootVisualElement.Q<Image>("legendNavigateIcon");
        _legendConfirmIcon = rootVisualElement.Q<Image>("legendConfirmIcon");
        _legendTabsIcon = rootVisualElement.Q<Image>("legendTabsIcon");
        _legendBackIcon = rootVisualElement.Q<Image>("legendBackIcon");
        _legendBackLabel = rootVisualElement.Q<Label>("legendBackLabel");

        // Sub-panel controllers
        _playerList = new LobbyPlayerListController(lobbyPanel, playerCardTemplate);
        _playerList.ReadyButton?.RegisterCallback<ClickEvent>(_ => HandlePrimaryActionButton());

        _settingsController = new LobbySettingsPanelController(lobbySettingsPanel, _networkState, allAvailableMaps);
    }

    protected override void Start()
    {
        base.Start();
        _playerInputHandler = PlayerInputHandler.Instance;
    }

    public override void OnNetworkSpawn()
    {
        _isLeavingLobby = false;

        _networkState.OnLobbyCodeUpdated += RefreshLobbyCodeLabel;
        _networkState.OnPlayersUpdated += RefreshPlayers;
        _networkState.OnSettingsUpdated += HandleSettingsUpdated;

        base.OnNetworkSpawn();

        UISystem.Instance?.Open<LobbyOverlayMenuScreen>();

        RefreshLobbyCodeLabel(_networkState.LobbyCode);
        RefreshPlayers();
    }

    public override void Show()
    {
        base.Show();
    }

    public override void OnNetworkDespawn()
    {
        _networkState.OnLobbyCodeUpdated -= RefreshLobbyCodeLabel;
        _networkState.OnPlayersUpdated -= RefreshPlayers;
        _networkState.OnSettingsUpdated -= HandleSettingsUpdated;

        base.OnNetworkDespawn();
    }

    protected override void OnHostSpawn()
    {
        if (_lobbySettingsTabLabel != null)
        {
            _lobbySettingsTabLabel.style.display = DisplayStyle.Flex;
            RegisterTab(LobbyTab.LobbySettings, _lobbySettingsTabLabel, insertIndex: 1);
        }

        _settingsController.InitializeHostControls();
        RefreshPlayers();
    }

    protected override void OnClientSpawn()
    {
        if (_lobbySettingsTabLabel != null)
            _lobbySettingsTabLabel.style.display = DisplayStyle.None;

        RefreshPlayers();
    }

    protected override void OnShow()
    {
        base.OnShow();

        if (_playerInputHandler == null)
            _playerInputHandler = PlayerInputHandler.Instance;

        if (_playerInputHandler != null)
        {
            _playerInputHandler.OnReturnPressed += HandleBackAction;
        }

        SelectTab(_currentTab);
        RefreshPlayers();
    }

    protected override void OnHide()
    {
        base.OnHide();

        if (_playerInputHandler != null)
        {
            _playerInputHandler.OnReturnPressed -= HandleBackAction;
        }
    }

    private void HandleSettingsUpdated(LobbySettings settings)
    {
        if (!IsHost && _currentTab == LobbyTab.LobbySettings)
        {
            SelectTab(LobbyTab.Lobby);
        }

        if (IsHost)
        {
            _settingsController?.SyncWithSettings(settings);
        }

        RefreshPlayers();
    }

    private void RefreshPlayers()
    {
        if (_playerList == null || _networkState == null || _networkState.Players == null) return;

        var players = new List<LobbyPlayerData>();
        bool localReady = false;

        ulong localSteamId = SteamManager.Instance != null && SteamManager.Instance.IsInitialized
            ? SteamClient.SteamId.Value
            : 0;
        ulong localClientId = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening
            ? NetworkManager.Singleton.LocalClientId
            : 0;

        foreach (LobbyPlayerData p in _networkState.Players)
        {
            players.Add(p);

            bool isThisLocal = (localSteamId != 0 && p.SteamId == localSteamId) ||
                               (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && p.ClientId == localClientId);
            if (isThisLocal)
            {
                localReady = p.IsReady;
            }
        }

        _playerList.SetPlayers(players);

        if (_playerList.ReadyButton != null)
        {
            bool netRunning = _networkState.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

            if (IsHost)
            {
                bool canStart = netRunning && _networkState.CanStartMatch();
                _playerList.ReadyButton.text = canStart ? "START MATCH" : "WAITING FOR PLAYER";
                _playerList.ReadyButton.SetEnabled(canStart);
                _playerList.ReadyButton.EnableInClassList("btn--active", canStart);
            }
            else
            {
                _playerList.ReadyButton.SetEnabled(netRunning);
                _playerList.SetLocalReadyButtonState(localReady);
            }
        }
    }

    private void HandlePrimaryActionButton()
    {
        if (_networkState == null || !_networkState.IsSpawned) return;
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;

        if (IsHost)
        {
            _networkState.HostStartMatch();
        }
        else
        {
            _networkState.RequestToggleReady();
        }
    }

    private void RefreshLobbyCodeLabel(string code)
    {
        if (_lobbyCodeLabel == null) return;

        if (string.IsNullOrEmpty(code))
        {
            _lobbyCodeLabel.text = "------";
            return;
        }

        _lobbyCodeLabel.text = _isCodeHidden ? new string('•', code.Length) : code;
    }

    private void ToggleLobbyCodeVisibility()
    {
        _isCodeHidden = !_isCodeHidden;
        if (_toggleCodeVisibilityButton != null)
            _toggleCodeVisibilityButton.text = _isCodeHidden ? "Mostrar" : "Ocultar";

        RefreshLobbyCodeLabel(_networkState.LobbyCode);
    }

    private void CopyLobbyCodeToClipboard()
    {
        string code = _networkState.LobbyCode;
        if (string.IsNullOrEmpty(code)) return;

        GUIUtility.systemCopyBuffer = code;

        if (_copyLobbyCodeButton != null)
        {
            _copyLobbyCodeButton.text = "Copiado!";
            _copyLobbyCodeButton.schedule.Execute(() => _copyLobbyCodeButton.text = "Copiar").ExecuteLater(1500);
        }
    }

    private void RegisterTab(LobbyTab tab, Label label, int insertIndex = -1)
    {
        if (label == null || _tabLabels.ContainsKey(tab)) return;

        _tabLabels[tab] = label;

        if (insertIndex >= 0 && insertIndex <= _orderedTabs.Count)
            _orderedTabs.Insert(insertIndex, tab);
        else
            _orderedTabs.Add(tab);

        label.RegisterCallback<PointerDownEvent>(_ => SelectTab(tab));
    }

    public void SelectTab(LobbyTab tab)
    {
        if (tab == LobbyTab.LobbySettings && !IsHost)
            return;

        _currentTab = tab;

        foreach (var kvp in _tabLabels)
            kvp.Value.EnableInClassList("tabSelected", kvp.Key == tab);

        foreach (var kvp in _tabPanels)
        {
            if (kvp.Value == null) continue;

            bool isActive = kvp.Key == tab;
            kvp.Value.style.display = isActive ? DisplayStyle.Flex : DisplayStyle.None;
            SetElementInteractable(kvp.Value, isActive);
        }

        if (tab == LobbyTab.LobbySettings && IsHost)
            _settingsController.SyncWithSettings(_networkState.Settings);

        UpdateFooterLegendForTab(tab);
    }

    private void HandleBackAction()
    {
        if (_currentTab != LobbyTab.Lobby)
        {
            SelectTab(LobbyTab.Lobby);
            return;
        }

        LeaveLobbyAndTransition();
    }

    private void LeaveLobbyAndTransition()
    {
        if (_isLeavingLobby) return;
        _isLeavingLobby = true;

        SteamManager.Instance?.LeaveCurrentLobby();

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        if (TransitionSystem.Instance != null && !string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            TransitionSystem.Instance.LoadScene(mainMenuSceneName);
        }
        else
        {
            Return();
        }
    }

    private void UpdateFooterLegendForTab(LobbyTab tab)
    {
        if (_legendBackLabel != null)
            _legendBackLabel.text = tab == LobbyTab.Lobby ? "Sair do Lobby" : "Voltar ao Lobby";

        if (_legendNavigateGroup != null)
        {
            bool hasNavigableItems = tab == LobbyTab.LobbySettings || tab == LobbyTab.Character || tab == LobbyTab.Rolima;
            _legendNavigateGroup.style.display = hasNavigableItems ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private void SetIcon(Image target, Sprite sprite)
    {
        if (target != null && sprite != null)
            target.style.backgroundImage = new StyleBackground(sprite);
    }
}