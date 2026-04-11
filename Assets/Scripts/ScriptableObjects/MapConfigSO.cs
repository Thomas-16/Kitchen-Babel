using UnityEngine;

[CreateAssetMenu(menuName = "Kitchen Babel/Map Config")]
public class MapConfigSO : ScriptableObject
{
    public string mapName;
    public GameObject mapPrefab;
    public Vector3 cameraPosition;
    public Vector3 cameraRotation;
    public float cameraFOV = 20f;
}
