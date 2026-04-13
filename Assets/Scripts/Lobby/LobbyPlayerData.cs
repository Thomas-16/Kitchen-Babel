using Fusion;

public struct LobbyPlayerData : INetworkStruct
{
    public PlayerRef Player;
    public NetworkString<_32> PlayerName;
    public NetworkBool IsReady;
    public int ColorIndex;
    // -1 = unassigned (co-op). 0 = Team A, 1 = Team B in versus.
    public int TeamIndex;
}
