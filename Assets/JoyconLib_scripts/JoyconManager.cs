using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
public class JoyconManager: MonoBehaviour
{
    public Joycon j;
	public float[] stick;
    static JoyconManager instance;
    public static JoyconManager Instance
    {
       get { return instance; }
    }
    void Awake()
    {
        if (instance != null) Destroy(gameObject);
        instance = this;
        j = new Joycon();
    }
    // Use this for initialization
    void Start()
    {
        j.Attach(alpha: 10f, leds: 0xf, imu: true);
		j.Begin ();
    }

    // Update is called once per frame
    void Update()
    {
        j.Update();
		stick = j.GetStick ();
    }

    void OnApplicationQuit()
    {
        j.Detach();
    }
}
