#define DEBUG

using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;

using System.Threading;
using UnityEngine;

public class Joycon
{
    public enum DebugType : int
    {
        NONE,
        ALL,
        COMMS,
        THREADING,
        IMU,
        RUMBLE,
    };
	public DebugType debug_type = DebugType.NONE;
    public bool isLeft;
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
    private bool[] buttons_down = new bool[13];
    private bool[] buttons_up = new bool[13];
    private bool[] buttons = new bool[13];
    private bool[] down_ = new bool[13];

    private float[] stick = { 0, 0 };

    private 
	IntPtr handle;

    byte[] default_buf = { 0x0, 0x1, 0x40, 0x40, 0x0, 0x1, 0x40, 0x40 };

    private byte[] stick_raw = { 0, 0, 0 };
    private UInt16[] stick_cal = { 0, 0, 0, 0, 0, 0 };
    private UInt16 deadzone;
    private UInt16[] stick_precal = { 0, 0 };

    private bool stop_polling = false;
    private int timestamp;
    private bool first_imu_packet = true;
    private bool imu_enabled = false;
    private Int16[] acc_r = { 0, 0, 0 };
    private Vector3 acc_g;

    private Int16[] gyr_r = { 0, 0, 0 };
    private Int16[] gyr_neutral = { 0, 0, 0 };
    private Vector3 gyr_g;
	private bool do_localize;
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
        public void CopyBuffer(byte[] b)
        {
            for (int i = 0; i < report_len; ++i)
            {
                b[i] = r[i];
            }
        }
    };
    private struct Rumble
    {
        private float h_f, amp, l_f;
        public float t;
        public bool timed_rumble;

        public void set_vals(float low_freq, float high_freq, float amplitude, int time = 0)
        {
            h_f = high_freq;
            amp = amplitude;
            l_f = low_freq;
            timed_rumble = false;
            t = 0;
            if (time != 0)
            {
                t = time / 1000f;
                timed_rumble = true;
            }
        }
        public Rumble(float low_freq, float high_freq, float amplitude, int time = 0)
        {
            h_f = high_freq;
            amp = amplitude;
            l_f = low_freq;
            timed_rumble = false;
            t = 0;
            if (time != 0)
            {
                t = time / 1000f;
                timed_rumble = true;
            }
        }
        private float clamp(float x, float min, float max)
        {
            if (x < min) return min;
            if (x > max) return max;
            return x;
        }
        public byte[] GetData()
        {
            byte[] rumble_data = new byte[8];
            if (amp == 0.0f){
                rumble_data[0] = 0x0;
                rumble_data[1] = 0x1;
                rumble_data[2] = 0x40;
                rumble_data[3] = 0x40;
            }else{
                l_f = clamp(l_f, 40.875885f, 626.286133f);
                amp = clamp(amp, 0.0f, 1.0f);
                h_f = clamp(h_f, 81.75177f, 1252.572266f);
                UInt16 hf = (UInt16)((Mathf.Round(32f * Mathf.Log(h_f * 0.1f, 2)) - 0x60) * 4);
                byte lf = (byte)(Mathf.Round(32f * Mathf.Log(l_f * 0.1f, 2)) - 0x40);
                byte hf_amp;
                if (amp == 0) hf_amp = 0;
                else if (amp < 0.117) hf_amp = (byte)(((Mathf.Log(amp * 1000, 2) * 32) - 0x60) / (5 - Mathf.Pow(amp, 2)) - 1);
                else if (amp < 0.23) hf_amp = (byte)(((Mathf.Log(amp * 1000, 2) * 32) - 0x60) - 0x5c);
                else hf_amp = (byte)((((Mathf.Log(amp * 1000, 2) * 32) - 0x60) * 2) - 0xf6);

                UInt16 lf_amp = (UInt16)(Mathf.Round(hf_amp) * .5);
                byte parity = (byte)(lf_amp % 2);
                if (parity > 0)
                {
                    --lf_amp;
                }

                lf_amp = (UInt16)(lf_amp >> 1);
                lf_amp += 0x40;
                if (parity > 0) lf_amp |= 0x8000;
                rumble_data = new byte[8];
                rumble_data[0] = (byte)(hf & 0xff);
                rumble_data[1] = (byte)((hf >> 8) & 0xff);
                rumble_data[2] = lf;
                rumble_data[1] += hf_amp;
                rumble_data[2] += (byte)((lf_amp >> 8) & 0xff);
                rumble_data[3] += (byte)(lf_amp & 0xff);
            }
            for (int i = 0; i < 4; ++i)
            {
                rumble_data[4 + i] = rumble_data[i];
            }
            //Debug.Log(string.Format("Encoded hex freq: {0:X2}", encoded_hex_freq));
            //Debug.Log(string.Format("lf_amp: {0:X4}", lf_amp));
            //Debug.Log(string.Format("hf_amp: {0:X2}", hf_amp));
            //Debug.Log(string.Format("l_f: {0:F}", l_f));
            //Debug.Log(string.Format("hf: {0:X4}", hf));
            //Debug.Log(string.Format("lf: {0:X2}", lf));
            return rumble_data;
        }
    }
    private Queue<Report> reports = new Queue<Report>();
    private Rumble rumble_obj;

    private byte global_count = 0;
    private string debug_str;

	public Joycon(IntPtr handle_, bool imu, bool localize, float alpha, bool left)
    {
		handle = handle_;
		imu_enabled = imu;
		do_localize = localize;
        rumble_obj = new Rumble(160, 320, 0);
		filterweight = alpha;
		isLeft = left;
    }
    public void DebugPrint(String s, DebugType d)
    {
        if (debug_type == DebugType.NONE) return;
        if (d == DebugType.ALL || d == debug_type || debug_type == DebugType.ALL)
        {
            Debug.Log(s);
        }
    }
    public bool GetButtonDown(Button b)
    {
        return buttons_down[(int)b];
    }
    public bool GetButton(Button b)
    {
        return buttons[(int)b];
    }
    public bool GetButtonUp(Button b)
    {
        return buttons_up[(int)b];
    }
    public float[] GetStick()
    {
        return stick;
    }
    public Vector3 GetGyro()
    {
        return gyr_g;
    }
    public Vector3 GetAccel()
    {
        return acc_g;
    }
    public Quaternion GetVector()
    {
        Vector3 v1 = new Vector3(j_b.x, i_b.x, k_b.x);
        Vector3 v2 = -(new Vector3(j_b.z, i_b.z, k_b.z));
        if (v2 != Vector3.zero){
		    return Quaternion.LookRotation(v1, v2);
        }else{
            return Quaternion.identity;
        }
    }
	public int Attach(byte leds_ = 0x0)
    {
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
        a[0] = leds_;
        Subcommand(0x30, a, 1);
        Subcommand(0x40, new byte[] { (imu_enabled ? (byte)0x1 : (byte)0x0) }, 1, true);
        Subcommand(0x3, new byte[] { 0x30 }, 1, true);
        Subcommand(0x48, new byte[] { 0x1 }, 1, true);
        DebugPrint("Done with init.", DebugType.COMMS);
        return 0;
    }
    public void SetFilterCoeff(float a)
    {
        filterweight = a;
    }
    public void Detach()
    {
        stop_polling = true;
        PrintArray(max, format: "Max {0:S}", d: DebugType.IMU);
        PrintArray(sum, format: "Sum {0:S}", d: DebugType.IMU);
        if (state > state_.NO_JOYCONS)
        {
            Subcommand(0x30, new byte[] { 0x0 }, 1);
            Subcommand(0x40, new byte[] { 0x0 }, 1);
            Subcommand(0x48, new byte[] { 0x0 }, 1);
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
    private System.DateTime ts_prev;
    private int ReceiveRaw()
    {
        if (handle == IntPtr.Zero) return -2;
        HIDapi.hid_set_nonblocking(handle, 0);
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
            DebugPrint(string.Format("Enqueue. Bytes read: {0:D}. Timestamp: {1:X2}", ret, raw_buf[1]), DebugType.THREADING);
        }
        return ret;
    }
    private Thread PollThreadObj;
    private void Poll()
    {
        int attempts = 0;
        while (!stop_polling & state > state_.NO_JOYCONS)
        {
            SendRumble(rumble_obj.GetData());
            int a = ReceiveRaw();
            a = ReceiveRaw();
            if (a > 0)
            {
                state = state_.IMU_DATA_OK;
                attempts = 0;
            }
            else if (attempts > 1000)
            {
                state = state_.DROPPED;
                DebugPrint("Connection lost. Is the Joy-Con connected?", DebugType.ALL);
                break;
            }
            else
            {
                DebugPrint("Pause 5ms", DebugType.THREADING);
                Thread.Sleep((Int32)5);
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
                    rep.CopyBuffer(report_buf);
                }
                if (imu_enabled)
                {
                    if (do_localize)
                    {
                        ProcessIMU(report_buf);
                    }
                    else
                    {
                        ExtractIMUValues(report_buf, 0);
                    }
                }
                if (ts_de == report_buf[1])
                {
                    DebugPrint(string.Format("Duplicate timestamp dequeued. TS: {0:X2}", ts_de), DebugType.THREADING);
                }
                ts_de = report_buf[1];
                DebugPrint(string.Format("Dequeue. Queue length: {0:d}. Packet ID: {1:X2}. Timestamp: {2:X2}. Lag to dequeue: {3:t}. Lag between packets (expect 15ms): {4:g}",
                    reports.Count, report_buf[0], report_buf[1], System.DateTime.Now.Subtract(rep.GetTime()), rep.GetTime().Subtract(ts_prev)), DebugType.THREADING);
                ts_prev = rep.GetTime();
            }
            ProcessButtonsAndStick(report_buf);
			if (rumble_obj.timed_rumble) {
				if (rumble_obj.t < 0) {
					rumble_obj.set_vals (160, 320, 0, 0);
				} else {
					rumble_obj.t -= Time.deltaTime;
				}
			}
        }
    }
    private int ProcessButtonsAndStick(byte[] report_buf)
    {
        if (report_buf[0] == 0x00) return -1;

        stick_raw[0] = report_buf[6 + (isLeft ? 0 : 3)];
        stick_raw[1] = report_buf[7 + (isLeft ? 0 : 3)];
        stick_raw[2] = report_buf[8 + (isLeft ? 0 : 3)];

        stick_precal[0] = (UInt16)(stick_raw[0] | ((stick_raw[1] & 0xf) << 8));
        stick_precal[1] = (UInt16)((stick_raw[1] >> 4) | (stick_raw[2] << 4));
        stick = CenterSticks(stick_precal);
        lock (buttons)
        {
            lock (down_)
            {
                for (int i = 0; i < buttons.Length; ++i)
                {
                    down_[i] = buttons[i];
                }
            }
            buttons[(int)Button.DPAD_DOWN] = (report_buf[3 + (isLeft ? 2 : 0)] & (isLeft ? 0x01 : 0x04)) != 0;
            buttons[(int)Button.DPAD_RIGHT] = (report_buf[3 + (isLeft ? 2 : 0)] & (isLeft ? 0x04 : 0x08)) != 0;
            buttons[(int)Button.DPAD_UP] = (report_buf[3 + (isLeft ? 2 : 0)] & (isLeft ? 0x02 : 0x02)) != 0;
            buttons[(int)Button.DPAD_LEFT] = (report_buf[3 + (isLeft ? 2 : 0)] & (isLeft ? 0x08 : 0x01)) != 0;
            buttons[(int)Button.HOME] = ((report_buf[4] & 0x10) != 0);
            buttons[(int)Button.MINUS] = ((report_buf[4] & 0x01) != 0);
            buttons[(int)Button.PLUS] = ((report_buf[4] & 0x02) != 0);
            buttons[(int)Button.STICK] = ((report_buf[4] & (isLeft ? 0x08 : 0x04)) != 0);
            buttons[(int)Button.SHOULDER_1] = (report_buf[3 + (isLeft ? 2 : 0)] & 0x40) != 0;
            buttons[(int)Button.SHOULDER_2] = (report_buf[3 + (isLeft ? 2 : 0)] & 0x80) != 0;
            buttons[(int)Button.SR] = (report_buf[3 + (isLeft ? 2 : 0)] & 0x10) != 0;
            buttons[(int)Button.SL] = (report_buf[3 + (isLeft ? 2 : 0)] & 0x20) != 0;
            lock (buttons_up)
            {
                lock (buttons_down)
                {
                    for (int i = 0; i < buttons.Length; ++i)
                    {
                        buttons_up[i] = (down_[i] & !buttons[i]);
                        buttons_down[i] = (!down_[i] & buttons[i]);
                    }
                }
            }
        }
        return 0;
    }
    private void ExtractIMUValues(byte[] report_buf, int n = 0)
    {
        gyr_r[0] = (Int16)(report_buf[19 + n * 12] | ((report_buf[20 + n * 12] << 8) & 0xff00));
        gyr_r[1] = (Int16)(report_buf[21 + n * 12] | ((report_buf[22 + n * 12] << 8) & 0xff00));
        gyr_r[2] = (Int16)(report_buf[23 + n * 12] | ((report_buf[24 + n * 12] << 8) & 0xff00));
        acc_r[0] = (Int16)(report_buf[13 + n * 12] | ((report_buf[14 + n * 12] << 8) & 0xff00));
        acc_r[1] = (Int16)(report_buf[15 + n * 12] | ((report_buf[16 + n * 12] << 8) & 0xff00));
        acc_r[2] = (Int16)(report_buf[17 + n * 12] | ((report_buf[18 + n * 12] << 8) & 0xff00));
        for (int i = 0; i < 3; ++i)
        {
            acc_g[i] = acc_r[i] * 0.00025f;
            gyr_g[i] = (gyr_r[i] - gyr_neutral[i]) * 0.00122187695f;
            if (Math.Abs(acc_g[i]) > Math.Abs(max[i]))
                max[i] = acc_g[i];
        }
    }

	private float err;
    public Vector3 i_b, j_b, k_b, k_acc;
	private Vector3 d_theta;
	private Vector3 i_b_;
	private Vector3 w_a, w_g;
    private Quaternion vec;
	
    private int ProcessIMU(byte[] report_buf)
    {

		// Direction Cosine Matrix method
		// http://www.starlino.com/dcm_tutorial.html

        if (!imu_enabled | state < state_.IMU_DATA_OK)
            return -1;

        if (report_buf[0] != 0x30) return -1; // no gyro data

        // read raw IMU values
        int dt = (report_buf[1] - timestamp);
        if (report_buf[1] < timestamp) dt += 0x100;

        for (int n = 0; n < 3; ++n)
        {
            ExtractIMUValues(report_buf, n);
            
			float dt_sec = 0.005f * dt;
            sum[0] += gyr_g.x * dt_sec;
            sum[1] += gyr_g.y * dt_sec;
            sum[2] += gyr_g.z * dt_sec;

            if (isLeft)
            {
                gyr_g.y *= -1;
                gyr_g.z *= -1;
                acc_g.y *= -1;
                acc_g.z *= -1;
            }

            if (first_imu_packet)
            {
                i_b = new Vector3(1, 0, 0);
                j_b = new Vector3(0, 1, 0);
                k_b = new Vector3(0, 0, 1);
                first_imu_packet = false;
            }
            else
            {
                k_acc = -Vector3.Normalize(acc_g);
                w_a = Vector3.Cross(k_b, k_acc);
                w_g = -gyr_g * dt_sec;
                d_theta = (filterweight * w_a + w_g) / (1f + filterweight);
                k_b += Vector3.Cross(d_theta, k_b);
                i_b += Vector3.Cross(d_theta, i_b);
                j_b += Vector3.Cross(d_theta, j_b);
                //Correction, ensure new axes are orthogonal
                err = Vector3.Dot(i_b, j_b) * 0.5f;
                i_b_ = Vector3.Normalize(i_b - err * j_b);
                j_b = Vector3.Normalize(j_b - err * i_b);
                i_b = i_b_;
                k_b = Vector3.Cross(i_b, j_b);
            }
            dt = 1;
        }
        timestamp = report_buf[1] + 2;
        return 0;
    }
    public void Begin()
    {
        if (PollThreadObj == null)
        {
            PollThreadObj = new Thread(new ThreadStart(Poll));
            PollThreadObj.Start();
        }
    }
    public void Recenter()
    {
        first_imu_packet = true;
    }
    private float[] CenterSticks(UInt16[] vals)
    {

        float[] s = { 0, 0 };
        for (uint i = 0; i < 2; ++i)
        {
            float diff = vals[i] - stick_cal[2 + i];
            if (Math.Abs(diff) < deadzone) vals[i] = 0;
            else if (diff > 0) // if axis is above center
            {
                s[i] = diff / stick_cal[i];
            }
            else
            {
                s[i] = diff / stick_cal[4 + i];
            }
        }
        return s;
    }
    public void SetRumble(float low_freq, float high_freq, float amp, int time = 0)
    {
        if (state <= Joycon.state_.ATTACHED) return;
		if (rumble_obj.timed_rumble == false || rumble_obj.t < 0)
        {
            rumble_obj = new Rumble(low_freq, high_freq, amp, time);
        }
    }
    private void SendRumble(byte[] buf)
    {
        byte[] buf_ = new byte[report_len];
        buf_[0] = 0x10;
        buf_[1] = global_count;
        if (global_count == 0xf) global_count = 0;
        else ++global_count;
        Array.Copy(buf, 0, buf_, 2, 8);
        PrintArray(buf_, DebugType.RUMBLE, format: "Rumble data sent: {0:S}");
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
        byte[] buf_ = ReadSPI(0x80, (isLeft ? (byte)0x12 : (byte)0x1d), 9); // get user calibration data if possible
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
            buf_ = ReadSPI(0x60, (isLeft ? (byte)0x3d : (byte)0x46), 9); // get user calibration data if possible
        }
        stick_cal[isLeft ? 0 : 2] = (UInt16)((buf_[1] << 8) & 0xF00 | buf_[0]); // X Axis Max above center
        stick_cal[isLeft ? 1 : 3] = (UInt16)((buf_[2] << 4) | (buf_[1] >> 4));  // Y Axis Max above center
        stick_cal[isLeft ? 2 : 4] = (UInt16)((buf_[4] << 8) & 0xF00 | buf_[3]); // X Axis Center
        stick_cal[isLeft ? 3 : 5] = (UInt16)((buf_[5] << 4) | (buf_[4] >> 4));  // Y Axis Center
        stick_cal[isLeft ? 4 : 0] = (UInt16)((buf_[7] << 8) & 0xF00 | buf_[6]); // X Axis Min below center
        stick_cal[isLeft ? 5 : 1] = (UInt16)((buf_[8] << 4) | (buf_[7] >> 4));  // Y Axis Min below center

        PrintArray(stick_cal, len: 6, start: 0, format: "Stick calibration data: {0:S}");

        buf_ = ReadSPI(0x60, (isLeft ? (byte)0x86 : (byte)0x98), 16);
        deadzone = (UInt16)((buf_[4] << 8) & 0xF00 | buf_[3]);

        buf_ = ReadSPI(0x80, 0x34, 10);
        gyr_neutral[0] = (Int16)(buf_[0] | ((buf_[1] << 8) & 0xff00));
        gyr_neutral[1] = (Int16)(buf_[2] | ((buf_[3] << 8) & 0xff00));
        gyr_neutral[2] = (Int16)(buf_[4] | ((buf_[5] << 8) & 0xff00));
        PrintArray(gyr_neutral, len: 3, d: DebugType.IMU, format: "User gyro neutral position: {0:S}");

        // This is an extremely messy way of checking to see whether there is user stick calibration data present, but I've seen conflicting user calibration data on blank Joy-Cons. Worth another look eventually.
        if (gyr_neutral[0] + gyr_neutral[1] + gyr_neutral[2] == -3 || Math.Abs(gyr_neutral[0]) > 100 || Math.Abs(gyr_neutral[1]) > 100 || Math.Abs(gyr_neutral[2]) > 100)
        {
            buf_ = ReadSPI(0x60, 0x29, 10);
            gyr_neutral[0] = (Int16)(buf_[3] | ((buf_[4] << 8) & 0xff00));
            gyr_neutral[1] = (Int16)(buf_[5] | ((buf_[6] << 8) & 0xff00));
            gyr_neutral[2] = (Int16)(buf_[7] | ((buf_[8] << 8) & 0xff00));
            PrintArray(gyr_neutral, len: 3, d: DebugType.IMU, format: "Factory gyro neutral position: {0:S}");
        }
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
