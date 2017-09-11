#define DEBUG

using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;
using UnityEngine;

public class Joycon {

    private byte[] default_buf = { 0x1, 0x0, 0x0, 0x1, 0x40, 0x40, 0x0, 0x1, 0x40, 0x40 };
    private byte global_count = 0;
    public bool isleft;
    public bool alive=false;
    public int roll, pitch, yaw;
    public int acx, acy, acz;
    private IntPtr handle;

    public int attach()
    {
        HIDapi.hid_init();
        IntPtr ptr = HIDapi.hid_enumerate(0x057e, 0x0);
        if (ptr == IntPtr.Zero)
        {
            HIDapi.hid_free_enumeration(ptr);
            Debug.Log("No Joy-Cons found.");
            return -1;
        }
        hid_device_info enumerate = (hid_device_info)Marshal.PtrToStructure(ptr, typeof(hid_device_info));
        if (enumerate.product_id == 0x2006)
        {
            isleft = true;
            Debug.Log("Left Joy-Con connected.");
        }
        else if (enumerate.product_id == 0x2007)
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
        HIDapi.hid_free_enumeration(ptr);
        alive = true;
        return 0;
    }
    public void init(byte leds)
    {
        byte[] a = { 0x1 };
        // Enable rumble
        subcommand(0x48, a, 1);
        // Enable IMU
        subcommand(0x40, a, 1);
        // Input report mode
        a[0] = 0x30;
        subcommand(0x3, a, 1);
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
    public void poll()
    {
        byte[] buf = new byte[65];
        HIDapi.hid_read_timeout(handle, buf, new UIntPtr(64), 200);
        if (buf[0] == 0x30)
        {
            roll = buf[13] | ((buf[14] << 8) & 0xff00);
            pitch = buf[15] | ((buf[16] << 8) & 0xff00);
            yaw = buf[17] | ((buf[18] << 8) & 0xff00);
            acx = buf[19] | ((buf[20] << 8) & 0xff00);
            acy = buf[21] | ((buf[22] << 8) & 0xff00);
            acz = buf[23] | ((buf[24] << 8) & 0xff00);
            //http://www.instructables.com/id/Accelerometer-Gyro-Tutorial/
        }
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

    public void subcommand(byte subcommand, byte[] buf, uint len)
    {
        byte[] buf_ = new byte[65];
        Array.Copy(default_buf, buf_, 10);
        Array.Copy(buf, 0, buf_, 11, len);
        buf_[10] = subcommand;
        buf_[1] = global_count;
        ++global_count;
        if (global_count >= 0xf) global_count -= 0xf;
#if DEBUG
        printarray(buf_, len+12);
#endif
        HIDapi.hid_write(handle, buf_, new UIntPtr(len+11));
    }
}
