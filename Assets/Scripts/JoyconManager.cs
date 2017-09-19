using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
public class JoyconManager: MonoBehaviour
{
    public Joycon j;

    static JoyconManager instance;
    public static JoyconManager Instance
    {
        get
        {
            if (instance != null) return instance;
            instance = FindObjectOfType<JoyconManager>();
            return instance;
        }
        private set { instance = value; }
    }
    void Awake()
    {
        instance = this;
    }
    // Use this for initialization
    void Start()
    {
        j = new Joycon();
        j.Attach(alpha: 1f, leds: 0xf, imu: true);
		j.Begin ();
    }

    // Update is called once per frame
    void Update()
    {
        j.Update();
    }

    void OnApplicationQuit()
    {
        j.Detach();
    }
}
