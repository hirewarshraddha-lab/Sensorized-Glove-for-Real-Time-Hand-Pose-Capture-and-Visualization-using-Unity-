using System.Collections;
using System.IO.Ports;
using System.Threading;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;


//Serial Thread = rawADC[5]            (Module 2)
// Filter Mode = filteredADC[5]         (Module 3)
// Calibration = normalized[5]          (Module 4)
// Finger Flexion = flexionAngle[5]     (Module 5)
//Circular-Grasp Model = MCP/PIP/DIP   (Module 6)
//Bone Rotation                        (Module 7)

public class HandFingerSerial : MonoBehaviour
{
    const int FINGER_COUNT = 5;

    [Header("── Thumb ──")]
    public FingerState thumb = new FingerState();
    [Header("── Index ──")]
    public FingerState indexFinger = new FingerState();
    [Header("── Middle ──")]
    public FingerState middle = new FingerState();
    [Header("── Ring ──")]
    public FingerState ring = new FingerState();
    [Header("── Pinky ──")]
    public FingerState pinky = new FingerState();

    FingerState[] fingers; 

    [Header("Filter Mode (shared by all fingers)")]
    public FingerFilterMode mode = FingerFilterMode.RawData;

    [Header("Moving Average Filter Tuning (shared by all fingers)")]
    [Range(2, 32)]
    public int maWindow = 3;

    // velSmooth:  (lower=faster)
    // velGain:  (higher=less lag on fast moves)
    [Header("Velocity Assist  ← reduces return lag (shared by all fingers)")]
    public float velSmooth = 0.1f;
    public float velGain = 0.6f;

    float prevTime;
    float[] latestADC = new float[FINGER_COUNT];
    bool hasNewData = false;
    Thread readThread;
    bool running = false;

    // CALIBRATION
    bool trackingAllowed = false;

    //PORT 
    [Header("Serial Port")]
    public string portName = "COM8";
    public int maxAttempts = 5;

    SerialPort port;

    //CALIBRATION 
    public void SetFingerCalibration(FingerId finger, float calibratedMin, float calibratedMax)
    {
        int i = (int)finger;
        fingers[i].adcMin = calibratedMin;
        fingers[i].adcMax = calibratedMax;
        Debug.Log("[HandFingerSerial] " + finger + " calibrated — min="
                + calibratedMin.ToString("F1") + "  max=" + calibratedMax.ToString("F1"));
    }

    public void EnableTracking()
    {
        trackingAllowed = true;
        Debug.Log("[HandFingerSerial] Tracking UNLOCKED for all 5 fingers.");
    }

    public void AllowTracking(float[] calibratedMins, float[] calibratedMaxes)
    {
        if (calibratedMins.Length != FINGER_COUNT || calibratedMaxes.Length != FINGER_COUNT)
        {
            Debug.LogError("[HandFingerSerial] AllowTracking needs exactly " + FINGER_COUNT + " mins/maxes.");
            return;
        }
        for (int i = 0; i < FINGER_COUNT; i++)
        {
            fingers[i].adcMin = calibratedMins[i];
            fingers[i].adcMax = calibratedMaxes[i];
        }
        EnableTracking();
    }

    public FingerState GetFinger(FingerId finger) => fingers[(int)finger];

    void Awake()
    {
        fingers = new FingerState[] { thumb, indexFinger, middle, ring, pinky };
    }

    void Start()
    {
        prevTime = Time.time;

        for (int i = 0; i < FINGER_COUNT; i++)
        {
            FingerState f = fingers[i];
            f.ResetState();
            f.rawADC = 0f;

            if (f.mcpBone != null) f.mcpInit = f.mcpBone.localRotation;
            if (f.pipBone != null) f.pipInit = f.pipBone.localRotation;
            if (f.dipBone != null) f.dipInit = f.dipBone.localRotation;
        }

        StartCoroutine(OpenPortWithRetry());
    }

