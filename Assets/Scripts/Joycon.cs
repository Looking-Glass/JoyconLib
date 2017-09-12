#define DEBUG

using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;
using UnityEngine;

public class Joycon
{

    public bool isleft;
    public bool alive = false;

    public byte[] buttons = { 0x00, 0x00 };
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

    private UInt16[] acc = { 0, 0, 0 };
    private double[] acc_f = { 0, 0, 0 };
    private UInt16[] acc_g = { 0, 0, 0 };

    private Int16[] gyr = { 0, 0, 0 };
    public double[] euler = { 0, 0, 0 };
    private const double alpha = 0.5;

    private byte global_count = 0;
    private const uint report_len = 48;



    public int attach()
    {
        HIDapi.hid_init();
        IntPtr ptr = HIDapi.hid_enumerate(vendor_id, 0x0);
        if (ptr == IntPtr.Zero)
        {
            ptr = HIDapi.hid_enumerate(vendor_id_, 0x0);
            if (ptr == IntPtr.Zero)
            {
                HIDapi.hid_free_enumeration(ptr);
                Debug.Log("No Joy-Cons found.");
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
            isleft = false;
        }
        else
        {
            HIDapi.hid_free_enumeration(ptr);
            Debug.Log("No Joy-Cons found.");
            return -1;
        }
        handle = HIDapi.hid_open_path(enumerate.path);
        HIDapi.hid_set_nonblocking(handle, 1);
        HIDapi.hid_free_enumeration(ptr);

        alive = true;
        return 0;
    }
    public void init(byte leds)
    {
        byte[] a = { 0x0 };
        // Input report mode
        subcommand(0x3, new byte[] { 0x3f }, 1);
        a[0] = 0x1;
        subcommand(0x48, a, 1);
        subcommand(0x40, a, 1);
        dump_calibration_data();
        // Connect
        a[0] = 0x01;
        subcommand(0x1, a, 1);
        a[0] = 0x02;
        subcommand(0x1, a, 1);
        a[0] = 0x03;
        subcommand(0x1, a, 1);
        a[0] = leds;
        printarray(subcommand(0x30, a, 1), report_len);
    }

    public void poll()
    {
        byte[] buf = new byte[report_len];
        HIDapi.hid_read_timeout(handle, buf, new UIntPtr(report_len), 50);

        if (buf[0] != 0x30)
        {
            Debug.Log("Changing input mode to 0x30. If this happens more than once something is wrong.");
            subcommand(0x3, new byte[] { 0x30 }, 1);
            return;
        }

        gyr[0] = (Int16)(buf[13] | ((buf[14] << 8) & 0xff00));
        gyr[1] = (Int16)(buf[15] | ((buf[16] << 8) & 0xff00));
        gyr[2] = (Int16)(buf[17] | ((buf[18] << 8) & 0xff00));

        acc[0] = (UInt16)((int)(buf[19] | ((buf[20] << 8) & 0xff00))+32767);
        acc[1] = (UInt16)((int)(buf[21] | ((buf[22] << 8) & 0xff00))+32767);
        acc[2] = (UInt16)((int)(buf[23] | ((buf[24] << 8) & 0xff00))+32767);
        Debug.Log(acc[0]);

        buttons[0] = buf[3 + (isleft ? 2 : 0)];
        buttons[1] = buf[4];

        stick_raw[0] = buf[6 + (isleft ? 0 : 3)];
        stick_raw[1] = buf[7 + (isleft ? 0 : 3)];
        stick_raw[2] = buf[8 + (isleft ? 0 : 3)];

        stick_precal[0] = (UInt16)(stick_raw[0] | ((stick_raw[1] & 0xf) << 8));
        stick_precal[1] = (UInt16)((stick_raw[1] >> 4) | (stick_raw[2] << 4));
        stick = center_sticks(stick_precal);

        for (int i = 0; i < 3; ++i)
            acc_f[i] = (acc[i]) / 32768.0 * alpha + (acc_f[i] * (1.0f - alpha));

        //https://theccontinuum.com/2012/09/24/arduino-imu-pitch-roll-from-accelerometer/
        euler[0] = (Math.Atan2(-acc_f[1], acc_f[2]) * 180.0) / Math.PI;
        euler[1] = (Math.Atan2(acc_f[0], Math.Sqrt(acc_f[1] * acc_f[1] + acc_f[2] * acc_f[2])) * 180.0) / Math.PI;

        //http://www.instructables.com/id/Accelerometer-Gyro-Tutorial/
    }
    
    public void set_zero_accel()
    {
        acc_g[0] = acc[0];
        acc_g[1] = acc[1];
        acc_g[2] = acc[2];
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

    private byte[] subcommand(byte subcommand, byte[] buf, uint len, bool print = true)
    {
        byte[] buf_ = new byte[report_len];
        byte[] response = new byte[report_len];
        Array.Copy(default_buf, buf_, 10);
        Array.Copy(buf, 0, buf_, 11, len);
        buf_[10] = subcommand;
        buf_[1] = global_count;
        ++global_count;
        if (global_count >= 0xf) global_count -= 0xf;

#if DEBUG
        if (print) { printarray(buf_, len + 11); };
#endif
        HIDapi.hid_write(handle, buf_, new UIntPtr(len + 11));
        int res = HIDapi.hid_read_timeout(handle, response, new UIntPtr(report_len), 100);
        if (res == 0)
        {
            Debug.Log("READ FAILED");
        }
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
                Debug.Log("Found user calibration data.");
                found = true;
                break;
            }
        }
        if (!found)
        {
            Debug.Log("Using factory calibration data.");
            buf_ = spi_read(0x60, (isleft ? (byte)0x3d : (byte)0x46), 9); // get user calibration data if possible
        }
        stick_cal[0] = (UInt16)((buf_[1] << 8) & 0xF00 | buf_[0]);
        stick_cal[1] = (UInt16)((buf_[2] << 4) | (buf_[1] >> 4));
        stick_cal[2] = (UInt16)((buf_[4] << 8) & 0xF00 | buf_[3]);
        stick_cal[3] = (UInt16)((buf_[5] << 4) | (buf_[4] >> 4));
        stick_cal[4] = (UInt16)((buf_[7] << 8) & 0xF00 | buf_[6]);
        stick_cal[5] = (UInt16)((buf_[8] << 4) | (buf_[7] >> 4));
#if DEBUG
        printarray(stick_cal, 6);
#endif 
    }
    private byte[] spi_read(byte addr1, byte addr2, uint len)
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
        printarray(ret, len);
#endif
        return ret;
    }
    private void printarray(byte[] arr, uint len)
    {
        string tostr = "";
        for (int i = 0; i < len; ++i)
        {
            tostr += string.Format("{0:X2} ", arr[i]);
        }
        Debug.Log(tostr);
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
