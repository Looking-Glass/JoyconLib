using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JoyconDemo : MonoBehaviour {
    private Joycon j;
    private LineRenderer lr;
    private Transform line, sphere;
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
            Vector3 p = j.GetVector();
            Vector3 endpt = new Vector3(-p[1], p[0], -p[2]);
            if (j.GetButtonDown(Joycon.Button.SHOULDER_2))
            {
                j.Recenter();
            }
            lr.SetPosition(0, -2f * endpt);
            lr.SetPosition(1, 2f * endpt);
            sphere.position = 2f * endpt;
        }
        
    }

    private void OnDrawGizmos()
    {
    }

    void OnGUI()
    {
        if (j != null) { GUI.Label(new Rect(0, 0, 200, 100), j.GetDebugText()); };
    }
}
