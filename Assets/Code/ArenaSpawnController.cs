using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ArenaSpawnController : NetworkBehaviour
{
    [Header("1v1 Spawn Points")]
    [SerializeField] private Transform leftSpawnPoint;
    [SerializeField] private Transform rightSpawnPoint;

    [Header("Player Prefabs (Must have NetworkObject)")]
    [SerializeField] private GameObject player1Prefab; // Host
    [SerializeField] private GameObject player2Prefab; // Client

    [Header("Facing Mode")]
    [Tooltip("Enable if using a 2D side-scroller where facing left/right flips scale or Y rotation.")]
    [SerializeField] private bool is2DMode = false;

    private readonly Dictionary<ulong, NetworkObject> _spawnedPlayers = new();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer) return;

        NetworkManager.OnClientConnectedCallback += HandleClientConnected;
        NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;

        SpawnAndPositionAllConnectedPlayers();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }

        base.OnNetworkDespawn();
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        SpawnAndPositionAllConnectedPlayers();
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        if (_spawnedPlayers.TryGetValue(clientId, out NetworkObject netObj))
        {
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(destroy: true);
            }
            _spawnedPlayers.Remove(clientId);
        }

        PositionExistingPlayers();
    }

    /// <summary>
    /// Spawns player1Prefab (Host) at leftSpawnPoint and player2Prefab (Client) at rightSpawnPoint,
    /// then faces both players toward each other.
    /// </summary>
    public void SpawnAndPositionAllConnectedPlayers()
    {
        if (!IsServer || NetworkManager == null) return;
        if (leftSpawnPoint == null || rightSpawnPoint == null) return;

        List<NetworkClient> clients = new List<NetworkClient>(NetworkManager.ConnectedClientsList);
        if (clients.Count == 0) return;

        foreach (NetworkClient client in clients)
        {
            ulong clientId = client.ClientId;
            bool isHostClient = clientId == NetworkManager.ServerClientId;

            if (_spawnedPlayers.TryGetValue(clientId, out NetworkObject existing) && existing != null && existing.IsSpawned)
                continue;

            GameObject prefabToSpawn = isHostClient ? player1Prefab : player2Prefab;
            Transform spawnPoint = isHostClient ? leftSpawnPoint : rightSpawnPoint;

            if (prefabToSpawn == null)
            {
                Debug.LogWarning($"[ArenaSpawnController] Missing prefab for client {clientId} (IsHost={isHostClient}).", this);
                continue;
            }

            NetworkObject spawned = SpawnPlayerObject(prefabToSpawn, clientId, spawnPoint.position);
            if (spawned != null)
            {
                _spawnedPlayers[clientId] = spawned;
            }
        }

        PositionExistingPlayers();
    }

    private NetworkObject SpawnPlayerObject(GameObject prefab, ulong ownerClientId, Vector3 position)
    {
        GameObject instance = Instantiate(prefab, position, Quaternion.identity);
        if (!instance.TryGetComponent(out NetworkObject netObj))
        {
            Debug.LogError($"[ArenaSpawnController] Prefab '{prefab.name}' does not have a NetworkObject component.", instance);
            Destroy(instance);
            return null;
        }

        netObj.SpawnAsPlayerObject(ownerClientId, destroyWithScene: true);
        return netObj;
    }

    public void PositionExistingPlayers()
    {
        if (!IsServer || NetworkManager == null) return;
        if (leftSpawnPoint == null || rightSpawnPoint == null) return;

        _spawnedPlayers.TryGetValue(NetworkManager.ServerClientId, out NetworkObject hostObj);

        NetworkObject clientObj = null;
        foreach (var kvp in _spawnedPlayers)
        {
            if (kvp.Key != NetworkManager.ServerClientId && kvp.Value != null)
            {
                clientObj = kvp.Value;
                break;
            }
        }

        Vector3 hostTargetPos = leftSpawnPoint.position;
        Vector3 clientTargetPos = rightSpawnPoint.position;

        if (hostObj != null && hostObj.IsSpawned)
        {
            Vector3 lookTarget = clientObj != null ? clientObj.transform.position : clientTargetPos;
            TeleportAndFace(hostObj.transform, hostTargetPos, lookTarget);
        }

        if (clientObj != null && clientObj.IsSpawned)
        {
            Vector3 lookTarget = hostObj != null ? hostObj.transform.position : hostTargetPos;
            TeleportAndFace(clientObj.transform, clientTargetPos, lookTarget);
        }
    }

    private void TeleportAndFace(Transform playerTransform, Vector3 targetPosition, Vector3 lookTargetPosition)
    {
        CharacterController cc = playerTransform.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        playerTransform.position = targetPosition;

        if (is2DMode)
        {
            float dirX = lookTargetPosition.x - targetPosition.x;
            Vector3 scale = playerTransform.localScale;
            scale.x = Mathf.Abs(scale.x) * (dirX >= 0f ? 1f : -1f);
            playerTransform.localScale = scale;
        }
        else
        {
            Vector3 flatDirection = lookTargetPosition - targetPosition;
            flatDirection.y = 0f;
            if (flatDirection.sqrMagnitude > 0.0001f)
            {
                playerTransform.rotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
            }
        }

        if (cc != null) cc.enabled = true;
    }
}