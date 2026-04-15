using UnityEngine;

[CreateAssetMenu(fileName = "VersusBalanceConfigSO", menuName = "ScriptableObjects/VersusBalanceConfigSO")]
public class VersusBalanceConfigSO : ScriptableObject
{
    [System.Serializable]
    public struct Tier
    {
        [Tooltip("Applies when the larger team has at least this many players.")]
        public int minPlayersPerTeam;
        [Tooltip("Seconds between natural order spawns for both teams.")]
        public float naturalOrderIntervalSeconds;
        [Tooltip("Soft cap on concurrent waiting orders per team. Clamped to VersusMatchManager.OrderCapacity at runtime.")]
        [Range(1, 6)] public int maxWaitingOrdersPerTeam;
    }

    [Tooltip("Tiers evaluated in array order. The last tier whose minPlayersPerTeam <= larger-team-size wins. Put smallest minPlayersPerTeam first.")]
    public Tier[] tiers;

    [Tooltip("Used when tiers is empty or no tier matches.")]
    public Tier fallback = new Tier
    {
        minPlayersPerTeam = 1,
        naturalOrderIntervalSeconds = 15f,
        maxWaitingOrdersPerTeam = 6,
    };
}
