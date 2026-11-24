using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

public class SteamManager : MonoBehaviour
{
    public static SteamManager Instance { get; private set; }

    [SerializeField] private uint appId = 480;
    [SerializeField] private bool showDebugLogs = true;

    public Texture2D CurrentUserAvatar { get; private set; }
    public Lobby? CurrentLobby { get; private set; }
    public bool IsInitialized => _isInitialized;

    private bool _isInitialized;
    private string _avatarCacheDir;
    private readonly Dictionary<ulong, Texture2D> _memoryCache = new Dictionary<ulong, Texture2D>();

    private async void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        _avatarCacheDir = Path.Combine(Application.persistentDataPath, "SteamAvatars");
        Directory.CreateDirectory(_avatarCacheDir);

        try
        {
            SteamClient.Init(appid: appId, asyncCallbacks: true);
            _isInitialized = true;

            if (showDebugLogs)
            {
                Debug.Log(
                    $"[SteamPluginCheck] Steam inicializado com sucesso. Usuário: {SteamClient.Name} ({SteamClient.SteamId}) | IsValid: {SteamClient.IsValid}",
                    this
                );
            }
        }
        catch (Exception exception)
        {
            _isInitialized = false;
            Debug.LogError(
                $"[SteamPluginCheck] Falha ao inicializar o SteamClient: {exception.Message}",
                this
            );
            return;
        }

        CurrentUserAvatar = await GetOrCacheAvatarAsync(SteamClient.SteamId);
    }

    public void SetCurrentLobby(Lobby? lobby)
    {
        CurrentLobby = lobby;
        if (CurrentLobby.HasValue)
        {
            Logger.Debug(
                true,
                "Steam Manager",
                message: $"The current lobby code is {CurrentLobby.Value.GetData("lobby_code")}"
            );
        }
    }

    public void SyncLobbyMetadata(LobbySettings settings)
    {
        if (!_isInitialized || !CurrentLobby.HasValue)
            return;

        Lobby lobby = CurrentLobby.Value;

        lobby.SetData("game_mode", "1v1_Adverbs");
        lobby.SetData("preset", settings.PresetName.ToString());
        lobby.SetData("map", settings.MapName.ToString());
        lobby.SetData("phrase_count", settings.PhrasesAndAnswers.Length.ToString());

        lobby.MaxMembers = Mathf.Clamp(settings.MaxPlayers, 2, 2);

        if (settings.IsPrivate)
            lobby.SetFriendsOnly();
        else
            lobby.SetPublic();
    }

    public void LeaveCurrentLobby()
    {
        if (CurrentLobby.HasValue)
        {
            CurrentLobby.Value.Leave();
            CurrentLobby = null;
        }
    }

    public async Task<Texture2D> GetOrCacheAvatarAsync(SteamId steamId, bool forceRefresh = false)
    {
        ulong id = steamId.Value;
        string filePath = GetAvatarFilePath(id);

        if (!forceRefresh && _memoryCache.TryGetValue(id, out Texture2D cachedTex) && cachedTex != null)
        {
            return cachedTex;
        }

        if (!forceRefresh && File.Exists(filePath))
        {
            byte[] pngBytes = File.ReadAllBytes(filePath);
            Texture2D diskTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (diskTex.LoadImage(pngBytes))
            {
                diskTex.filterMode = FilterMode.Trilinear;
                _memoryCache[id] = diskTex;
                return diskTex;
            }
            Destroy(diskTex);
        }

        if (!_isInitialized)
        {
            return null;
        }

        Image? steamImage = null;
        try
        {
            steamImage = await SteamFriends.GetLargeAvatarAsync(steamId);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to fetch Steam avatar for {id}: {e.Message}", this);
            return null;
        }

        if (!steamImage.HasValue)
        {
            return null;
        }

        Texture2D texture = steamImage.Value.ToTexture2D();
        _memoryCache[id] = texture;

        try
        {
            byte[] encodedPng = texture.EncodeToPNG();
            if (encodedPng != null)
            {
                File.WriteAllBytes(filePath, encodedPng);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to write avatar cache for {id}: {e.Message}", this);
        }

        return texture;
    }

    private string GetAvatarFilePath(ulong steamId)
    {
        return Path.Combine(_avatarCacheDir, $"avatar_{steamId}.png");
    }

    private void Update()
    {
        if (!_isInitialized)
        {
            return;
        }

        SteamClient.RunCallbacks();
    }

    private void OnApplicationQuit()
    {
        ShutdownSteam();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            ShutdownSteam();
            Instance = null;
        }
    }

    private void ShutdownSteam()
    {
        if (!_isInitialized)
        {
            return;
        }

        LeaveCurrentLobby();
        SteamClient.Shutdown();
        _isInitialized = false;

        if (showDebugLogs)
        {
            Debug.Log("[SteamPluginCheck] SteamClient encerrado.", this);
        }
    }
}

public static class SteamImageExtensions
{
    public static Texture2D ToTexture2D(this Image image)
    {
        int width = (int)image.Width;
        int height = (int)image.Height;

        var avatar = new Texture2D(width, height, TextureFormat.RGBA32, false);
        avatar.filterMode = FilterMode.Trilinear;

        var pixels = new Color32[width * height];

        for (int y = 0; y < height; y++)
        {
            int flippedY = height - 1 - y;
            for (int x = 0; x < width; x++)
            {
                var p = image.GetPixel(x, y);
                pixels[flippedY * width + x] = new Color32(p.r, p.g, p.b, p.a);
            }
        }

        avatar.SetPixels32(pixels);
        avatar.Apply();
        return avatar;
    }
}