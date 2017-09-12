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
        if (j.attach() != -1) {
            j.init(0x0);
        }
        j.poll();
        j.set_zero_accel();
    }

    // Update is called once per frame
    void Update() {
       if (j.alive) { j.poll(); };
        if ((j.buttons[0] & 0x80) != 0)
        {
            j.set_zero_accel();
        }
        tr.eulerAngles = new Vector3((float)(j.euler[0]), (float)(j.euler[1]), 0);
    }
    // FixedUpdate is called before physics are applied each frame
    private void FixedUpdate()
    {

    }
}
