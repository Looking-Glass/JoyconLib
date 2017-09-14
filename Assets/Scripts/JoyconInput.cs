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
        j.attach(alpha:0.05f,leds:0x0,imu:true);
    }

    // Update is called once per frame
    void Update() {
        if (j.state > Joycon.state_.DROPPED) {
            j.poll();
            j.update();
            tr.eulerAngles = new Vector3((float)(j.euler[0]), 0, (float)(-j.euler[1]));
            if (j.pressed[(int)Joycon.Button.DPAD_DOWN]) Debug.Log("HI");
        }
    }
}