    // MODULE 2 — SERIAL COMMUNICATION 

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
                Debug.Log("[HandFingerSerial] " + portName
                        + " opened OK on attempt " + attempt);
                running = true;
                readThread = new Thread(ReadLoop);
                readThread.IsBackground = true;
                readThread.Start();
                yield break;
            }
            else
            {
                Debug.LogWarning("[HandFingerSerial] attempt " + attempt
                               + "/" + maxAttempts + " failed: " + errorMsg);
                if (attempt < maxAttempts)
                {
                    Debug.Log("[HandFingerSerial] Retrying in 1 second...");
                    yield return new WaitForSeconds(1f);
                }
                else
                {
                    Debug.LogError("[HandFingerSerial] Failed after "
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
                string[] tokens = line.Split(',');

                if (tokens.Length != FINGER_COUNT) continue; // malformed

                float[] parsed = new float[FINGER_COUNT];
                bool ok = true;
                for (int i = 0; i < FINGER_COUNT; i++)
                {
                    if (!float.TryParse(tokens[i], out parsed[i])) { ok = false; break; }
                }

                if (ok)
                {
                    lock (this)
                    {
                        for (int i = 0; i < FINGER_COUNT; i++) latestADC[i] = parsed[i];
                        hasNewData = true;
                    }
                }
            }
            catch (System.TimeoutException) { }
            catch (System.Exception) { break; }
        }
    }

    void Update()
    {
        
        float[] adc = new float[FINGER_COUNT];
        lock (this)
        {
            if (!hasNewData) return;
            System.Array.Copy(latestADC, adc, FINGER_COUNT);
            hasNewData = false;
        }

        for (int i = 0; i < FINGER_COUNT; i++)
            fingers[i].rawADC = adc[i];

        if (!trackingAllowed) return;

        float dt = Mathf.Max(Time.time - prevTime, 0.001f);
        prevTime = Time.time;

        for (int i = 0; i < FINGER_COUNT; i++)
            Pipeline(fingers[i], adc[i], dt);
    }

    // Runs Modules 3–7 for ONE finger. Every finger calls this with its own
    
    void Pipeline(FingerState f, float rawADC, float dt)
    {
        // Module 3 — Filtering  (rawADC → filteredADC)
        f.filteredADC = ApplyFilter(f.filter, rawADC, dt);

        // Module 4 — Calibration  (filteredADC → normalized)
        f.normalized = Normalize(f.filteredADC, f.adcMin, f.adcMax);

        // Module 5 — Finger Flexion  (normalized → flexionAngle)
        f.flexionAngle = f.normalized * f.flexionMax;

        // Module 6 — Circular-Grasp Model  (flexionAngle → MCP / PIP / DIP
        float pip = f.flexionAngle;
        float mcp = Mathf.Clamp(f.mcpRatio * pip, 0f, f.mcpClamp);
        float dip = Mathf.Clamp(f.dipRatio * pip, 0f, f.dipClamp);

        f.filteredAngle = mcp; 
        f.finalMCP = mcp;
        f.finalPIP = pip;
        f.finalDIP = dip;

        
        if (f.mcpBone != null) f.mcpBone.localRotation = f.mcpInit * Quaternion.Euler(0, 0, -mcp);
        if (f.pipBone != null) f.pipBone.localRotation = f.pipInit * Quaternion.Euler(0, 0, -pip);
        if (f.dipBone != null) f.dipBone.localRotation = f.dipInit * Quaternion.Euler(0, 0, -dip);
    }

  

    float ApplyFilter(FilterState fs, float raw, float dt)
    {
        float result;

        switch (mode)
        {
            case FingerFilterMode.RawData:
                result = raw;
                fs.vel = 0f;
                break;

            case FingerFilterMode.MovingAverage:
                result = MovingAverage(fs, raw);
                fs.vel = 0f;
                break;

            case FingerFilterMode.VelocityOnly:
                {
                    float ma = MovingAverage(fs, raw);
                    float rv = (ma - fs.prev) / dt;
                    fs.vel = Mathf.Lerp(fs.vel, rv, velSmooth);
                    result = ma + fs.vel * dt;
                    break;
                }

            case FingerFilterMode.VelocityAssist:
                {
                    float ma = MovingAverage(fs, raw);
                    float rv = (ma - fs.prev) / dt;
                    fs.vel = Mathf.Lerp(fs.vel, rv, velSmooth);
                    float nudge = fs.vel * velGain * dt;
                    result = ma + nudge;
                    break;
                }

            default:
                result = raw;
                break;
        }

        fs.prev = result; 
        return result;
    }

    float MovingAverage(FilterState fs, float z)
    {
        int w = Mathf.Clamp(maWindow, 2, 32);   // safe window size

  
        fs.maBuf[fs.maHead] = z;
        fs.maHead = (fs.maHead + 1) % w;        
        if (fs.maCount < w) fs.maCount++;       

        // Compute mean over the filled portion
        float sum = 0f;
        for (int s = 0; s < fs.maCount; s++)
            sum += fs.maBuf[s];
        return sum / fs.maCount;
    }

    // MODULE 4 — CALIBRATION   (filteredADC → normalized)

    float Normalize(float filteredADC, float adcMin, float adcMax)
    {
        return Mathf.InverseLerp(adcMin, adcMax, filteredADC);
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
