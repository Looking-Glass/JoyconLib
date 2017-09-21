using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JoyconDemo : MonoBehaviour {
    private Joycon j;
    private Transform tr;
    // Use this for initialization
    void Start ()
    {
        j = JoyconManager.Instance.j;
        tr = GetComponent<Transform>();
    }

    // Update is called once per frame
    void Update () {
        tr.eulerAngles = j.GetEulerAngles();
        if (j.GetButtonDown(Joycon.Button.SHOULDER_2))
        {
            j.Recenter();
        }
    }
}
