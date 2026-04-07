using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class PlayerColorDataSO : ScriptableObject
{
    [System.Serializable]
    public struct PlayerColorEntry
    {
        public string colorName;
        public Material bodyMaterial;
        public Sprite previewSprite;
        public Color tintColor;
    }

    public List<PlayerColorEntry> colors;

    public PlayerColorEntry GetByIndex(int index)
    {
        return colors[Mathf.Clamp(index, 0, colors.Count - 1)];
    }

    public int Count => colors.Count;
}
