#define DEBUG

using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;
using UnityEngine;

public class Joycon
{
    public bool isleft;
    public enum state_ : uint
    {
        NOT_ATTACHED,
        DROPPED,
        NO_JOYCONS,
        ATTACHED,
        INPUT_MODE_0x30,
        IMU_DATA_OK,
    };
    public state_ state;

    public enum Button : int
    {
        DPAD_DOWN = 0,
        DPAD_RIGHT = 1,
        DPAD_LEFT = 2,
        DPAD_UP = 3,
        SL = 4,
        SR = 5,
        MINUS = 6,
        HOME = 7,
        PLUS = 8,
        CAPTURE = 9,
        STICK = 10,
        SHOULDER_1 = 11,
        SHOULDER_2 = 12
    };

    public bool[] pressed = new bool[13];
    public bool[] down = new bool[13];

    public Int16[] stick = { 0, 0 };

    private IntPtr handle;

    // Different operating systems either do or don't like the trailing zero
    private const ushort vendor_id = 0x57e;
    private const ushort vendor_id_ = 0x057e;

    private const ushort product_l = 0x2006;
    private const ushort product_r = 0x2007;

    private byte[] default_buf = { 0x1, 0x0, 0x0, 0x1, 0x40, 0x40, 0x0, 0x1, 0x40, 0x40 };

    private byte[] stick_raw = { 0, 0, 0 };
    private UInt16[] stick_cal = { 0, 0, 0, 0, 0, 0 };
    private UInt16[] stick_precal = { 0, 0 };

    private bool imu_enabled = false;
    private int GYRO_RANGE_G = 2000;
    private int GYRO_RANGE_DIV;
    private const byte ACCEL_RANGE_G = 8;
    private Int16[] acc_r = { 0, 0, 0 };
    private float[] acc_g = { 0, 0, 0 };
    private Int16[] gyr_r = { 0, 0, 0 };
    private float[] gyr_g = { 0, 0, 0 };
    private float[] gyr_a = { 0, 0, 0 };

    private float[] est_g = { 0, 0, 0 };
    public float[] pos = { 0, 0, 0 };
    public float[] euler = { 0, 0, 0 };
    private float filterweight;

    private const uint report_len = 49;
    private byte[] report_buf = new byte[report_len];
    private byte global_count = 0;
    private uint attempts = 0;

