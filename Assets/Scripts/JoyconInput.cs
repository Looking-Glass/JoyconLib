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
        j.Attach(alpha:.1f,leds:0xff,imu:true);
    }

    // Update is called once per frame
    void Update() {
        j.Update();
        tr.eulerAngles = j.GetEulerAngles();
        if (j.GetKeyPressed(Joycon.Button.SHOULDER_2)) j.Recenter();
    }

    void OnApplicationQuit()
    {
        j.Detach();
    }
}
