using System;
using System.Threading.Tasks;
using Netcode.Transports.Facepunch;
using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
using UnityConsole;
using UnityEngine;

[ConsoleContainer("steam")]
public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    public Lobby? CurrentLobby { get; private set; }
    public string CurrentLobbyCode { get; private set; }

    [SerializeField] private int maxAmountOfPlayers = 2;
    [SerializeField] private int codeLength = 6;

    private const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public async Task<bool> CreateHost()
    {
        if (!await CreateLobby())
            return false;

        if (NetworkManager.Singleton == null)
        {
            Logger.Error("Lobby", "NetworkManager.Singleton está nulo! Verifique se o NetworkManager está na cena.");
            return false;
        }

        NetworkManager.Singleton.StartHost();

        if (!NetworkManager.Singleton.IsListening)
        {
            Logger.Error("Lobby", "Falha ao iniciar o Host no NetworkManager.");
            return false;
        }

        return true;
    }

    public async Task<bool> CreateLobby()
    {
        try
        {
            int capacity = Mathf.Clamp(maxAmountOfPlayers, 2, 2);
            var lobby = await SteamMatchmaking.CreateLobbyAsync(capacity);

            if (!lobby.HasValue)
            {
                Logger.Error("Lobby", "Lobby Creation Failed!");
                return false;
            }

            CurrentLobby = lobby.Value;
            CurrentLobbyCode = GenerateLobbyCode(codeLength);

            CurrentLobby.Value.SetData("lobby_code", CurrentLobbyCode);
            CurrentLobby.Value.SetPublic();
            CurrentLobby.Value.SetJoinable(true);

            Logger.Debug(true, "Lobby Manager", $"The lobby code is {CurrentLobbyCode}");

            SteamManager.Instance?.SetCurrentLobby(CurrentLobby);

            Logger.Debug(true, "Lobby", message: $"Lobby criada! Código: {CurrentLobbyCode} | Máx: {CurrentLobby.Value.MaxMembers}");
            return true;
        }
        catch (Exception e)
        {
            Logger.Error("Lobby", message: $"{e}");
            return false;
        }
    }

    public async Task<bool> JoinLobbyByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            Logger.Warning(true, "Lobby", "Código da lobby vazio.");
            return false;
        }

        string formattedCode = code.Trim().ToUpperInvariant();

        try
        {
            Lobby[] matchingLobbies = await SteamMatchmaking.LobbyList
                .FilterDistanceWorldwide()
                .WithKeyValue("lobby_code", formattedCode)
                .WithMaxResults(1)
                .RequestAsync();

            if (matchingLobbies == null || matchingLobbies.Length == 0)
            {
                Logger.Warning(true, "Lobby", $"Nenhuma lobby encontrada com o código: {formattedCode}");
                return false;
            }

            RoomEnter enterResult = await matchingLobbies[0].Join();
            if (enterResult != RoomEnter.Success)
            {
                Logger.Error("Lobby", $"Falha ao entrar na lobby ({enterResult}).");
                return false;
            }

            CurrentLobby = matchingLobbies[0];
            CurrentLobbyCode = formattedCode;
            SteamManager.Instance?.SetCurrentLobby(CurrentLobby);

            if (NetworkManager.Singleton == null)
            {
                Logger.Error("Lobby", "NetworkManager.Singleton está nulo! Verifique se o NetworkManager está na cena.");
                return false;
            }

            if (!NetworkManager.Singleton.TryGetComponent<FacepunchTransport>(out var transport))
            {
                Logger.Error("Lobby", "FacepunchTransport não encontrado no mesmo GameObject do NetworkManager!");
                return false;
            }

            transport.targetSteamId = CurrentLobby.Value.Owner.Id;
            NetworkManager.Singleton.StartClient();

            Logger.Debug(true, "Lobby", message: $"Entrou na lobby com código: {formattedCode}");
            return true;
        }
        catch (Exception e)
        {
            Logger.Error("Lobby", message: $"Erro ao tentar entrar na lobby: {e.Message}");
            return false;
        }
    }

    private string GenerateLobbyCode(int length)
    {
        int safeLength = Mathf.Clamp(length, 4, 12);
        char[] buffer = new char[safeLength];
        for (int i = 0; i < safeLength; i++)
        {
            buffer[i] = CodeChars[UnityEngine.Random.Range(0, CodeChars.Length)];
        }
        return new string(buffer);
    }

    [ConsoleCommand(name: "join_lobby", description: "Entra em uma lobby usando o código fornecido!")]
    private async void JoinLobbyCommand(string lobbyCode)
    {
        await JoinLobbyByCode(lobbyCode);
    }

    [ConsoleCommand(name: "create_lobby")]
    private async void TestLobby()
    {
        try
        {
            await CreateHost();
            UISystem.Instance?.Open<LobbyOverlayMenuScreen>();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }
}