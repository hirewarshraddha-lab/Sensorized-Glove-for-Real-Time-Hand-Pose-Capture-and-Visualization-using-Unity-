using UnityEngine;

public enum FingerId { Thumb = 0, Index = 1, Middle = 2, Ring = 3, Pinky = 4 }

public enum FingerFilterMode { RawData, MovingAverage, VelocityOnly, VelocityAssist }

[System.Serializable]
public class FilterState
{
    [System.NonSerialized] public float[] maBuf;
    [System.NonSerialized] public int maHead;
    [System.NonSerialized] public int maCount;
    [System.NonSerialized] public float prev;
    [System.NonSerialized] public float vel;

    public void Reset()
    {
        maBuf = new float[32];
        maHead = 0;
        maCount = 0;
        prev = 0f;
        vel = 0f;
    }
}

[System.Serializable]
public class FingerState
{
    [Header("Joint Transforms (proximal → distal)")]
    public Transform mcpBone;
    public Transform pipBone;
    public Transform dipBone;

    [Header("Calibration  ← auto-set by FingerCalibration.cs")]
    public float adcMin = 3800f;
    public float adcMax = 4000f;

    [Header("Circular-Grasp Ratios — Table 4, intrafinger constraints")]
    public float flexionMax = 50f;    // ° at normalized = 1  (this finger's PIP-equivalent range)
    public float mcpRatio = 4f / 3f;  // MCP = mcpRatio * flexionAngle   (θ_MCP = (4/3)·θ_PIP)
    public float dipRatio = 3f / 2f;  // DIP = dipRatio * flexionAngle   (θ_DIP = (3/2)·θ_PIP)
    public float mcpClamp = 90f;
    public float dipClamp = 135f;

    [Header("Debug — every pipeline stage, exposed for graphing")]
    public float rawADC;
    public float filteredADC;
    public float normalized;
    public float flexionAngle;
    public float filteredAngle;  // == finalMCP, kept for the existing grapher
    public float finalMCP;
    public float finalPIP;
    public float finalDIP;

    [System.NonSerialized] public FilterState filter;
    [System.NonSerialized] public Quaternion mcpInit, pipInit, dipInit;

    public void ResetState()
    {
        filter = new FilterState();
        filter.Reset();
    }
}
