using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ArenaSpawnController : NetworkBehaviour
{
    [Header("1v1 Spawn Points")]
    [SerializeField] private Transform leftSpawnPoint;
    [SerializeField] private Transform rightSpawnPoint;

    [Header("Facing Mode")]
    [Tooltip("Enable if using a 2D side-scroller where facing left/right flips scale or Y rotation.")]
    [SerializeField] private bool is2DMode = false;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer) return;

        NetworkManager.OnClientConnectedCallback += HandleClientConnected;
        PositionAllConnectedPlayers();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientConnectedCallback -= HandleClientConnected;
        }

        base.OnNetworkDespawn();
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        PositionAllConnectedPlayers();
    }

    /// <summary>
    /// Places up to two connected players on opposite sides facing each other.
    /// </summary>
    public void PositionAllConnectedPlayers()
    {
        if (!IsServer || NetworkManager == null) return;
        if (leftSpawnPoint == null || rightSpawnPoint == null) return;

        List<NetworkClient> clients = new List<NetworkClient>(NetworkManager.ConnectedClientsList);
        if (clients.Count == 0) return;

        if (clients.Count >= 1 && clients[0].PlayerObject != null)
        {
            Transform p0 = clients[0].PlayerObject.transform;
            Vector3 p1Pos = clients.Count >= 2 && clients[1].PlayerObject != null
                ? clients[1].PlayerObject.transform.position
                : rightSpawnPoint.position;

            TeleportAndFace(p0, leftSpawnPoint.position, p1Pos);
        }

        if (clients.Count >= 2 && clients[1].PlayerObject != null)
        {
            Transform p1 = clients[1].PlayerObject.transform;
            Vector3 p0Pos = clients[0].PlayerObject != null
                ? clients[0].PlayerObject.transform.position
                : leftSpawnPoint.position;

            TeleportAndFace(p1, rightSpawnPoint.position, p0Pos);
        }
    }

    private void TeleportAndFace(Transform playerTransform, Vector3 targetPosition, Vector3 lookTargetPosition)
    {
        // Temporarily disable CharacterController if present so position sets cleanly
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