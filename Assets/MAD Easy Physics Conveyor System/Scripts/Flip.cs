using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MADFerret
{
    public class Flip : MonoBehaviour
    {
        public string thisKey;
        public bool Left;
        public Quaternion from;
        public Quaternion to;
        public float speed;
        public Text text;
        public float timeCount;
        // Start is called before the first frame update
        void Start()
        {
            text.text = thisKey;
        }

        // Update is called once per frame
        void Update()
        {

            if (Input.GetKeyDown(thisKey))
            {
                if (Left == true) Left = false;
                else { Left = true; }
                timeCount = 0;
            }

            if (Left == true)
            {
                transform.rotation = Quaternion.Lerp(from, to, timeCount * speed);
                timeCount = timeCount + Time.deltaTime;
            }
            if (Left == false)
            {
                transform.rotation = Quaternion.Lerp(to, from, timeCount * speed);
                timeCount = timeCount + Time.deltaTime;
            }
        }
    }
}
