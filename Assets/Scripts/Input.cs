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
        for (int i = 0; i < 2; ++i) {
            j.poll();
        }
        j.log_to_file("New run", false);
    }

 

    // Update is called once per frame
    void Update() {
       if (j.alive) { j.poll(); };
        if ((j.buttons[0] & 0x80) != 0)
        {
            j.set_zero_accel();
        }
    }
    // FixedUpdate is called before physics are applied each frame
    void FixedUpdate()
    {
        j.update();
        tr.eulerAngles = new Vector3((float)(j.euler[0]), 0, (float)(-j.euler[1]));
    }
}
