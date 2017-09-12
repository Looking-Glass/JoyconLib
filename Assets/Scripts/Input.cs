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
        //tr.Rotate(0.5f, 0.5f, 0.5f);
        if (j.attach() != -1) {
            j.init(0x0);
        }
    }

    // Update is called once per frame
    void Update() {
       if (j.alive) { j.poll(); };
    }
    // FixedUpdate is called before physics are applied each frame
    private void FixedUpdate()
    {

    }
}