    public Joycon()
    {
        GYRO_RANGE_DIV = (GYRO_RANGE_G == 245) ? (2) : (GYRO_RANGE_G / 125);
    }
    public bool GetKeyPressed(Button key)
    {
        return pressed[(int)key];
    }
    public bool GetKeyDown(Button key)
    {
        return down[(int)key];
    }
    public Int16[] GetStick()
    {
        return stick;
    }
    public Vector3 GetEulerAngles()
    {
        return new Vector3(-euler[1], -euler[0], -euler[2]);
    }
    public int Attach(byte leds = 0x0, bool imu=true, float alpha = 1f)
    {
        filterweight = alpha;
        state = state_.NOT_ATTACHED;
        HIDapi.hid_init();
        IntPtr ptr = HIDapi.hid_enumerate(vendor_id, 0x0);
        if (ptr == IntPtr.Zero)
        {
            ptr = HIDapi.hid_enumerate(vendor_id_, 0x0);
            if (ptr == IntPtr.Zero)
            {
                HIDapi.hid_free_enumeration(ptr);
                Debug.Log("No Joy-Cons found.");
                state = state_.NO_JOYCONS;
                return -1;
            }
        }
        hid_device_info enumerate = (hid_device_info)Marshal.PtrToStructure(ptr, typeof(hid_device_info));
        if (enumerate.product_id == product_l)
        {
            isleft = true;
            Debug.Log("Left Joy-Con connected.");
        }
        else if (enumerate.product_id == product_r)
        {
            Debug.Log("Right Joy-Con connected.");
        }
        else
        {
            HIDapi.hid_free_enumeration(ptr);
            Debug.Log("No Joy-Cons found.");
            state = state_.NO_JOYCONS;
            return -1;
        }
        handle = HIDapi.hid_open_path(enumerate.path);
        HIDapi.hid_set_nonblocking(handle, 1);
        HIDapi.hid_free_enumeration(ptr);
        state = state_.ATTACHED;
        byte[] a = { 0x0 };
        // Input report mode
        Subcommand(0x3, new byte[] { 0x3f }, 1, false);
        a[0] = 0x1;
        dump_calibration_data();
        // Connect
        a[0] = 0x01;
        Subcommand(0x1, a, 1);
        a[0] = 0x02;
        Subcommand(0x1, a, 1);
        a[0] = 0x03;
        Subcommand(0x1, a, 1);
        a[0] = leds;
        Subcommand(0x30, a, 1);
        imu_enabled = imu;
        Subcommand(0x40, new byte[] { 0x1 }, 1, true);
        Debug.Log("Done with init.");
        return 0;
    }
    public void Detach()
    {
        PrintArray(max, format: "max {0:S}");
		Debug.Log ("Sum: " + sum);
        if (state > state_.NO_JOYCONS)
        {
            Subcommand(0x30, new byte[] { 0x0 }, 1);
            Subcommand(0x40, new byte[] { 0x0 }, 1);
            Subcommand(0x3, new byte[] { 0x3f }, 1);
        }
        if (state > state_.DROPPED)
        {
            HIDapi.hid_close(handle);
        }
        state = state_.NOT_ATTACHED;
    }
    public uint Poll()
    {
        if (state < state_.INPUT_MODE_0x30)
        {
            report_buf = Subcommand(0x1, new byte[] { 0x0 }, 0);
        }
        else
        {
            HIDapi.hid_read(handle, report_buf, new UIntPtr(report_len));
        }

        if (report_buf[0] != 0x30)
        {
            ++attempts;
            bool change = state < state_.INPUT_MODE_0x30 | attempts > 2;
            if (change | state >= state_.INPUT_MODE_0x30) Debug.Log(string.Format("No IMU data received. Attempt: {0:D}, packet ID: 0x{1:X2}." + (change ? " Changing input mode to 0x30." : ""), attempts, report_buf[0]));
            if (!change) return attempts;
            state = state_.ATTACHED;
            Subcommand(0x3, new byte[] { 0x30 }, 1, true);
            state = state_.INPUT_MODE_0x30;
            if (attempts < 30) return attempts;
            state = state_.DROPPED;
            Debug.Log("Connection lost. Is the Joy-Con connected?");
            return attempts;
        }
        attempts = 0;
        int i = 0;
        if (!imu_enabled)
        {
            state = state_.INPUT_MODE_0x30;
            return attempts;
        }
        while (i < 36)
        {
            if (report_buf[i + 13] != 0x00) break;
            ++i;
        }
        if (i == 36)
        {
            state = state_.INPUT_MODE_0x30;
            Debug.Log("Report ID 0x30 received, but no IMU data. Enabling IMU.");
            Subcommand(0x40, new byte[] { 0x1 }, 1, true);
            return attempts;
        }
        if (state != state_.IMU_DATA_OK)
        {
            state = state_.IMU_DATA_OK;
            Debug.Log("IMU data received.");
        }
        return attempts;
    }
	float[] max = { 0, 0, 0 };
	float sum = 0;
    public void Update()
    {
        if (state > state_.NO_JOYCONS)
        {
            Poll();
            ProcessPacket();
        }
    }
    private int ProcessPacket()
    {   
        if (report_buf[0] == 0x00) return -1;

        stick_raw[0] = report_buf[6 + (isleft ? 0 : 3)];
        stick_raw[1] = report_buf[7 + (isleft ? 0 : 3)];
        stick_raw[2] = report_buf[8 + (isleft ? 0 : 3)];

        stick_precal[0] = (UInt16)(stick_raw[0] | ((stick_raw[1] & 0xf) << 8));
        stick_precal[1] = (UInt16)((stick_raw[1] >> 4) | (stick_raw[2] << 4));
        stick = CenterSticks(stick_precal);

        for (int i = 0; i < down.Length; ++i)
        {
            pressed[i] = down[i];
        }

        down[(int)Button.DPAD_DOWN] = (report_buf[3 + (isleft ? 2 : 0)] & (isleft ? 0x01 : 0x04)) != 0;
        down[(int)Button.DPAD_RIGHT] = (report_buf[3 + (isleft ? 2 : 0)] & (isleft ? 0x04 : 0x08)) != 0;
        down[(int)Button.DPAD_UP] = (report_buf[3 + (isleft ? 2 : 0)] & (isleft ? 0x02 : 0x02)) != 0;
        down[(int)Button.DPAD_LEFT] = (report_buf[3 + (isleft ? 2 : 0)] & (isleft ? 0x08 : 0x01)) != 0;
        down[(int)Button.HOME] = ((report_buf[4] & 0x10) != 0);
        down[(int)Button.MINUS] = ((report_buf[4] & 0x01) != 0);
        down[(int)Button.PLUS] = ((report_buf[4] & 0x02) != 0);
        down[(int)Button.STICK] = ((report_buf[4] & (isleft ? 0x08 : 0x04)) != 0);
        down[(int)Button.SHOULDER_2] = (report_buf[3 + (isleft ? 2 : 0)] & 0x80) != 0;
        down[(int)Button.SHOULDER_1] = (report_buf[3 + (isleft ? 2 : 0)] & 0x40) != 0;
        down[(int)Button.SHOULDER_2] = (report_buf[3 + (isleft ? 2 : 0)] & 0x80) != 0;
        down[(int)Button.SR] = (report_buf[3 + (isleft ? 2 : 0)] & 0x10) != 0;
        down[(int)Button.SL] = (report_buf[3 + (isleft ? 2 : 0)] & 0x20) != 0;

        for (int i = 0; i < down.Length; ++i) { 
            pressed[i] = !pressed[i] & down[i];
        }

        if (!imu_enabled | state < state_.IMU_DATA_OK)
            return -1;
        // read raw IMU values
        uint n = 0;

        gyr_r[0] = (Int16)(report_buf[19 + n * 12] | ((report_buf[20 + n * 12] << 8) & 0xff00));
        gyr_r[1] = (Int16)(report_buf[21 + n * 12] | ((report_buf[22 + n * 12] << 8) & 0xff00));
        gyr_r[2] = (Int16)(report_buf[23 + n * 12] | ((report_buf[24 + n * 12] << 8) & 0xff00));

        acc_r[0] = (Int16)(report_buf[13 + n * 12] | ((report_buf[14 + n * 12] << 8) & 0xff00));
        acc_r[1] = (Int16)(report_buf[15 + n * 12] | ((report_buf[16 + n * 12] << 8) & 0xff00));
        acc_r[2] = (Int16)(report_buf[17 + n * 12] | ((report_buf[18 + n * 12] << 8) & 0xff00));

        // http://www.starlino.com/imu_guide.html

        if (report_buf[0] != 0x30) return -1; // no gyro data

        // accelerometer ranging data:
        // +/- 2g : 0.061 mg/LSB
        // +/- 4g : 0.122 mg/LSB
        // +/- 6g : 0.244 mg/LSB
        // +/- 8g : 0.488 mg/LSB
        // +/- 8g is likely correct

		for (int i = 0; i < 3; ++i)
        {
			gyr_r[i] = (Int16)(gyr_r[i] * ((isleft & i>0) ? -1 : 1));
            acc_g[i] = acc_r[i] * 0.061f * (ACCEL_RANGE_G >> 1) / 1000f;
            gyr_g[i] = gyr_r[i] * 4.375f * GYRO_RANGE_DIV / 1000f;
			gyr_a [i] = (gyr_a [i] + gyr_g [i]) / 2;
			if (Math.Abs(acc_g [i]) > Math.Abs(max [i]))
				max [i] = acc_g [i];
        }
        float acc_mag = (float)Math.Sqrt(acc_g[0] * acc_g[0] + acc_g[1] * acc_g[1] + acc_g[2] * acc_g[2]);
        for (int i = 0; i < 3; ++i)
        {
            acc_g[i] = acc_g[i] / acc_mag;
        }
        if (pos[0] == 0 & pos[1] == 0 & pos[2] == 0)
        {
            pos[0] = acc_g[0];
            pos[1] = acc_g[1];
            pos[2] = acc_g[2];
            euler[0] = (float)Math.Atan2(pos[1], pos[2]);
            euler[1] = (float)Math.Atan2(pos[2], pos[0]);
            euler[2] = (float)Math.Atan2(pos[0], pos[1]);
        }
        else
        {
            euler[0] = euler[0] + gyr_a[0] * Time.deltaTime;
            euler[1] = euler[1] + gyr_a[1] * Time.deltaTime;
            euler[2] = euler[2] + gyr_a[2] * Time.deltaTime;
			sum += gyr_g [2] * Time.deltaTime;
            est_g[0] = (float)(Math.Sin(euler[1]) / Math.Sqrt(1 + Math.Pow(Math.Cos(euler[1]), 2) * Math.Pow(Math.Tan(euler[0]), 2)));
            est_g[1] = (float)(Math.Sin(euler[0]) / Math.Sqrt(1 + Math.Pow(Math.Cos(euler[0]), 2) * Math.Pow(Math.Tan(euler[1]), 2)));
            est_g[2] = (float)(Math.Sign(est_g[2]) * Math.Sqrt(1 - est_g[1]*est_g[1] - est_g[2]*est_g[2]));
			for (int i = 0; i < 2; ++i)
				pos [i] = (acc_g[i] + est_g[i] * filterweight) / (1 + filterweight);
            float est_mag = (float)Math.Sqrt(pos[0] * pos[0] + pos[1] * pos[1] + pos[2] * pos[2]);
            pos[0] /= est_mag;
            pos[1] /= est_mag;
            pos[2] /= est_mag;
        }
        return 0;
    }
    public void Recenter()
    {
        euler[0] = 0;
        euler[1] = 0;
        euler[2] = 0;
        pos[0] = 0;
        pos[1] = 0;
        pos[2] = 0;
    }
    private Int16[] CenterSticks(UInt16[] vals)
    {
        Int16[] s = { 0, 0 };

        for (uint i = 0; i < 2; ++i)
        {
            if (vals[i] > stick_cal[2 + i])
            {
                s[i] = (Int16)((vals[i] - stick_cal[2 + i]) * -1.0f / stick_cal[i] * 32768);
            }
            else
            {
                s[i] = (Int16)((vals[i] - stick_cal[2 + i]) * 1.0f / stick_cal[4 + i] * 32768);
            }
        }
        return s;
    }
    private byte[] Subcommand(byte sc, byte[] buf, uint len, bool print = false)
    {
        byte[] buf_ = new byte[report_len];
        byte[] response = new byte[report_len];
        Array.Copy(default_buf, buf_, 10);
        Array.Copy(buf, 0, buf_, 11, len);
        buf_[10] = sc;
        buf_[1] = global_count;
        ++global_count;
        if (global_count >= 0xf) global_count -= 0xf;
#if DEBUG
        if (print) { PrintArray(buf_, len, 11, "Subcommand 0x"+string.Format("{0:X2}",sc)+" sent. Data: 0x{0:S}"); };
#endif
        HIDapi.hid_write(handle, buf_, new UIntPtr(len + 11));
        int res = HIDapi.hid_read_timeout(handle, response, new UIntPtr(report_len), 50);
#if DEBUG
        if (res < 1) Debug.Log("No response.");
        else if (print) { PrintArray(response, report_len-1, 1, "Response ID 0x" + string.Format("{0:X2}", response[0]) + ". Data: 0x{0:S}"); }
#endif
        return response;
    }
    private void dump_calibration_data()
    {
        byte[] buf_ = ReadSPI(0x80, (isleft ? (byte)0x12 : (byte)0x1d), 9); // get user calibration data if possible
        bool found = false;
        for (int i = 0; i < 9; ++i)
        {
            if (buf_[i] != 0xff)
            {
                Debug.Log("Using user stick calibration data.");
                found = true;
                break;
            }
        }
        if (!found)
        {
            Debug.Log("Using factory stick calibration data.");
            buf_ = ReadSPI(0x60, (isleft ? (byte)0x3d : (byte)0x46), 9); // get user calibration data if possible
        }
        stick_cal[0] = (UInt16)((buf_[1] << 8) & 0xF00 | buf_[0]);
        stick_cal[1] = (UInt16)((buf_[2] << 4) | (buf_[1] >> 4));
        stick_cal[2] = (UInt16)((buf_[4] << 8) & 0xF00 | buf_[3]);
        stick_cal[3] = (UInt16)((buf_[5] << 4) | (buf_[4] >> 4));
        stick_cal[4] = (UInt16)((buf_[7] << 8) & 0xF00 | buf_[6]);
        stick_cal[5] = (UInt16)((buf_[8] << 4) | (buf_[7] >> 4));
    }
    private byte[] ReadSPI(byte addr1, byte addr2, uint len, bool print=false)
    {
        byte[] buf = { addr2, addr1, 0x00, 0x00, (byte)len };
        byte[] ret = new byte[len];
        byte[] buf_ = new byte[len + 20];

        for (int i = 0; i < 100; ++i)
        {
            buf_ = Subcommand(0x10, buf, 5, false);
            if (buf_[15] == addr2 && buf_[16] == addr1)
            {
                break;
            }
        }
        Array.Copy(buf_, 20, ret, 0, len);
#if DEBUG
        if (print) PrintArray(ret, len);
#endif
        return ret;
    }
    private void PrintArray<T>(T[] arr, uint len=0, uint start = 0, string format = "{0:S}")
    {
        if (len == 0) len = (uint)arr.Length;
        string tostr = "";
        for (int i = 0; i < len; ++i)
        {
            tostr += string.Format((arr[0] is byte) ? "{0:X2} " : ((arr[0] is float) ? "{0:F} " : "{0:D} "), arr[i + start]);
        }
        Debug.Log(string.Format(format, tostr));
    }
}
