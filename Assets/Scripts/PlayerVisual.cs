using UnityEngine;

public class PlayerVisual : MonoBehaviour
{
    [SerializeField] private Player player;
    [SerializeField] private MeshRenderer bodyMeshRenderer;
    [SerializeField] private MeshRenderer headMeshRenderer;
    [SerializeField] private PlayerColorDataSO playerColorDataSO;

    private int lastAppliedColorIndex = -1;

    private void Update()
    {
        if (player == null || playerColorDataSO == null) return;

        int currentIndex = player.ColorIndex;
        if (currentIndex != lastAppliedColorIndex)
        {
            lastAppliedColorIndex = currentIndex;
            Material mat = playerColorDataSO.GetByIndex(currentIndex).bodyMaterial;
            bodyMeshRenderer.material = mat;
            headMeshRenderer.material = mat;
        }
    }
}
