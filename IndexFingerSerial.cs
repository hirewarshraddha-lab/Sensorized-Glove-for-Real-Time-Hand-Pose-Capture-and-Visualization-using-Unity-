using System.IO.Ports;
using System.Threading;
using UnityEngine;

public class IndexFingerSerial : MonoBehaviour
{
    public Transform index1, index2, index3;

    [Header("Calibration")]
    public float adcMin = 2513f;
    public float adcMax = 2559f;
    public float angleMin = 0f;
    public float angleMax = 70f;

    // ── ADDED FROM CODE 2 ──────────────────────────────────────────
    [Header("Kalman Tuning")]
    public float kQ = 0.02f, kR = 1.5f;

    [Header("Velocity Assist")]
    public float velSmooth = 0.15f, velGain = 0.4f;

    // Kalman states for 3 joints {P, x}
    float[] kP = { 1, 1, 1 }, kX = { 0, 0, 0 };

    // Velocity tracking per joint
    float[] prev = { 0, 0, 0 }, vel = { 0, 0, 0 };

    
    float prevTime;
    float latestADC = -1f;        // ADD
    bool hasNewData = false;       // ADD
    Thread readThread;             // ADD
    bool running = false;          // ADD

    // ── END ADDED ──────────────────────────────────────────────────

    [Header("Debug")]
    public float rawADC = 0f;
    public float mappedAngle = 0f;
    public float filteredAngle = 0f;   // ADDED FROM CODE 2

    SerialPort port;
    Quaternion i1Init, i2Init, i3Init;

    // ── ADDED FROM CODE 2 ──────────────────────────────────────────
    float Kalman(int i, float z)
    {
        kP[i] += kQ;
        float K = kP[i] / (kP[i] + kR);
        kX[i] += K * (z - kX[i]);
        kP[i] *= 1f - K;
        return kX[i];
    }
    // ── END ADDED ──────────────────────────────────────────────────

    void Start()
    {
        adcMin = 2513; adcMax = 2559;
        angleMin = 0f; angleMax = 70f;
        rawADC = 0f; mappedAngle = 0f;
        prevTime = Time.time;

        i1Init = index1.localRotation;
        i2Init = index2.localRotation;
        i3Init = index3.localRotation;

        try
        {
            port = new SerialPort("COM8", 115200);
            port.ReadTimeout = 100;
            port.DtrEnable = false;
            port.RtsEnable = false;
            port.Handshake = Handshake.None;  // NEW
            port.Open();
            Debug.Log("COM8 opened OK");

            running = true;
            readThread = new Thread(ReadLoop);
            readThread.IsBackground = true;
            readThread.Start();               // NEW — reads on background thread
        }
        catch (System.Exception e)
        {
            Debug.LogError("COM8 failed: " + e.Message);
        }
    }

    // ADD this entire new method
    void ReadLoop()
    {
        while (running && port != null && port.IsOpen)
        {
            try
            {
                string line = port.ReadLine().Trim();
                if (float.TryParse(line, out float val))
                {
                    lock (this) { latestADC = val; hasNewData = true; }
                }
            }
            catch (System.TimeoutException) { }
            catch (System.Exception) { break; }
        }
    }

    void Update()
    {
        // Guard — grab latest value from thread safely
        float adc = -1f;
        lock (this)
        {
            if (!hasNewData) return;
            adc = latestADC;
            hasNewData = false;
        }

        rawADC = adc;
        float dt = Mathf.Max(Time.time - prevTime, 0.001f);
        prevTime = Time.time;

        // Step 1 — Normalize ADC to 0.0-1.0
        float normalized = Mathf.InverseLerp(adcMin, adcMax, adc);

        // Step 2 — Scale to full 200° budget
        mappedAngle = normalized * 200f;

        // Step 3 — Sequential bending (NO flip line)
        // MCP: 0→40°
        float mcpAngle = Mathf.Clamp(mappedAngle, 0f, 40f);

        // PIP: starts after MCP hits 40°
        float pipAngle = 0f;
        if (mappedAngle > 40f)
        {
            pipAngle = Mathf.Clamp(mappedAngle - 40f, 0f, 90f);
        }

        // DIP: starts after PIP hits 90° (40+90=130)
        float dipAngle = 0f;
        if (mappedAngle > 130f)
        {
            dipAngle = Mathf.Clamp(mappedAngle - 130f, 0f, 70f);
        }

        float[] raw = { mcpAngle, pipAngle, dipAngle };

        float[] final = new float[3];

        for (int i = 0; i < 3; i++)
        {
            float f = Kalman(i, raw[i]);
            float rv = (f - prev[i]) / dt;
            vel[i] = Mathf.Lerp(vel[i], rv, velSmooth);
            final[i] = f + vel[i] * velGain * dt;
            prev[i] = f;
        }

        filteredAngle = kX[0];

        
        index1.localRotation = i1Init * Quaternion.Euler(0, 0, -final[0]);
        index2.localRotation = i2Init * Quaternion.Euler(0, 0, -final[1]);
        index3.localRotation = i3Init * Quaternion.Euler(0, 0, -final[2]);
    }

    // REPLACE OnApplicationQuit with these two
    void OnDisable()
    {
        running = false;
        if (readThread != null) readThread.Join(200);
        if (port != null && port.IsOpen)
        {
            port.DiscardInBuffer();
            port.Close();
            port.Dispose();
            port = null;
        }
    }

    void OnApplicationQuit() => OnDisable();

    
}