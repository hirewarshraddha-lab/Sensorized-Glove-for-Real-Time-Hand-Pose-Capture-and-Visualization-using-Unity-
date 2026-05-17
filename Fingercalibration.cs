using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class FingerCalibration : MonoBehaviour
{
    [Header("Source — drag HandRoot here")]
    public IndexFingerSerial fingerSerial;

    [Header("Settings")]
    public int readingsPerPose = 5;
    public float delayBetweenReadings = 0.15f;

    enum State { WaitingForPort, WaitingFlat, SamplingFlat, WaitingBent, SamplingBent, Done }
    State _state = State.WaitingForPort;

    List<float> _flatReadings = new List<float>();
    List<float> _bentReadings = new List<float>();

    // ── START ─────────────────────────────────────────────────────────
    void Start()
    {
        // Log immediately so we know Start() ran
        Debug.Log("[Calib] Start() called");

        if (fingerSerial == null)
        {
            Debug.LogError("[Calib] FINGER SERIAL IS NULL — drag HandRoot into the slot!");
            return;
        }

        Debug.Log("[Calib] fingerSerial found: " + fingerSerial.name);
        Debug.Log("[Calib] Current rawADC = " + fingerSerial.rawADC);
        Debug.Log("[Calib] Waiting for port to open...");

        StartCoroutine(WaitForPort());
    }

    // ── WAIT FOR PORT ─────────────────────────────────────────────────
    IEnumerator WaitForPort()
    {
        Debug.Log("[Calib] WaitForPort coroutine started");

        int tries = 0;
        // Wait up to 30 seconds for rawADC to become positive
        while (fingerSerial.rawADC <= 0f && tries < 150)
        {
            tries++;
            // Log every 5 checks so you can see it's alive
            if (tries % 5 == 0)
                Debug.Log("[Calib] Still waiting... rawADC=" + fingerSerial.rawADC
                        + "  attempt " + tries + "/150");
            yield return new WaitForSeconds(0.2f);
        }

        if (fingerSerial.rawADC <= 0f)
        {
            Debug.LogError("[Calib] Timed out waiting for rawADC. "
                         + "Check serial port and ESP32 connection.");
            yield break;
        }

        Debug.Log("[Calib] Port ready! rawADC = " + fingerSerial.rawADC.ToString("F0"));
        Debug.Log("=== FINGER CALIBRATION ===");
        Debug.Log("STEP 1/2 — Straighten finger FLAT, hold still → press SPACE");
        _state = State.WaitingFlat;
    }

    // ── UPDATE ────────────────────────────────────────────────────────
    void Update()
    {
        if (Keyboard.current == null) return;
        bool spacePressed = Keyboard.current.spaceKey.wasPressedThisFrame;

        if (!spacePressed) return;

        Debug.Log("[Calib] SPACE pressed — current state: " + _state);

        if (_state == State.WaitingFlat)
        {
            _state = State.SamplingFlat;
            _flatReadings.Clear();
            Debug.Log("[Calib] Sampling FLAT pose — hold still...");
            StartCoroutine(SamplePose(_flatReadings, OnFlatDone));
        }
        else if (_state == State.WaitingBent)
        {
            _state = State.SamplingBent;
            _bentReadings.Clear();
            Debug.Log("[Calib] Sampling BENT pose — hold still...");
            StartCoroutine(SamplePose(_bentReadings, OnBentDone));
        }
        else
        {
            Debug.Log("[Calib] Space pressed but state is " + _state + " — ignoring");
        }
    }

    // ── SAMPLE POSE ───────────────────────────────────────────────────
    IEnumerator SamplePose(List<float> results, System.Action onDone)
    {
        Debug.Log("[Calib] SamplePose started");
        yield return new WaitForSeconds(0.2f);

        for (int i = 0; i < readingsPerPose; i++)
        {
            float last = fingerSerial.rawADC;
            float elapsed = 0f;

            // Wait up to 1 second for a new reading
            while (fingerSerial.rawADC == last && elapsed < 1f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // If value didn't change, just use whatever we have
            float val = fingerSerial.rawADC;
            results.Add(val);
            Debug.Log("[Calib]   Sample " + (i + 1) + "/" + readingsPerPose
                    + "  →  ADC: " + val.ToString("F0"));

            yield return new WaitForSeconds(delayBetweenReadings);
        }

        Debug.Log("[Calib] SamplePose complete — " + results.Count + " readings");
        onDone?.Invoke();
    }

    // ── CALLBACKS ─────────────────────────────────────────────────────
    void OnFlatDone()
    {
        float avg = Avg(_flatReadings);
        Debug.Log("[Calib] ✓ Flat average ADC = " + avg.ToString("F1"));
        Debug.Log("STEP 2/2 — Bend finger FULLY, hold still → press SPACE");
        _state = State.WaitingBent;
    }

    void OnBentDone()
    {
        float flatAvg = Avg(_flatReadings);
        float bentAvg = Avg(_bentReadings);
        Debug.Log("[Calib] ✓ Bent average ADC = " + bentAvg.ToString("F1"));

        float finalMin = Mathf.Min(flatAvg, bentAvg);
        float finalMax = Mathf.Max(flatAvg, bentAvg);

        float pad = (finalMax - finalMin) * 0.05f;
        finalMin -= pad;
        finalMax += pad;

        Debug.Log("[Calib] Calling AllowTracking(" + finalMin.ToString("F1")
                + ", " + finalMax.ToString("F1") + ")");

        fingerSerial.AllowTracking(finalMin, finalMax);

        _state = State.Done;

        Debug.Log("=== CALIBRATION COMPLETE ===");
        Debug.Log("  adcMin = " + finalMin.ToString("F1"));
        Debug.Log("  adcMax = " + finalMax.ToString("F1"));
        Debug.Log("  Finger tracking → ACTIVE");
        Debug.Log("  Graph Y range   → AUTO-UPDATED");
    }

    float Avg(List<float> vals)
    {
        float s = 0f;
        foreach (float v in vals) s += v;
        return s / vals.Count;
    }
}
