using System;
using Fusion;
using UnityEngine;

public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [Networked, Capacity(4)] public NetworkLinkedList<LobbyPlayerData> Players => default;
    [Networked] public int PlayersVersion { get; set; }

    public event Action OnPlayersChanged;

    private ChangeDetector changeDetector;
    private int playerJoinCounter;

    public override void Spawned()
    {
        Instance = this;
        changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        Debug.Log($"[LobbyManager] Spawned! HasStateAuthority={HasStateAuthority}, PlayersVersion={PlayersVersion}, Players.Count={Players.Count}");

        if (HasStateAuthority)
        {
            playerJoinCounter = 0;
            AddPlayer(Runner.LocalPlayer);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
            Instance = null;
    }

    public override void Render()
    {
        foreach (string propertyName in changeDetector.DetectChanges(this))
        {
            if (propertyName == nameof(PlayersVersion))
            {
                OnPlayersChanged?.Invoke();
            }
        }
    }

    public void AddPlayer(PlayerRef player)
    {
        if (!HasStateAuthority) return;

        // Check if player already exists
        foreach (LobbyPlayerData data in Players)
        {
            if (data.Player == player) return;
        }

        playerJoinCounter++;
        LobbyPlayerData playerData = new LobbyPlayerData
        {
            Player = player,
            PlayerName = $"Player {playerJoinCounter}",
            IsReady = false
        };
        Players.Add(playerData);
        PlayersVersion++;
        Debug.Log($"[LobbyManager] AddPlayer: {player}, PlayersVersion now={PlayersVersion}, Count={Players.Count}");
    }

    public void RemovePlayer(PlayerRef player)
    {
        if (!HasStateAuthority) return;

        for (int i = 0; i < Players.Count; i++)
        {
            if (Players.Get(i).Player == player)
            {
                Players.Remove(Players.Get(i));
                PlayersVersion++;
                return;
            }
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetPlayerName(PlayerRef player, NetworkString<_32> name)
    {
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players.Get(i).Player == player)
            {
                LobbyPlayerData data = Players.Get(i);
                data.PlayerName = name;
                Players.Set(i, data);
                PlayersVersion++;
                return;
            }
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_ToggleReady(PlayerRef player)
    {
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players.Get(i).Player == player)
            {
                LobbyPlayerData data = Players.Get(i);
                data.IsReady = !data.IsReady;
                Players.Set(i, data);
                PlayersVersion++;
                return;
            }
        }
    }

    public bool AreAllPlayersReady()
    {
        if (Players.Count == 0) return false;

        foreach (LobbyPlayerData data in Players)
        {
            if (!data.IsReady) return false;
        }
        return true;
    }
}
