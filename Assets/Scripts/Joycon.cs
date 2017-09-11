#define DEBUG

using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;
using UnityEngine;

public class Joycon {

    public bool isleft;
    public bool alive = false;
    public int roll, pitch, yaw;
    public int acx, acy, acz;
    public byte[] buttons = { 0x00, 0x00 };
    public UInt16[] stick = { 0, 0 };

    private IntPtr handle;

    // Different operating systems either do or don't like the trailing zero
	private const ushort vendor_id  = 0x57e;
    private const ushort vendor_id_ = 0x057e;

	private const ushort product_l = 0x2006;
	private const ushort product_r = 0x2007;
	private byte[] default_buf = { 0x1, 0x0, 0x0, 0x1, 0x40, 0x40, 0x0, 0x1, 0x40, 0x40 };
    private byte[] stick_raw = { 0, 0, 0 };
    private byte global_count = 0;

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

        //hid_device_info top = enumerate;
        //while (true)
        //{
        //    Debug.Log(top.vendor_id + " " + top.product_id);
        //    if (top.next != IntPtr.Zero)
        //    {
        //        top = (hid_device_info)Marshal.PtrToStructure(top.next, typeof(hid_device_info));
        //    }
        //    else
        //    {
        //        break;
        //    }
        //}
        //return -1;

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
        // Input report mode reset
        a[0] = 0x3f;
        for (int i = 0; i < 5; ++i)
        {
            subcommand(0x3, a, 1);
        }
        dump_calibration_data();
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
        for (int i = 0; i < 5; ++i)
        {
            subcommand(0x30, a, 1);
        }
    }

    private void dump_calibration_data()
    {
        byte[] buf_ = spi_read(0x80, 0x60, 38);
        //uint len = 48;
        //byte[] buf = { 0x60, 0x80, 0x00, 0x00, (byte)len }; // start reading from SPI flash at address 0x801b (little endian)
        //byte[] buf_ = new byte[len];
        //HIDapi.hid_set_nonblocking(handle, 1);
        //subcommand(0x10, buf, 5);
        //HIDapi.hid_read(handle, buf_, new UIntPtr(len));
        //HIDapi.hid_set_nonblocking(handle, 0);

#if DEBUG
        Debug.Log("Calibration Data");
        printarray(buf_, 38);
#endif
    }

    private byte[] spi_read(byte addr1, byte addr2, uint len)
    {
        byte[] buf = { addr2, addr1, 0x00, 0x00, (byte)len }; // start reading from SPI flash at address 0x801b (little endian)
        byte[] buf_ = new byte[len+10];
        subcommand(0x10, buf, 5);
        for (int i = 0; i < 1000; ++i){
            HIDapi.hid_read_timeout(handle, buf_, new UIntPtr(len), 200);
            if (buf_[13] == addr2 && buf_[14] == addr1)
            {
                break;
            }
        }
        return buf_;
    }

    public void poll()
    {
        byte[] buf = new byte[65];
        HIDapi.hid_read_timeout(handle, buf, new UIntPtr(64), 200);
        if (buf[0] == 0x30)
        {
            roll = buf[13]  | ((buf[14] << 8) & 0xff00);
            pitch = buf[15] | ((buf[16] << 8) & 0xff00);
            yaw = buf[17]   | ((buf[18] << 8) & 0xff00);
            acx = buf[19]   | ((buf[20] << 8) & 0xff00);
            acy = buf[21]   | ((buf[22] << 8) & 0xff00);
            acz = buf[23]   | ((buf[24] << 8) & 0xff00);
            buttons[0] = buf[2 + (isleft ? 2 : 0)];
            buttons[1] = buf[3];
            stick_raw[0] = buf[5 + (isleft ? 0 : 3)];
            stick_raw[1] = buf[6 + (isleft ? 0 : 3)];
            stick_raw[2] = buf[7 + (isleft ? 0 : 3)];
            stick[0] = (UInt16)(stick_raw[0] | ((stick_raw[1] & 0xf) << 8));
            stick[1] = (UInt16)((stick_raw[1] >> 4) | (stick_raw[2] << 4));
            //http://www.instructables.com/id/Accelerometer-Gyro-Tutorial/
        }
    }

    public void calibrate() {
        for (int i = 0; i < 10; ++i)
        {

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
        printarray(buf_, len+11);
#endif
        HIDapi.hid_write(handle, buf_, new UIntPtr(len+11));
    }
}
