using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JoyconDemo : MonoBehaviour {
	
    private Joycon j;

    void Start ()
    {
		// get the public Joycon object attached to the JoyconManager in scene
        j = JoyconManager.Instance.j;
		
    }

    // Update is called once per frame
    void Update () {
		// make sure the Joycon only gets checked if attached
        if (j != null && j.state > Joycon.state_.ATTACHED)
        {
			// GetButtonDown checks if a button has been pressed (not held)
            if (j.GetButtonDown(Joycon.Button.SHOULDER_2))
            {
				Debug.Log ("Shoulder button 2 pressed");

				// GetStick returns a 2-element vector with x/y joystick components
				Debug.Log(string.Format("Stick x: {0:N} Stick y: {1:N}",j.GetStick()[0],j.GetStick()[1]));
            
				// Joycon has no magnetometer, so it cannot accurately determine its yaw value. Joycon.Recenter allows the user to reset the yaw value.
				j.Recenter ();
			}
			// GetButtonDown checks if a button has been released
			if (j.GetButtonUp (Joycon.Button.SHOULDER_2))
			{
				Debug.Log ("Shoulder button 2 released");
			}
			// GetButtonDown checks if a button is currently down (pressed or held)
			if (j.GetButton (Joycon.Button.SHOULDER_2))
			{
				Debug.Log ("Shoulder button 2 held");
			}

			if (j.GetButtonDown (Joycon.Button.DPAD_DOWN)) {
				Debug.Log ("Rumble");

				// Rumble for 200 milliseconds, with low frequency rumble at 160 Hz and high frequency rumble at 320 Hz. For more information check:
				// https://github.com/dekuNukem/Nintendo_Switch_Reverse_Engineering/blob/master/rumble_data_table.md

				j.SetRumble (160, 320, 0.6f);

				// The last argument (time) in SetRumble is optional. Call it with three arguments to turn it on without telling it when to turn off.
				// Then call SetRumble(0,0,0) when you want to turn it off.
			}

			// GetVector returns a Vector3 of Joycon pitch, yaw, roll
            Vector3 p = j.GetVector(0);

            gameObject.transform.eulerAngles = p;

        }
    }
}
