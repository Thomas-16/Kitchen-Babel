using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MADFerret
{
    public class Paint : MonoBehaviour
    {
        public ParticleSystem Dust;
        public Color[] paintColor;
        bool isColliding;
        private void OnTriggerEnter(Collider other)
        {
            if (other.tag == "Box")
            {
                if (isColliding) return;
                isColliding = true;
                Color newColor = paintColor[Random.Range(0, paintColor.Length)];
                other.GetComponent<MeshRenderer>().material.color = newColor;
                Dust.startColor = newColor;
                Dust.Play();
            }
        }
        void Update()
        {
            isColliding = false;
        }
    }
}