using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JoyconDemo : MonoBehaviour {
    private Joycon j;
    private LineRenderer lr;
    private Transform line, sphere;
    public int sensor_ind;
	public Vector3 gyr_g;
	public Vector3 acc_g;
    public float amp;
    public bool lock_rumble;
    public float r;
    public int t;
    public float low_freq = 160;
    public float high_freq = 320;
    // Use this for initialization
    void Start ()
    {
        j = JoyconManager.Instance.j;
        line = gameObject.transform.GetChild(0);
        sphere = gameObject.transform.GetChild(1);
        lr = line.GetComponent<LineRenderer>();
        float alpha = 1.0f;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.blue, 0.0f), new GradientColorKey(Color.red, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(alpha, 0.0f), new GradientAlphaKey(alpha, 1.0f) }
            );
        lr.colorGradient = gradient;
        lr.positionCount = 2;
    }

    // Update is called once per frame
    void Update () {
        if (j != null && j.state > Joycon.state_.ATTACHED)
        {
            Vector3 p = j.GetVector(sensor_ind);
			p = Quaternion.Euler (new Vector3(-90, 0, 90)) * p;
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
            if (!lock_rumble) r = Mathf.Abs(j.GetStick()[1])*0.75f;

            j.SetRumble(low_freq, high_freq, r);
            gyr_g = j.gyr_g;
			acc_g = j.acc_g;
            lr.SetPosition(0, -1f * p);
            lr.SetPosition(1, p);
            sphere.position = transform.position + p;
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
