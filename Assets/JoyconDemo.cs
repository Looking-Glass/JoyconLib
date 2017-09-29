using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JoyconDemo : MonoBehaviour {
    private Joycon j;
    private Transform t;
    public int sensor_ind;
	public Vector3 gyr_g;
	public Vector3 acc_g;
    public float amp;
    public bool lock_rumble;
    public float r;
    public float low_freq = 160;
    public float high_freq = 320;
    // Use this for initialization
    void Start ()
    {
        j = JoyconManager.Instance.j;
        t = gameObject.transform;
    }

    // Update is called once per frame
    void Update () {
        if (j != null && j.state > Joycon.state_.ATTACHED)
        {
            if (j.GetButtonDown(Joycon.Button.SHOULDER_2))
            {
                j.Recenter();
            }
            if (j.GetButtonDown(Joycon.Button.SHOULDER_1))
            {
                lock_rumble = !lock_rumble;
            }
            if (j.GetButtonDown(Joycon.Button.DPAD_RIGHT))
            {
                high_freq += 20f;
            }
            if (j.GetButtonDown(Joycon.Button.DPAD_LEFT))
            {
                high_freq -= 20f;
            }
            if (j.GetButtonDown(Joycon.Button.DPAD_UP))
            {
                low_freq += 20f;
            }
            if (j.GetButtonDown(Joycon.Button.DPAD_DOWN))
            {
                low_freq -= 20f;
            }
            if (j.GetButtonDown(Joycon.Button.SL))
            {
                j.SetRumble(160, 320, 0.3f, 100);
            }
            if (!lock_rumble) r = Mathf.Abs(j.GetStick()[1])*0.75f;

            j.SetRumble(low_freq, high_freq, r);
            gyr_g = j.gyr_g;
			acc_g = j.acc_g;

            Vector3 p = j.GetVector(sensor_ind);
            t.eulerAngles = p;

        }
    }

    private void OnDrawGizmos()
    {
    }

    void OnGUI()
    {
        if (sensor_ind == 2 && j != null) { GUI.Label(new Rect(0, 0, 200, 100), j.GetDebugText()); };
    }
}
