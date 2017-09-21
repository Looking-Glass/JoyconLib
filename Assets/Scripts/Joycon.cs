#define DEBUG

using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;
using System.Threading;
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
    private bool[] pressed = new bool[13];
    private bool[] released = new bool[13];
    private bool[] down = new bool[13];
    private bool[] down_ = new bool[13];

    public Int16[] stick = { 0, 0 };

    private IntPtr handle;

    // Different operating systems either do or don't like the trailing zero
    private const ushort vendor_id = 0x57e;
    private const ushort vendor_id_ = 0x057e;

    private const ushort product_l = 0x2006;
    private const ushort product_r = 0x2007;

    private byte[] default_buf = { 0x0, 0x1, 0x40, 0x40, 0x0, 0x1, 0x40, 0x40 };

    private byte[] stick_raw = { 0, 0, 0 };
    private UInt16[] stick_cal = { 0, 0, 0, 0, 0, 0 };
    private UInt16[] stick_precal = { 0, 0 };

    private bool stop_polling = false;
    private int timestamp;
    private bool first_imu_packet = true;
    private bool imu_enabled = false;
    private Int16[] acc_r = { 0, 0, 0 };
    private Vector3 acc_g;

    private Int16[] gyr_r = { 0, 0, 0 };
    private Vector3 gyr_g;
    private Vector3 gyr_est;

    public Vector3 pos;
    public Vector3 euler;
    private float filterweight;

    private const uint report_len = 49;
    private struct Report
    {
        byte[] r;
        System.DateTime t;
        public Report(byte[] report, System.DateTime time)
        {
            r = report;
            t = time;
        }
        public System.DateTime GetTime()
        {
            return t;
        }
        public byte[] GetBuffer()
        {
            return r;
        }
    };
    private Queue<Report> reports = new Queue<Report>();
    public enum DebugType : int
    {
        NONE,
        ALL,
        COMMS,
        THREADING,
        IMU,
        RUMBLE,
    };
    public DebugType debug_type = DebugType.ALL;
    private byte global_count = 0;
    private string debug_str;

    public Joycon()
    {
    }
    public void DebugPrint(String s, DebugType d)
    {
        if (debug_type == DebugType.NONE) return;
        if (d == debug_type || debug_type == DebugType.ALL)
        {
            Debug.Log(s);
        }
    }
    public bool GetKeyPressed(Button key)
    {
        return pressed[(int)key];
    }
    public bool GetKeyDown(Button key)
    {
        return down[(int)key];
    }
    public bool GetKeyReleased(Button key)
    {
        return released[(int)key];
    }
    public Int16[] GetStick()
    {
        return stick;
    }
    public Vector3 GetVector()
    {
        return pos;
    }
    public int Attach(byte leds = 0x0, bool imu = true, float alpha = 1f)
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
                DebugPrint("No Joy-Cons found.", DebugType.ALL);
                state = state_.NO_JOYCONS;
                return -1;
            }
        }
        hid_device_info enumerate = (hid_device_info)Marshal.PtrToStructure(ptr, typeof(hid_device_info));
        if (enumerate.product_id == product_l)
        {
            isleft = true;
            DebugPrint("Left Joy-Con connected.", DebugType.ALL);
        }
        else if (enumerate.product_id == product_r)
        {
            DebugPrint("Right Joy-Con connected.", DebugType.ALL);
        }
        else
        {
            HIDapi.hid_free_enumeration(ptr);
            DebugPrint("No Joy-Cons found.", DebugType.ALL);
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
        Subcommand(0x3, new byte[] { 0x30 }, 1, true);

        DebugPrint("Done with init.", DebugType.COMMS);
        return 0;
    }
    public string GetDebugText()
    {
        if (!down[(int)Button.DPAD_DOWN]) debug_str = "x:" + pos[0] + "\ny:" + pos[1] + "\nz:" + pos[2] + "\n";
        return debug_str;
    }
    public void SetFilterCoeff(float a)
    {
        filterweight = a;
    }
    public void Detach()
    {
        stop_polling = true;
        PrintArray(max, format: "max {0:S}");
        PrintArray(sum, format: "Sum {0:S}");
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
    private byte ts_en;
    private byte ts_de;
    private int ReceiveRaw(bool block = false)
    {
        if (handle == IntPtr.Zero) return -2;
        HIDapi.hid_set_nonblocking(handle, block ? 0 : 1);
        byte[] raw_buf = new byte[report_len];
        int ret = HIDapi.hid_read(handle, raw_buf, new UIntPtr(report_len));
        if (ret > 0)
        {
            lock (reports)
            {
                reports.Enqueue(new Report(raw_buf, System.DateTime.Now));
            }
            if (ts_en == raw_buf[1])
            {
                DebugPrint(string.Format("Duplicate timestamp enqueued. TS: {0:X2}", ts_en), DebugType.THREADING);
            }
            ts_en = raw_buf[1];
            DebugPrint(string.Format("Enqueue. Blocking? {0:b}. Bytes read: {1:D}. Timestamp: {2:X2}", block, ret, raw_buf[1]), DebugType.THREADING);
        }
        return ret;
    }
    private Thread PollThreadObj;
    private void Poll()
    {
        bool recvd = false;
        int attempts = 0;
        while (!stop_polling)
        {
            int a = ReceiveRaw(recvd);
            if (a > 0)
            {
                state = state_.IMU_DATA_OK;
                attempts = 0;
                recvd = true;
            }
            else if (attempts > 1000)
            {
                state = state_.DROPPED;
                DebugPrint("Connection lost. Is the Joy-Con connected?", DebugType.ALL);
                break;
            }
            else
            {
                Thread.Sleep(5);
            }
            ++attempts;
        }
        DebugPrint("End poll loop.", DebugType.THREADING);
    }
    float[] max = { 0, 0, 0 };
    float[] sum = { 0, 0, 0 };
    public void Update()
    {
        if (state > state_.NO_JOYCONS)
        {
            byte[] report_buf = new byte[report_len];
            while (reports.Count > 0)
            {
                Report rep;
                lock (reports)
                {
                    rep = reports.Dequeue();
                    report_buf = rep.GetBuffer();
                }
                ProcessIMU(report_buf);
                if (ts_de == report_buf[1])
                {
                    DebugPrint(string.Format("Duplicate timestamp dequeued. TS: {0:X2}", ts_de), DebugType.THREADING);
                }
                ts_de = report_buf[1];
                DebugPrint(string.Format("Dequeue. Queue length: {0:d}. Packet ID: {1:X2}. Timestamp: {2:x2}. Lag: {3:s}", reports.Count, report_buf[0], report_buf[1], System.DateTime.Now.Subtract(rep.GetTime())), DebugType.THREADING);
            }

            ProcessButtonsAndStick(report_buf);
        }
    }
    private int ProcessButtonsAndStick(byte[] report_buf)
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
            down_[i] = down[i];
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

        for (int i = 0; i < down.Length; ++i)
        {
            released[i] = (down_[i] & !down[i]);
            pressed[i] = (!down_[i] & down[i]);
        }
        return 0;
    }
    private int ProcessIMU(byte[] report_buf)
    {

        if (!imu_enabled | state < state_.IMU_DATA_OK)
            return -1;

        if (report_buf[0] != 0x30) return -1; // no gyro data
                                              // read raw IMU values
        int dt = (report_buf[1] - timestamp);
        if (report_buf[1] < timestamp) dt += 0x100;

        for (int n = 0; n < 3; ++n)
        {
            gyr_r[0] = (Int16)(report_buf[19 + n * 12] | ((report_buf[20 + n * 12] << 8) & 0xff00));
            gyr_r[1] = (Int16)(report_buf[21 + n * 12] | ((report_buf[22 + n * 12] << 8) & 0xff00));
            gyr_r[2] = (Int16)(report_buf[23 + n * 12] | ((report_buf[24 + n * 12] << 8) & 0xff00));

            acc_r[0] = (Int16)(report_buf[13 + n * 12] | ((report_buf[14 + n * 12] << 8) & 0xff00));
            acc_r[1] = (Int16)(report_buf[15 + n * 12] | ((report_buf[16 + n * 12] << 8) & 0xff00));
            acc_r[2] = (Int16)(report_buf[17 + n * 12] | ((report_buf[18 + n * 12] << 8) & 0xff00));

            for (int i = 0; i < 3; ++i)
            {
                gyr_r[i] = (Int16)(gyr_r[i] * ((isleft & i > 0) ? -1 : 1));
                acc_g[i] = acc_r[i] * 0.00025f;
                gyr_g[i] = gyr_r[i] * (isleft ? 0.061f : 0.07344f);
                if (Math.Abs(acc_g[i]) > Math.Abs(max[i]))
                    max[i] = acc_g[i];
            }
            acc_g = acc_g.normalized;
            if (first_imu_packet)
            {
                pos = acc_g;
                first_imu_packet = false;
            }
            else
            {
                for (int i = 0; i < 3; ++i) sum[i] += gyr_g[i] * (0.005f * dt);
                if (Math.Abs(pos[2]) < 0.1f)
                {
                    gyr_est = pos;
                }
                else
                {
                    euler[0] = Mathf.Atan2(pos[0], pos[2]) + gyr_g[0] * (0.005f * dt);
                    euler[1] = Mathf.Atan2(pos[1], pos[2]) + gyr_g[1] * (0.005f * dt);
                    //euler[2] = Mathf.Atan2(pos[0], pos[0]) + gyr_g[1] * (0.005f * dt);
                }
                int sign = (Math.Cos(euler[0]) >= 0) ? 1 : -1;
                for (int i = 0; i < 1; ++i)
                {
                    gyr_est[i] = Mathf.Sin(euler[i] * Mathf.PI / 180);
                    gyr_est[i] /= Mathf.Sqrt(1 + Mathf.Pow((Mathf.Cos(euler[i] * Mathf.PI / 180)), 2) * Mathf.Pow(Mathf.Tan(euler[1 - i] * Mathf.PI / 180), 2));
                }
                gyr_est[2] = sign * Mathf.Sqrt(1 - Mathf.Pow(gyr_est[0], 2) - Mathf.Pow(gyr_est[1], 2));
            }
            pos = (acc_g + gyr_est * filterweight) / (1 + filterweight);
            pos = pos.normalized;
            dt = 1;
        }
        timestamp = report_buf[1] + 2;
        return 0;
    }
    public void Begin()
    {
        PollThreadObj = new Thread(new ThreadStart(Poll));
        PollThreadObj.Start();
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
    private void Rumble(byte[] buf=null)
    {
        byte[] buf_ = new byte[report_len];
        buf[0] = 0x10;
        buf_[1] = global_count;
        if (global_count == 0xf) global_count = 0;
        else ++global_count;
        if (buf == null)
        {
            Array.Copy(default_buf, 0, buf_, 2, 8);
        }
        else
        {
            Array.Copy(buf, 0, buf_, 2, 8);
        }
        PrintArray(buf_, DebugType.RUMBLE, 8, 0, string.Format("Rumble data sent: {0:S}"));
        HIDapi.hid_write(handle, buf_, new UIntPtr(report_len));
    }
    private byte[] Subcommand(byte sc, byte[] buf, uint len, bool print = true)
    {
        byte[] buf_ = new byte[report_len];
        byte[] response = new byte[report_len];
        Array.Copy(default_buf, 0, buf_, 2, 8);
        Array.Copy(buf, 0, buf_, 11, len);
        buf_[10] = sc;
        buf_[1] = global_count;
        buf_[0] = 0x1;
        if (global_count == 0xf) global_count = 0;
        else ++global_count;
        if (print) { PrintArray(buf_, DebugType.COMMS, len, 11, "Subcommand 0x" + string.Format("{0:X2}", sc) + " sent. Data: 0x{0:S}"); };
        HIDapi.hid_write(handle, buf_, new UIntPtr(len + 11));
        int res = HIDapi.hid_read_timeout(handle, response, new UIntPtr(report_len), 50);
        if (res < 1) DebugPrint("No response.", DebugType.COMMS);
        else if (print) { PrintArray(response, DebugType.COMMS, report_len - 1, 1, "Response ID 0x" + string.Format("{0:X2}", response[0]) + ". Data: 0x{0:S}"); }
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
                DebugPrint("Using user stick calibration data.", DebugType.COMMS);
                found = true;
                break;
            }
        }
        if (!found)
        {
            DebugPrint("Using factory stick calibration data.", DebugType.COMMS);
            buf_ = ReadSPI(0x60, (isleft ? (byte)0x3d : (byte)0x46), 9); // get user calibration data if possible
        }
        stick_cal[0] = (UInt16)((buf_[1] << 8) & 0xF00 | buf_[0]);
        stick_cal[1] = (UInt16)((buf_[2] << 4) | (buf_[1] >> 4));
        stick_cal[2] = (UInt16)((buf_[4] << 8) & 0xF00 | buf_[3]);
        stick_cal[3] = (UInt16)((buf_[5] << 4) | (buf_[4] >> 4));
        stick_cal[4] = (UInt16)((buf_[7] << 8) & 0xF00 | buf_[6]);
        stick_cal[5] = (UInt16)((buf_[8] << 4) | (buf_[7] >> 4));
    }
    private byte[] ReadSPI(byte addr1, byte addr2, uint len, bool print = false)
    {
        byte[] buf = { addr2, addr1, 0x00, 0x00, (byte)len };
        byte[] read_buf = new byte[len];
        byte[] buf_ = new byte[len + 20];

        for (int i = 0; i < 100; ++i)
        {
            buf_ = Subcommand(0x10, buf, 5, false);
            if (buf_[15] == addr2 && buf_[16] == addr1)
            {
                break;
            }
        }
        Array.Copy(buf_, 20, read_buf, 0, len);
        if (print) PrintArray(read_buf, DebugType.COMMS, len);
        return read_buf;
    }
    private void PrintArray<T>(T[] arr, DebugType d = DebugType.NONE, uint len = 0, uint start = 0, string format = "{0:S}")
    {
        if (d != debug_type && debug_type != DebugType.ALL) return;
        if (len == 0) len = (uint)arr.Length;
        string tostr = "";
        for (int i = 0; i < len; ++i)
        {
            tostr += string.Format((arr[0] is byte) ? "{0:X2} " : ((arr[0] is float) ? "{0:F} " : "{0:D} "), arr[i + start]);
        }
        DebugPrint(string.Format(format, tostr), d);
    }

    
}