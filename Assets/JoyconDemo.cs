using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JoyconDemo : MonoBehaviour {
    private Joycon j;
    private LineRenderer lr;
    private Transform line, sphere;
    public int sensor_ind;
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
            Vector3 d = j.GetVector(sensor_ind);
            Vector3 p = d;
            //Vector3 p = new Vector3(d.x, 0, 0);
            if (j.GetButtonDown(Joycon.Button.SHOULDER_2))
            {
                j.Recenter();
            }
			if (j.GetButtonDown (Joycon.Button.SHOULDER_1)) {
				j.EnqueueRumble (100);
			}
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
