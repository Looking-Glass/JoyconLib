#define DEBUG

using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;
using UnityEngine;

public class Joycon
{
    public bool isleft;
    public enum state_ : uint {
        NOT_ATTACHED,
        DROPPED,
        NO_JOYCONS,
        ATTACHED,
        INPUT_MODE_0x30,
        IMU_DATA_OK,
    };
    public struct buttons_
    {
        public bool DPAD_DOWN;
        public bool DPAD_RIGHT;
        public bool DPAD_LEFT;
        public bool DPAD_UP;
        public bool SL;
        public bool SR;
        public bool MINUS;
        public bool HOME;
        public bool PLUS;
        public bool CAPTURE;
        public bool STICK;
        public bool SHOULDER_1;
        public bool SHOULDER_2;
    };
    public buttons_ buttons;
    public state_ state;

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

    private const byte RANGE_G = 8;
    private Int16[] acc_r = { 0, 0, 0 };
    private float[] acc_f = { 0, 0, 0 };
    private float[] acc_g = { 0, 0, 0 };
    private float[] acc_z = { 0, 0, 0 };

    private bool imu_enabled = false;
    private Int16[] gyr = { 0, 0, 0 };
    public double[] euler = { 0, 0, 0 };
    private const float alpha = 1f;

    private const uint report_len = 49;
    private byte[] report_buf;
    private byte global_count = 0;
    private uint attempts = 0;

    public int attach()
    {
        state = state_.NOT_ATTACHED;
        report_buf = new byte[report_len];
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
        return 0;
    }
    public void log_to_file(string s, bool append = true)
    {
        using (System.IO.StreamWriter file =
        new System.IO.StreamWriter(@"C:\Users\LKG\Desktop\data_dump.txt", append))
        {
            file.WriteLine(s);
        }
    }
    public void init(byte leds)
    {
        byte[] a = { 0x0 };
        // Input report mode
        subcommand(0x3, new byte[] { 0x3f }, 1, false);
        a[0] = 0x1;
        dump_calibration_data();
        // Connect
        a[0] = 0x01;
        subcommand(0x1, a, 1);
        a[0] = 0x02;
        subcommand(0x1, a, 1);
        a[0] = 0x03;
        subcommand(0x1, a, 1);
        a[0] = leds;
        subcommand(0x30, a, 1);
    }

    public uint poll()
    {
        if (state < state_.INPUT_MODE_0x30)
        {
            report_buf = subcommand(0x1, new byte[] { 0x0 }, 0);
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
            subcommand(0x3, new byte[] { 0x30 }, 1, true);
            state = state_.INPUT_MODE_0x30;
            if (attempts < 30) return attempts;
            state = state_.DROPPED;
            Debug.Log("Connection lost. Is the Joy-Con connected?");
            return attempts;
        }
        attempts = 0;
        int i = 0;
        if (!imu_enabled) {
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
            subcommand(0x40, new byte[] {0x1}, 1, true);
            return attempts;
        }
        if (state != state_.IMU_DATA_OK)
        {
            state = state_.IMU_DATA_OK;
            Debug.Log("IMU data received.");
        }
        return attempts;
    }
    public void enable_imu(bool a)
    {
        imu_enabled = a;
    }
    public int update()
    {
        if (report_buf[0] == 0x00) return -1;

        stick_raw[0] = report_buf[6 + (isleft ? 0 : 3)];
        stick_raw[1] = report_buf[7 + (isleft ? 0 : 3)];
        stick_raw[2] = report_buf[8 + (isleft ? 0 : 3)];

        stick_precal[0] = (UInt16)(stick_raw[0] | ((stick_raw[1] & 0xf) << 8));
        stick_precal[1] = (UInt16)((stick_raw[1] >> 4) | (stick_raw[2] << 4));
        stick = center_sticks(stick_precal);

        buttons.DPAD_DOWN = (report_buf[3 + (isleft ? 2 : 0)] & (isleft ? 0x01 : 0x04)) != 0;
        buttons.DPAD_RIGHT= (report_buf[3 + (isleft ? 2 : 0)] & (isleft ? 0x04 : 0x08)) != 0;
        buttons.DPAD_UP = (report_buf[3 + (isleft ? 2 : 0)] & (isleft ? 0x02 : 0x02)) != 0;
        buttons.DPAD_LEFT = (report_buf[3 + (isleft ? 2 : 0)] & (isleft ? 0x08 : 0x01)) != 0;
        buttons.HOME = ((report_buf[4] & 0x10) != 0);
        buttons.MINUS = ((report_buf[4] & 0x01) != 0);
        buttons.PLUS = ((report_buf[4] & 0x02) != 0);
        buttons.STICK = ((report_buf[4] & (isleft ? 0x08 : 0x04)) != 0);
        buttons.SHOULDER_2 = (report_buf[3 + (isleft ? 2 : 0)] & 0x80) != 0;
        buttons.SHOULDER_1 = (report_buf[3 + (isleft ? 2 : 0)] & 0x40) != 0;
        buttons.SHOULDER_2 = (report_buf[3 + (isleft ? 2 : 0)] & 0x80) != 0;
        buttons.SR = (report_buf[3 + (isleft ? 2 : 0)] & 0x10) != 0;
        buttons.SL = (report_buf[3 + (isleft ? 2 : 0)] & 0x20) != 0;

        if (!imu_enabled | state < state_.IMU_DATA_OK) return -1;
        // read raw IMU values
        uint n = 0;

        gyr[0] = (Int16)(report_buf[19 + n * 12] | ((report_buf[20 + n * 12] << 8) & 0xff00));
        gyr[1] = (Int16)(report_buf[21 + n * 12] | ((report_buf[22 + n * 12] << 8) & 0xff00));
        gyr[2] = (Int16)(report_buf[23 + n * 12] | ((report_buf[24 + n * 12] << 8) & 0xff00));

        acc_r[0] = (Int16)(report_buf[13 + n * 12] | ((report_buf[14 + n * 12] << 8) & 0xff00));
        acc_r[1] = (Int16)(report_buf[15 + n * 12] | ((report_buf[16 + n * 12] << 8) & 0xff00));
        acc_r[2] = (Int16)(report_buf[17 + n * 12] | ((report_buf[18 + n * 12] << 8) & 0xff00));

        stick_precal[0] = (UInt16)(stick_raw[0] | ((stick_raw[1] & 0xf) << 8));
        stick_precal[1] = (UInt16)((stick_raw[1] >> 4) | (stick_raw[2] << 4));
        stick = center_sticks(stick_precal);

        if (report_buf[0] != 0x30) return -1; // no gyro data

        // accelerometer ranging data:
        // +/- 2g : 0.061 mg/LSB
        // +/- 4g : 0.122 mg/LSB
        // +/- 6g : 0.244 mg/LSB
        // +/- 8g : 0.488 mg/LSB
        // assume +/- 2g for now

        for (int i = 0; i < 3; ++i)
        {
            acc_g[i] = acc_r[i] * 0.061f * (RANGE_G >> 1) / 1000f;
            acc_f[i] = (acc_g[i] - acc_z[i]) * alpha + (acc_f[i] * (1f - alpha));
        }

        // log_to_file(acc_r[0] + "," + acc_r[1] + "," + acc_r[2]);
        //log_to_file(acc_g[0] + " " + acc_g[1] + " " + acc_g[2]);

        //https://theccontinuum.com/2012/09/24/arduino-imu-pitch-roll-from-accelerometer/
        euler[0] = (Math.Atan2(-acc_f[1], acc_f[2]) * 180f) / Math.PI;
        euler[1] = (Math.Atan2(acc_f[0], Math.Sqrt(acc_f[1] * acc_f[1] + acc_f[2] * acc_f[2])) * 180f) / Math.PI;
        //http://www.instructables.com/id/Accelerometer-Gyro-Tutorial/
        return 0;
    }
    public void set_zero_accel()
    {
        acc_z[0] = acc_g[0];
        acc_z[1] = acc_g[1];
        acc_z[2] = acc_g[2];
    }

