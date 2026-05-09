using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerManager : NetworkBehaviour
{
    public static readonly int s_GamePlayerCount = 2;
    public static PlayerManager Instance;

    public Dictionary<ulong, Client> Players = new Dictionary<ulong, Client>();

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} connected.");
    }

    void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} disconnected.");
        UnregisterPlayer(clientId);
    }

    public void RegisterPlayer(ulong clientId, Client clientObj)
    {
        Players[clientId] = clientObj;
        var isHost = clientId == 0;
        clientObj.name = isHost ? "Host" : $"Client {clientId}";

        Debug.Log($"Players Count: {Players.Count}");
    }

    public void UnregisterPlayer(ulong clientId)
    {
        Players.Remove(clientId);
    }
    public Client GetHost()
    {
        return Players.ContainsKey(0) ? Players[0] : null;
    }

    public Client GetClient(ulong clientId)
    {
        return Players.ContainsKey(clientId) ? Players[clientId] : null;
    }
}