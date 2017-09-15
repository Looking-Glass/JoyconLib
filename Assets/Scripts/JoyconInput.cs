using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
public class JoyconInput : MonoBehaviour {

    private Transform tr;
    Joycon j;

    // Use this for initialization
    void Start() {
        j = new Joycon();
        QualitySettings.vSyncCount = 0;
        tr = GetComponent<Transform>();
        j.Attach(alpha:.05f,leds:0x0,imu:true);
    }

    // Update is called once per frame
    void Update() {
        if (j.state > Joycon.state_.NO_JOYCONS) {
            j.Poll();
            j.Update();
            tr.eulerAngles = new Vector3(-j.euler[1], -j.euler[0], -j.euler[2]);
            //tr.position = new Vector3(j.pos[0], j.pos[1], j.pos[2]);
            if (j.pressed[(int)Joycon.Button.SHOULDER_2]) j.recenter();
        }
    }

    void OnApplicationQuit()
    {
        j.Detach();
    }
}
