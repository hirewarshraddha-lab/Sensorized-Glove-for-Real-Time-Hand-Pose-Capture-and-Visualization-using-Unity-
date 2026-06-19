


using System.Collections;
using System.IO.Ports;
using System.Threading;
using UnityEngine;



public class IndexFingerSerial : MonoBehaviour
{
    
    public Transform index1, index2, index3;

    [Header("Calibration  ← auto-set by FingerCalibration.cs")]
    public float adcMin = 3800f;
    public float adcMax = 4000f;
    public float angleMin = 0f;
    public float angleMax = 70f;


    [Header("Moving Average Filter Tuning")]
    [Range(2, 32)]
    public int maWindow = 3;


    public enum FilterMode
    {
        RawData,
        MovingAverage,
        VelocityOnly,
        VelocityAssist
    }

    [Header("Filter Mode")]
    public FilterMode mode = FilterMode.RawData;


    // ── MOVING AVERAGE STATE (per joint)
    // Circular buffers — one per joint (MCP, PIP, DIP)
    float[][] maBuf = { new float[32], new float[32], new float[32] };
    int[] maHead = { 0, 0, 0 };   // write index inside circular buffer
    int[] maCount = { 0, 0, 0 };   // how many samples filled so far (ramps up to maWindow)

 
    // velSmooth:  (lower=faster)
    // velGain:  (higher=less lag on fast moves)
    [Header("Velocity Assist  ← reduces return lag")]
    public float velSmooth = 0.1f;
    public float velGain = 0.6f;

    // ── VELOCITY STATE (per joint)
    float[] prev = { 0, 0, 0 };
    float[] vel = { 0, 0, 0 };

    // ── SERIAL / THREAD 
    float prevTime;
    float latestADC = -1f;
    bool hasNewData = false;
    Thread readThread;
    bool running = false;

    // ── CALIBRATION GATE ─────────────────────────────────────────────────────

    bool trackingAllowed = false;

    // ── DEBUG ────────────────────────────────────────────────────────────────
    [Header("Debug  ← live read-only values")]
    public float rawADC = 0f;
    public float mappedAngle = 0f;
    public float filteredAngle = 0f;

    // ── PORT ─────────────────────────────────────────────────────────────────
    [Header("Serial Port")]
    public string portName = "COM8";
    public int maxAttempts = 5;

    SerialPort port;
    Quaternion i1Init, i2Init, i3Init;


    public void AllowTracking(float calibratedMin, float calibratedMax)
    {
        adcMin = calibratedMin;
        adcMax = calibratedMax;
        trackingAllowed = true;
        Debug.Log("[IndexFingerSerial] Tracking UNLOCKED — adcMin="
                + adcMin.ToString("F1") + "  adcMax=" + adcMax.ToString("F1"));
    }


    
    float MovingAverage(int i, float z)
    {
        int w = Mathf.Clamp(maWindow, 2, 32);   // safe window size

        // Write new sample into circular buffer
        maBuf[i][maHead[i]] = z;
        maHead[i] = (maHead[i] + 1) % w;        // advance write pointer
        if (maCount[i] < w) maCount[i]++;        // ramp up until buffer is full

        // Compute mean over the filled portion
        float sum = 0f;
        for (int s = 0; s < maCount[i]; s++)
            sum += maBuf[i][s];
        return sum / maCount[i];
    }


    void Start()
    {
        rawADC = 0f;
        mappedAngle = 0f;
        prevTime = Time.time;

        i1Init = index1.localRotation;
        i2Init = index2.localRotation;
        i3Init = index3.localRotation;

        StartCoroutine(OpenPortWithRetry());
    }



    IEnumerator OpenPortWithRetry()
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            bool success = false;
            string errorMsg = "";

            try
            {
                port = new SerialPort(portName, 115200);
                port.ReadTimeout = 100;
                port.DtrEnable = false;
                port.RtsEnable = false;
                port.Handshake = Handshake.None;
                port.Open();
                success = true;
            }
            catch (System.Exception e)
            {
                errorMsg = e.Message;
                if (port != null)
                {
                    try { port.Close(); port.Dispose(); } catch { }
                    port = null;
                }
            }

