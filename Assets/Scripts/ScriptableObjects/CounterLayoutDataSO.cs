using System;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/CounterLayoutDataSO")]
public class CounterLayoutDataSO : ScriptableObject
{
    [Serializable]
    public struct SpawnPoint
    {
        public Vector3 position;
        public float rotationY;
        public bool isCorner;
    }

    public SpawnPoint[] spawnPoints;
}
