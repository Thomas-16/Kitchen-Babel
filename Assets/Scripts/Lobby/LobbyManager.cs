using System;
using System.Collections.Generic;
using System.Linq;
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
            // On migration, Players list is restored from snapshot — continue numbering from current count
            // On fresh start, Players.Count is 0 so this is equivalent to the old playerJoinCounter = 0
            playerJoinCounter = Players.Count;

            // Remove stale player entries from the old host or disconnected players
            RemoveStalePlayerEntries();

            // Add all active players (covers both fresh start and return-from-game)
            foreach (PlayerRef player in Runner.ActivePlayers)
            {
                AddPlayer(player);
            }
        }
    }

    private void RemoveStalePlayerEntries()
    {
        List<PlayerRef> activePlayers = Runner.ActivePlayers.ToList();
        bool changed = false;

        for (int i = Players.Count - 1; i >= 0; i--)
        {
            LobbyPlayerData data = Players.Get(i);
            if (!activePlayers.Contains(data.Player))
            {
                Debug.Log($"[LobbyManager] Removing stale player entry: {data.Player}");
                Players.Remove(data);
                changed = true;
            }
        }

        if (changed)
        {
            PlayersVersion++;
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

    public void ResetReadyStates()
    {
        if (!HasStateAuthority) return;

        for (int i = 0; i < Players.Count; i++)
        {
            LobbyPlayerData data = Players.Get(i);
            if (data.IsReady)
            {
                data.IsReady = false;
                Players.Set(i, data);
            }
        }
        PlayersVersion++;
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
