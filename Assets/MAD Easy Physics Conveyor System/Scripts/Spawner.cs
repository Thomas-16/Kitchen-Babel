using UnityEngine;
using System.Collections.Generic;

// Starting in 2 seconds.
// a projectile will be launched every 3 seconds

namespace MADFerret
{
    public class Spawner : MonoBehaviour
    {
        public Rigidbody[] Box;
        public int BoxNo;

        void Start()
        {
            InvokeRepeating("SpawnBox", 2.0f, 3f);
        }

        void SpawnBox()
        {
            BoxNo = Random.Range(0, Box.Length);
            Instantiate(Box[BoxNo], transform.position, transform.rotation);
        }
    }
}