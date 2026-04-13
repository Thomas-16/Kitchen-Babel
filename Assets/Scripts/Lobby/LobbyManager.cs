using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [Networked, Capacity(6)] public NetworkLinkedList<LobbyPlayerData> Players => default;
    [Networked] public int PlayersVersion { get; set; }
    [Networked] public int SelectedMapIndex { get; set; } = -1;

    public event Action OnPlayersChanged;

    // Static snapshots — survive scene transitions where LobbyManager is despawned
    private static Dictionary<PlayerRef, int> cachedPlayerColors = new Dictionary<PlayerRef, int>();
    private static Dictionary<PlayerRef, string> cachedPlayerNames = new Dictionary<PlayerRef, string>();

    private ChangeDetector changeDetector;
    private int playerJoinCounter;
    private bool needsDeferredSync;

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

            // Restore map selection on return-from-game: fresh LobbyManager defaults to -1,
            // but NRM retained the previous selection on the host's machine. On host migration
            // CopyStateFrom already populated SelectedMapIndex so this no-ops.
            if (SelectedMapIndex < 0 && NetworkRunnerManager.Instance != null
                && NetworkRunnerManager.Instance.SelectedMapIndex >= 0)
            {
                SelectedMapIndex = NetworkRunnerManager.Instance.SelectedMapIndex;
            }

            needsDeferredSync = true;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (!needsDeferredSync) return;
        needsDeferredSync = false;

        // Re-sync one tick after Spawned() to catch any OnPlayerLeft/OnPlayerJoined
        // callbacks that fired while LobbyManager.Instance was null during scene transition
        RemoveStalePlayerEntries();
        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            AddPlayer(player);
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

                // Preserve lobby state (ready, color, name) across host migration by refreshing
                // the Photon snapshot whenever the authoritative Players list mutates.
                if (HasStateAuthority)
                {
                    NetworkRunnerManager.Instance?.PushSnapshot();
                }
            }
            else if (propertyName == nameof(SelectedMapIndex))
            {
                if (HasStateAuthority)
                {
                    NetworkRunnerManager.Instance?.PushSnapshot();
                }
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
            IsReady = false,
            ColorIndex = GetNextAvailableColor(Players.Count % 6, player)
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
    public void RPC_SetPlayerColor(PlayerRef player, int colorIndex)
    {
        if (IsColorTaken(colorIndex, player))
        {
            colorIndex = GetNextAvailableColor(colorIndex + 1, player);
        }

        for (int i = 0; i < Players.Count; i++)
        {
            if (Players.Get(i).Player == player)
            {
                LobbyPlayerData data = Players.Get(i);
                data.ColorIndex = colorIndex;
                Players.Set(i, data);
                PlayersVersion++;
                return;
            }
        }
    }

    public bool IsColorTaken(int colorIndex, PlayerRef excludePlayer)
    {
        foreach (LobbyPlayerData data in Players)
        {
            if (data.Player != excludePlayer && data.ColorIndex == colorIndex)
                return true;
        }
        return false;
    }

    public int GetNextAvailableColor(int startIndex, PlayerRef excludePlayer)
    {
        int colorCount = 6;
        for (int i = 0; i < colorCount; i++)
        {
            int candidate = (startIndex + i) % colorCount;
            if (!IsColorTaken(candidate, excludePlayer))
                return candidate;
        }
        return startIndex;
    }

    public int GetPrevAvailableColor(int startIndex, PlayerRef excludePlayer)
    {
        int colorCount = 6;
        for (int i = 0; i < colorCount; i++)
        {
            int candidate = ((startIndex - i) % colorCount + colorCount) % colorCount;
            if (!IsColorTaken(candidate, excludePlayer))
                return candidate;
        }
        return startIndex;
    }

    public int GetPlayerColorIndex(PlayerRef player)
    {
        foreach (LobbyPlayerData data in Players)
        {
            if (data.Player == player) return data.ColorIndex;
        }
        return 0;
    }

    public static int GetCachedPlayerColorIndex(PlayerRef player)
    {
        if (cachedPlayerColors.TryGetValue(player, out int colorIndex))
            return colorIndex;
        return 0;
    }

    public static string GetCachedPlayerName(PlayerRef player)
    {
        if (cachedPlayerNames.TryGetValue(player, out string name))
            return name;
        return "";
    }

    public void SnapshotPlayerColors()
    {
        cachedPlayerColors.Clear();
        cachedPlayerNames.Clear();
        foreach (LobbyPlayerData data in Players)
        {
            cachedPlayerColors[data.Player] = data.ColorIndex;
            cachedPlayerNames[data.Player] = data.PlayerName.ToString();
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
