using System;
using Steamworks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class LobbyNetworkState : NetworkBehaviour
{
    [SerializeField] private string defaultArenaMap = "Arena_01";
    [SerializeField] private int requiredPlayersFor1v1 = 2;
    [SerializeField] private int fallbackCodeLength = 6;

    private const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly NetworkVariable<LobbySettings> _settings = new(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<FixedString32Bytes> _lobbyCode = new(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );

    private NetworkList<LobbyPlayerData> _players;
    private bool _isStartingMatch;

    public LobbySettings Settings => _settings.Value;
    public string LobbyCode => _lobbyCode.Value.ToString();
    public NetworkList<LobbyPlayerData> Players => _players;

    public event Action<LobbySettings> OnSettingsUpdated;
    public event Action<string> OnLobbyCodeUpdated;
    public event Action OnPlayersUpdated;

    private void Awake()
    {
        _players = new NetworkList<LobbyPlayerData>(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server
        );
    }

    public override void OnNetworkSpawn()
    {
        _isStartingMatch = false;

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

            LobbySettings initial = LobbySettings.CreateDefault1v1(defaultArenaMap);
            _settings.Value = initial;

            string code = LobbyManager.Instance != null && !string.IsNullOrEmpty(LobbyManager.Instance.CurrentLobbyCode)
                ? LobbyManager.Instance.CurrentLobbyCode
                : SteamManager.Instance?.CurrentLobby?.GetData("lobby_code");

            if (string.IsNullOrEmpty(code))
                code = GenerateFallbackCode(fallbackCodeLength);

            _lobbyCode.Value = new FixedString32Bytes(code);

            if (SteamManager.Instance != null && SteamManager.Instance.IsInitialized && SteamManager.Instance.CurrentLobby.HasValue)
            {
                SteamManager.Instance.CurrentLobby.Value.SetData("lobby_code", code);
                SteamManager.Instance.SyncLobbyMetadata(initial);
            }
        }

        _settings.OnValueChanged += HandleSettingsChanged;
        _lobbyCode.OnValueChanged += HandleCodeChanged;
        _players.OnListChanged += HandlePlayersListChanged;

        ulong localSteamId = SteamManager.Instance != null && SteamManager.Instance.IsInitialized
            ? SteamClient.SteamId.Value
            : 0;
        string localName = SteamManager.Instance != null && SteamManager.Instance.IsInitialized
            ? SteamClient.Name
            : $"Player_{NetworkManager.Singleton.LocalClientId}";

        RegisterPlayerServerRpc(localSteamId, localName);
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;

        _settings.OnValueChanged -= HandleSettingsChanged;
        _lobbyCode.OnValueChanged -= HandleCodeChanged;
        _players.OnListChanged -= HandlePlayersListChanged;
    }

    private static string GenerateFallbackCode(int length)
    {
        int safeLength = Mathf.Clamp(length, 4, 12);
        char[] buffer = new char[safeLength];
        for (int i = 0; i < safeLength; i++)
        {
            buffer[i] = CodeChars[UnityEngine.Random.Range(0, CodeChars.Length)];
        }
        return new string(buffer);
    }

    public void ServerUpdateSettings(LobbySettings newSettings)
    {
        if (!IsServer) return;

        newSettings.MaxPlayers = LobbySettings.DefaultMaxPlayers1v1;
        if (newSettings.MapName.IsEmpty)
            newSettings.MapName = new FixedString32Bytes(defaultArenaMap);

        _settings.Value = newSettings;
        SteamManager.Instance?.SyncLobbyMetadata(newSettings);
    }

    public void RequestToggleReady()
    {
        if (IsHost) return;
        if (!IsSpawned || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return;

        ToggleReadyServerRpc();
    }

    public bool CanStartMatch()
    {
        if (_isStartingMatch) return false;
        if (_players == null || _players.Count < requiredPlayersFor1v1) return false;
        if (_settings.Value.PhrasesAndAnswers.Length == 0) return false;

        foreach (LobbyPlayerData player in _players)
        {
            if (!player.IsReady)
                return false;
        }
        return true;
    }

    public void HostStartMatch()
    {
        if (!IsServer || !CanStartMatch()) return;

        _isStartingMatch = true;

        SteamManager.Instance?.CurrentLobby?.SetJoinable(false);

        BeginMatchTransitionClientRpc(new FixedString64Bytes("Gameplay"));
    }

    [Rpc(SendTo.Everyone)]
    private void BeginMatchTransitionClientRpc(FixedString64Bytes targetScene)
    {
        string sceneName = targetScene.ToString();

        if (TransitionSystem.Instance != null)
        {
            TransitionSystem.Instance.BeginNetworkTransition(sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        else if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(
                sceneName,
                UnityEngine.SceneManagement.LoadSceneMode.Single
            );
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RegisterPlayerServerRpc(ulong steamId, FixedString64Bytes playerName, RpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return;

        ulong senderId = rpcParams.Receive.SenderClientId;

        for (int i = 0; i < _players.Count; i++)
        {
            if (_players[i].ClientId == senderId)
                return;
        }

        if (_players.Count >= LobbySettings.DefaultMaxPlayers1v1)
            return;

        bool isHostPlayer = senderId == NetworkManager.ServerClientId;

        _players.Add(new LobbyPlayerData
        {
            ClientId = senderId,
            SteamId = steamId,
            PlayerName = playerName,
            IsReady = isHostPlayer,
            IsHost = isHostPlayer
        });
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ToggleReadyServerRpc(RpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return;

        ulong senderId = rpcParams.Receive.SenderClientId;

        if (senderId == NetworkManager.ServerClientId)
            return;

        for (int i = 0; i < _players.Count; i++)
        {
            if (_players[i].ClientId == senderId)
            {
                LobbyPlayerData updated = _players[i];
                updated.IsReady = !updated.IsReady;
                _players[i] = updated;
                break;
            }
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        for (int i = _players.Count - 1; i >= 0; i--)
        {
            if (_players[i].ClientId == clientId)
            {
                _players.RemoveAt(i);
                break;
            }
        }
    }

    private void HandleSettingsChanged(LobbySettings _, LobbySettings current) => OnSettingsUpdated?.Invoke(current);
    private void HandleCodeChanged(FixedString32Bytes _, FixedString32Bytes current) => OnLobbyCodeUpdated?.Invoke(current.ToString());
    private void HandlePlayersListChanged(NetworkListEvent<LobbyPlayerData> _) => OnPlayersUpdated?.Invoke();
}