            if (success)
            {
                Debug.Log("[IndexFingerSerial] " + portName
                        + " opened OK on attempt " + attempt);
                running = true;
                readThread = new Thread(ReadLoop);
                readThread.IsBackground = true;
                readThread.Start();
                yield break;
            }
            else
            {
                Debug.LogWarning("[IndexFingerSerial] attempt " + attempt
                               + "/" + maxAttempts + " failed: " + errorMsg);
                if (attempt < maxAttempts)
                {
                    Debug.Log("[IndexFingerSerial] Retrying in 1 second...");
                    yield return new WaitForSeconds(1f);
                }
                else
                {
                    Debug.LogError("[IndexFingerSerial] Failed after "
                        + maxAttempts + " attempts. Unplug/replug ESP32.");
                }
            }
        }
    }


    
    void ReadLoop()
    {
        while (running && port != null && port.IsOpen)
        {
            try
            {
                string line = port.ReadLine().Trim();
                if (float.TryParse(line, out float val))
                    lock (this) { latestADC = val; hasNewData = true; }
            }
            catch (System.TimeoutException) { }
            catch (System.Exception) { break; }
        }
    }

    void Update()
    {
        // Grab latest ADC from background thread safely
        float adc = -1f;
        lock (this)
        {
            if (!hasNewData) return;
            adc = latestADC;
            hasNewData = false;
        }

        // Always update rawADC — calibration script needs this
        rawADC = adc;

        // Finger frozen until calibration calls AllowTracking()
        if (!trackingAllowed) return;

        float dt = Mathf.Max(Time.time - prevTime, 0.001f);
        prevTime = Time.time;

        // Step 1 — normalize ADC to 0.0–1.0 using calibrated range
        float normalized = Mathf.InverseLerp(adcMin, adcMax, adc);

        // Step 2 — scale to 200° total budget (MCP40 + PIP90 + DIP70)
        mappedAngle = normalized * 200f;

        // Step 3 — sequential bending: each joint activates after previous maxes
        float mcpAngle = Mathf.Clamp(mappedAngle, 0f, 40f);

        float pipAngle = 0f;
        if (mappedAngle > 40f)
            pipAngle = Mathf.Clamp(mappedAngle - 40f, 0f, 90f);

        float dipAngle = 0f;
        if (mappedAngle > 130f)
            dipAngle = Mathf.Clamp(mappedAngle - 130f, 0f, 70f);

        float[] raw = { mcpAngle, pipAngle, dipAngle };
        float[] final = new float[3];

        // Step 4 — Moving Average + velocity assist per joint

        for (int i = 0; i < 3; i++)
        {
            switch (mode)
            {
                case FilterMode.RawData:

                    final[i] = raw[i];
                    prev[i] = raw[i];
                    vel[i] = 0f;
                    break;

                case FilterMode.MovingAverage:

                    final[i] = MovingAverage(i, raw[i]);
                    prev[i] = final[i];
                    vel[i] = 0f;
                    break;

                case FilterMode.VelocityOnly:

                    float f1 = MovingAverage(i, raw[i]);
                    float rv1 = (f1 - prev[i]) / dt;
                    vel[i] = Mathf.Lerp(vel[i], rv1, velSmooth);

                    final[i] = f1 + vel[i] * dt;
                    prev[i] = f1;
                    break;

                case FilterMode.VelocityAssist:

                    float f2 = MovingAverage(i, raw[i]);
                    float rv2 = (f2 - prev[i]) / dt;
                    vel[i] = Mathf.Lerp(vel[i], rv2, velSmooth);

                    float nudge = vel[i] * velGain * dt;
                    final[i] = f2 + nudge;
                    prev[i] = f2;
                    break;
            }
        }

        
        filteredAngle = final[0];   // expose MCP filtered angle for graph

        // Step 5 — apply to finger bones
        index1.localRotation = i1Init * Quaternion.Euler(0, 0, -final[0]);
        index2.localRotation = i2Init * Quaternion.Euler(0, 0, -final[1]);
        index3.localRotation = i3Init * Quaternion.Euler(0, 0, -final[2]);
    }


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