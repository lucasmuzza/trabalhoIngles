using System;
using Steamworks;
using Unity.Collections;
using Unity.Netcode;

public struct LobbyPlayerData : INetworkSerializable, IEquatable<LobbyPlayerData>
{
    public ulong ClientId;
    public ulong SteamId;
    public FixedString64Bytes PlayerName;
    public bool IsReady;
    public bool IsHost;

    public bool IsLocal
    {
        get
        {
            if (SteamManager.Instance != null && SteamManager.Instance.IsInitialized && SteamId != 0)
            {
                return SteamId == SteamClient.SteamId.Value;
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                return ClientId == NetworkManager.Singleton.LocalClientId;
            }

            return false;
        }
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref SteamId);
        serializer.SerializeValue(ref PlayerName);
        serializer.SerializeValue(ref IsReady);
        serializer.SerializeValue(ref IsHost);
    }

    public bool Equals(LobbyPlayerData other)
    {
        return ClientId == other.ClientId &&
               SteamId == other.SteamId &&
               PlayerName == other.PlayerName &&
               IsReady == other.IsReady &&
               IsHost == other.IsHost;
    }

    public override bool Equals(object obj) => obj is LobbyPlayerData other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(ClientId, SteamId, PlayerName, IsReady, IsHost);
}