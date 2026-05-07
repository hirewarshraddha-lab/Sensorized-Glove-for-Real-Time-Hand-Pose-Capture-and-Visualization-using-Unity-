using System;
using System.IO.Ports;
using UnityEngine;

public class SerialReader : MonoBehaviour
{
    SerialPort serialPort;

    // ====== Bone References ======
    public Transform thumb1, thumb2, thumb3;
    public Transform index1, index2, index3;
    public Transform middle1, middle2, middle3;
    public Transform ring1, ring2, ring3;
    public Transform pinky1, pinky2, pinky3;

    // ====== Store Initial Rotations ======
    Quaternion t1Init, t2Init, t3Init;
    Quaternion i1Init, i2Init, i3Init;
    Quaternion m1Init, m2Init, m3Init;
    Quaternion r1Init, r2Init, r3Init;
    Quaternion p1Init, p2Init, p3Init;

    void Start()
    {
        try
        {
            serialPort = new SerialPort("COM4", 115200);
            serialPort.ReadTimeout = 500;
            serialPort.Open();

            Debug.Log("Serial Port Opened Successfully");
        }
        catch (Exception e)
        {
            Debug.LogError("Serial Error: " + e.Message);
        }

        // Store original rotations
        t1Init = thumb1.localRotation;
        t2Init = thumb2.localRotation;
        t3Init = thumb3.localRotation;

        i1Init = index1.localRotation;
        i2Init = index2.localRotation;
        i3Init = index3.localRotation;

        m1Init = middle1.localRotation;
        m2Init = middle2.localRotation;
        m3Init = middle3.localRotation;

        r1Init = ring1.localRotation;
        r2Init = ring2.localRotation;
        r3Init = ring3.localRotation;

        p1Init = pinky1.localRotation;
        p2Init = pinky2.localRotation;
        p3Init = pinky3.localRotation;
    }

    void Update()
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            try
            {
                if (serialPort.BytesToRead > 0)
                {
                    string data = serialPort.ReadExisting();

                    if (data.StartsWith("S"))
                    {
                        string[] values = data.Split(',');

                        if (values.Length >= 16)
                        {
                            
                            float t1 = Mathf.Clamp(float.Parse(values[1]), 0f, 50f);
                            float t2 = Mathf.Clamp(float.Parse(values[2]), 0f, 60f);
                            float t3 = Mathf.Clamp(float.Parse(values[3]), 0f, 70f);

                            float i1 = Mathf.Clamp(float.Parse(values[4]), 0f, 70f);
                            float i2 = Mathf.Clamp(float.Parse(values[5]), 0f, 90f);
                            float i3 = Mathf.Clamp(float.Parse(values[6]), 0f, 70f);


                            float m1 = Mathf.Clamp(float.Parse(values[7]), 0f, 70f);
                            float m2 = Mathf.Clamp(float.Parse(values[8]), 0f, 90f);
                            float m3 = Mathf.Clamp(float.Parse(values[9]), 0f, 70f);

                           
                            float r1 = Mathf.Clamp(float.Parse(values[10]), 0f, 70f);
                            float r2 = Mathf.Clamp(float.Parse(values[11]), 0f, 90f);
                            float r3 = Mathf.Clamp(float.Parse(values[12]), 0f, 70f);

                            float p1 = Mathf.Clamp(float.Parse(values[13]), 0f, 70f);
                            float p2 = Mathf.Clamp(float.Parse(values[14]), 0f, 90f);
                            float p3 = Mathf.Clamp(float.Parse(values[15]), 0f, 70f);


                            // Apply relative rotation
                            thumb1.localRotation = t1Init * Quaternion.Euler(-t1 * 0.25f, 0, -t1 * 0.5f);
                            thumb2.localRotation = t2Init * Quaternion.Euler(-t2 * 0.2f, 0, -t2 * 0.7f);
                            thumb3.localRotation = t3Init * Quaternion.Euler(0, 0, -t3 * 0.6f);


                            index1.localRotation = i1Init * Quaternion.Euler(0, 0, -i1);
                            index2.localRotation = i2Init * Quaternion.Euler(0, 0, -i2 * 0.7f);
                            index3.localRotation = i3Init * Quaternion.Euler(0, 0, -i3 * 0.4f);

                     
                            middle1.localRotation = m1Init * Quaternion.Euler(0, 0, -m1);
                            middle2.localRotation = m2Init * Quaternion.Euler(0, 0, -m2 * 0.7f);
                            middle3.localRotation = m3Init * Quaternion.Euler(0, 0, -m3 * 0.4f);

                            
                            ring1.localRotation = r1Init * Quaternion.Euler(0, 0, -r1);
                            ring2.localRotation = r2Init * Quaternion.Euler(0, 0, -r2 * 0.7f);
                            ring3.localRotation = r3Init * Quaternion.Euler(0, 0, -r3 * 0.4f);

                            
                            pinky1.localRotation = p1Init * Quaternion.Euler(0, 0, -p1);
                            pinky2.localRotation = p2Init * Quaternion.Euler(0, 0, -p2 * 0.75f);
                            pinky3.localRotation = p3Init * Quaternion.Euler(0, 0, -p3 * 0.5f);

                        }
                    }
                }
            }
            catch { }
        }
    }

    void OnApplicationQuit()
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
        }
    }
}