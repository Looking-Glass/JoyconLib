using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
public class Input : MonoBehaviour {

    private Transform tr;
    Joycon j;

    // Use this for initialization
    void Start() {
        j = new Joycon();
        tr = GetComponent<Transform>();
        j.attach();
        j.enable_imu(true);
        j.init(0);
    }

    // Update is called once per frame
    void Update() {
        if (j.state > Joycon.state_.DROPPED) {
            j.poll();
           
        }
    }
    // FixedUpdate is called before physics are applied each frame
    void FixedUpdate()
    {
        j.update();
        tr.eulerAngles = new Vector3((float)(j.euler[0]), 0, (float)(-j.euler[1]));
    }
}
