using Fusion;

public struct LobbyPlayerData : INetworkStruct
{
    public PlayerRef Player;
    public NetworkString<_32> PlayerName;
    public NetworkBool IsReady;
}
