using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MADFerret
{
    public class Delete : MonoBehaviour
    {
        public ParticleSystem Dust;
        private void OnTriggerEnter(Collider other)
        {
            if (other.tag == "Box")
            {
                Destroy(other.gameObject);
                Dust.Play();
            }
        }
    }
}
