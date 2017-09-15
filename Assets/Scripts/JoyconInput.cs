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
        j.Attach(alpha:0.05f,leds:0x0,imu:true);
    }

    // Update is called once per frame
    void Update() {
        if (j.state > Joycon.state_.NO_JOYCONS) {
            j.Poll();
            j.Update();
            tr.eulerAngles = new Vector3((float)(j.euler[0]), 0, (float)(-j.euler[1]));
            if (j.pressed[(int)Joycon.Button.DPAD_DOWN]) Debug.Log("HI");
        }
    }

    void OnApplicationQuit()
    {
        j.Detach();
    }
}
