using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using static HandFingerSerial;

public class FingerCalibration : MonoBehaviour
{
    [Header("Source — drag HandRoot here (needs the HandFingerSerial component)")]
    public HandFingerSerial fingerSerial;

    [Header("Settings")]
    public int readingsPerPose = 5;
    public float delayBetweenReadings = 0.15f;

    enum State { WaitingForPort, WaitingFlat, SamplingFlat, WaitingBent, SamplingBent, Done }
    State _state = State.WaitingForPort;

    const int FINGER_COUNT = 5;

    static readonly FingerId[] Order =
    {
        FingerId.Thumb,
        FingerId.Index,
        FingerId.Middle,
        FingerId.Ring,
        FingerId.Pinky
    };

    List<float>[] _flatReadings = new List<float>[FINGER_COUNT];
    List<float>[] _bentReadings = new List<float>[FINGER_COUNT];

    void Awake()
    {
        for (int i = 0; i < FINGER_COUNT; i++)
        {
            _flatReadings[i] = new List<float>();
            _bentReadings[i] = new List<float>();
        }
    }

    void Start()
    {
        Debug.Log("[Calib] Start() called");

        if (fingerSerial == null)
        {
            Debug.LogError("[Calib] FINGER SERIAL IS NULL — drag HandRoot into the slot!");
            return;
        }

        Debug.Log("[Calib] fingerSerial found: " + fingerSerial.name);
        Debug.Log("[Calib] Current Index rawADC = " + fingerSerial.GetFinger(FingerId.Index).rawADC);
        Debug.Log("[Calib] Waiting for port to open...");

        StartCoroutine(WaitForPort());
    }

    IEnumerator WaitForPort()
    {
        Debug.Log("[Calib] WaitForPort coroutine started");

        int tries = 0;
        while (AnyFingerNotReady() && tries < 150)
        {
            tries++;
            if (tries % 5 == 0)
                Debug.Log("[Calib] Still waiting... attempt " + tries + "/150");
            yield return new WaitForSeconds(0.2f);
        }

        if (AnyFingerNotReady())
        {
            Debug.LogError("[Calib] Timed out waiting for rawADC. Check serial port and ESP32 connection.");
            for (int i = 0; i < FINGER_COUNT; i++)
            {
                float v = fingerSerial.GetFinger(Order[i]).rawADC;
                if (v <= 0f)
                    Debug.LogError("[Calib]   " + Order[i] + " never received data (rawADC=" + v.ToString("F0") + ") — check its wiring/pin.");
            }
            yield break;
        }

        Debug.Log("[Calib] Port ready! All 5 fingers reporting data.");
        Debug.Log("=== 5-FINGER CALIBRATION ===");
        Debug.Log("STEP 1/2 — Open hand FLAT, hold still → press SPACE");
        _state = State.WaitingFlat;
    }

    bool AnyFingerNotReady()
    {
        for (int i = 0; i < FINGER_COUNT; i++)
            if (fingerSerial.GetFinger(Order[i]).rawADC <= 0f) return true;
        return false;
    }

    void Update()
    {
        if (Keyboard.current == null) return;
        bool spacePressed = Keyboard.current.spaceKey.wasPressedThisFrame;

        if (!spacePressed) return;

        Debug.Log("[Calib] SPACE pressed — current state: " + _state);

        if (_state == State.WaitingFlat)
        {
            _state = State.SamplingFlat;
            for (int i = 0; i < FINGER_COUNT; i++) _flatReadings[i].Clear();
            Debug.Log("[Calib] Sampling FLAT pose — hold still...");
            StartCoroutine(SampleAllFingers(_flatReadings, OnFlatDone));
        }
        else if (_state == State.WaitingBent)
        {
            _state = State.SamplingBent;
            for (int i = 0; i < FINGER_COUNT; i++) _bentReadings[i].Clear();
            Debug.Log("[Calib] Sampling BENT pose — hold still...");
            StartCoroutine(SampleAllFingers(_bentReadings, OnBentDone));
        }
        else
        {
            Debug.Log("[Calib] Space pressed but state is " + _state + " — ignoring");
        }
    }

    IEnumerator SampleAllFingers(List<float>[] results, System.Action onDone)
    {
        Debug.Log("[Calib] SamplePose started");
        yield return new WaitForSeconds(0.2f);

        float[] last = new float[FINGER_COUNT];
        for (int i = 0; i < FINGER_COUNT; i++)
            last[i] = fingerSerial.GetFinger(Order[i]).rawADC;

        for (int r = 0; r < readingsPerPose; r++)
        {
            float elapsed = 0f;

            while (elapsed < 1f)
            {
                bool anyChanged = false;
                for (int i = 0; i < FINGER_COUNT; i++)
                {
                    if (fingerSerial.GetFinger(Order[i]).rawADC != last[i]) { anyChanged = true; break; }
                }
                if (anyChanged) break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            string line = "[Calib]   Sample " + (r + 1) + "/" + readingsPerPose + "  →  ";
            for (int i = 0; i < FINGER_COUNT; i++)
            {
                float val = fingerSerial.GetFinger(Order[i]).rawADC;
                results[i].Add(val);
                last[i] = val;
                line += Order[i] + ":" + val.ToString("F0") + "  ";
            }
            Debug.Log(line);

            yield return new WaitForSeconds(delayBetweenReadings);
        }

        Debug.Log("[Calib] SamplePose complete — " + results[0].Count + " readings per finger");
        onDone?.Invoke();
    }

    void OnFlatDone()
    {
        Debug.Log("[Calib] ✓ Flat averages:");
        for (int i = 0; i < FINGER_COUNT; i++)
            Debug.Log("[Calib]     " + Order[i] + " = " + Avg(_flatReadings[i]).ToString("F1"));

        Debug.Log("STEP 2/2 — Make a fist, bend fingers FULLY, hold still → press SPACE");
        _state = State.WaitingBent;
    }

    void OnBentDone()
    {
        float[] mins = new float[FINGER_COUNT];
        float[] maxes = new float[FINGER_COUNT];

        Debug.Log("[Calib] ✓ Bent averages:");
        for (int i = 0; i < FINGER_COUNT; i++)
        {
            float flatAvg = Avg(_flatReadings[i]);
            float bentAvg = Avg(_bentReadings[i]);
            Debug.Log("[Calib]     " + Order[i] + " = " + bentAvg.ToString("F1"));

            float finMin = Mathf.Min(flatAvg, bentAvg);
            float finMax = Mathf.Max(flatAvg, bentAvg);
            float pad = (finMax - finMin) * 0.05f;

            mins[i] = finMin - pad;
            maxes[i] = finMax + pad;
        }

        Debug.Log("[Calib] Calling AllowTracking() for all 5 fingers...");
        fingerSerial.AllowTracking(mins, maxes);

        _state = State.Done;

        Debug.Log("=== CALIBRATION COMPLETE ===");
        for (int i = 0; i < FINGER_COUNT; i++)
            Debug.Log("  " + Order[i] + ": adcMin=" + mins[i].ToString("F1") + "  adcMax=" + maxes[i].ToString("F1"));
        Debug.Log("  Finger tracking → ACTIVE");
    }

    float Avg(List<float> vals)
    {
        float s = 0f;
        foreach (float v in vals) s += v;
        return s / vals.Count;
    }
}
