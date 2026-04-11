using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Kitchen Babel/Map List")]
public class MapListSO : ScriptableObject
{
    public List<MapConfigSO> maps;
}