    private Int16[] center_sticks(UInt16[] vals)
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

    private byte[] subcommand(byte sc, byte[] buf, uint len, bool print = false)
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
        if (print) { printarray(buf_, len, 11, "Subcommand 0x"+string.Format("{0:X2}",sc)+" sent. Data: 0x{0:S}"); };
#endif
        HIDapi.hid_write(handle, buf_, new UIntPtr(len + 11));
        int res = HIDapi.hid_read_timeout(handle, response, new UIntPtr(report_len), 50);
#if DEBUG
        if (res < 1) Debug.Log("No response.");
        else if (print) { printarray(response, report_len-1, 1, "Response ID 0x" + string.Format("{0:X2}", response[0]) + ". Data: 0x{0:S}"); }
#endif
        return response;
    }
    private void dump_calibration_data()
    {
        byte[] buf_ = spi_read(0x80, (isleft ? (byte)0x12 : (byte)0x1d), 9); // get user calibration data if possible
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
            buf_ = spi_read(0x60, (isleft ? (byte)0x3d : (byte)0x46), 9); // get user calibration data if possible
        }
        stick_cal[0] = (UInt16)((buf_[1] << 8) & 0xF00 | buf_[0]);
        stick_cal[1] = (UInt16)((buf_[2] << 4) | (buf_[1] >> 4));
        stick_cal[2] = (UInt16)((buf_[4] << 8) & 0xF00 | buf_[3]);
        stick_cal[3] = (UInt16)((buf_[5] << 4) | (buf_[4] >> 4));
        stick_cal[4] = (UInt16)((buf_[7] << 8) & 0xF00 | buf_[6]);
        stick_cal[5] = (UInt16)((buf_[8] << 4) | (buf_[7] >> 4));
    }
    private byte[] spi_read(byte addr1, byte addr2, uint len, bool print=false)
    {
        byte[] buf = { addr2, addr1, 0x00, 0x00, (byte)len };
        byte[] ret = new byte[len];
        byte[] buf_ = new byte[len + 20];

        for (int i = 0; i < 100; ++i)
        {
            buf_ = subcommand(0x10, buf, 5, false);
            if (buf_[15] == addr2 && buf_[16] == addr1)
            {
                break;
            }
        }
        Array.Copy(buf_, 20, ret, 0, len);
#if DEBUG
        if (print) printarray(ret, len);
#endif
        return ret;
    }
    private void printarray<T>(T[] arr, uint len=0, uint start = 0, string format = "{0:S}")
    {
        if (len == 0) len = (uint)arr.Length;
        string tostr = "";
        for (int i = 0; i < len; ++i)
        {
            tostr += string.Format((arr[0] is byte) ? "{0:X2} " : ((arr[0] is float) ? "{0:F} " : "{0:D} "), arr[i + start]);
        }
        Debug.Log(string.Format(format, tostr));
    }
    private void printarray(UInt16[] arr, uint len)
    {
        string tostr = "";
        for (int i = 0; i < len; ++i)
        {
            tostr += string.Format("{0:D} ", arr[i]);
        }
        Debug.Log(tostr);
    }
}
