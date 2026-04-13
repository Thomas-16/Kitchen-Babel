using Fusion;
using Michsky.UI.Heat;
using UnityEngine;

public static class LobbyUIHelpers
{
    public const string ColorPrefsKey = "HorizontalSelector_PlayerColor";

    public static void PopulateMapDropdown(Dropdown mapDropdown, MapListSO mapList)
    {
        if (mapDropdown == null || mapList == null) return;
        mapDropdown.items.Clear();
        mapDropdown.CreateNewItem("Random", false);
        foreach (MapConfigSO map in mapList.maps)
        {
            mapDropdown.CreateNewItem(map.mapName, false);
        }
        mapDropdown.Initialize();
    }

    public static void SyncMapDropdown(Dropdown mapDropdown, int mapIndex)
    {
        if (mapDropdown == null) return;
        int uiIndex = mapIndex < 0 ? 0 : mapIndex + 1;
        if (uiIndex < 0 || uiIndex >= mapDropdown.items.Count) uiIndex = 0;
        if (mapDropdown.selectedItemIndex != uiIndex)
        {
            mapDropdown.SetDropdownIndex(uiIndex);
        }
    }

    // Resolves a local color-selector change against team-scoped uniqueness, fires RPC, and
    // returns the final index the selector should settle on.
    public static int ResolveColorChange(HorizontalSelector colorSelector, int requestedIndex, int previousIndex)
    {
        if (LobbyManager.Instance == null) return requestedIndex;
        NetworkRunner runner = NetworkRunnerManager.Instance != null ? NetworkRunnerManager.Instance.Runner : null;
        if (runner == null) return requestedIndex;

        PlayerRef localPlayer = runner.LocalPlayer;
        int finalIndex = requestedIndex;

        if (LobbyManager.Instance.IsColorTaken(finalIndex, localPlayer))
        {
            int itemCount = colorSelector != null ? colorSelector.items.Count : 6;
            bool forward = previousIndex == -1
                || finalIndex > previousIndex
                || (previousIndex == itemCount - 1 && finalIndex == 0);

            finalIndex = forward
                ? LobbyManager.Instance.GetNextAvailableColor(finalIndex + 1, localPlayer)
                : LobbyManager.Instance.GetPrevAvailableColor(finalIndex - 1, localPlayer);

            if (colorSelector != null)
            {
                colorSelector.index = finalIndex;
                colorSelector.UpdateUI();
            }
        }

        PlayerPrefs.SetInt(ColorPrefsKey, finalIndex);
        LobbyManager.Instance.RPC_SetPlayerColor(localPlayer, finalIndex);
        return finalIndex;
    }
}
