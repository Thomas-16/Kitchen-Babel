using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MADFerret
{
    public class ConveyorSimple : MonoBehaviour
    {
        public float speed;
        Rigidbody rBody;
        // Start is called before the first frame update
        void Start()
        {
            rBody = GetComponent<Rigidbody>();
        }

        // Update is called once per frame
        void FixedUpdate()
        {
            Vector3 pos = rBody.position;
            rBody.position += transform.right * speed * Time.fixedDeltaTime;
            rBody.MovePosition(pos);
        }
    }
}
