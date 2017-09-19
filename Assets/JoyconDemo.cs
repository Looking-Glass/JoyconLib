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
        if (j.GetKeyPressed(Joycon.Button.SHOULDER_2))
        {
            j.Recenter();
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        if (j != null)
        {
            Vector3 p = j.GetPosition();

            Gizmos.DrawLine(Vector3.zero, new Vector3(p[2], p[0], p[1]));
            tr.position = j.GetPosition();

        }
    }

    void OnGUI()
    {
        GUI.Label(new Rect(0, 0, 200, 100),j.GetDebugText());
    }
}
