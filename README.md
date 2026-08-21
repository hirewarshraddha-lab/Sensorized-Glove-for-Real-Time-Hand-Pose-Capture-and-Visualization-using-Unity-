# Sensorized-Glove-for-Real-Time-Hand-Pose-Capture-and-Visualization-using-Unity-
 Final Reports
Sensorized Glove – Real-Time Hand Pose Capture & Visualization

A Unity-based real-time hand tracking and visualization system developed for a sensorized glove project.
The system receives sensor values from an ESP32 through serial communication, performs filtering and manual calibration, converts the calibrated values into finger flexion angles, estimates the MCP, PIP, and DIP joint angles using an intrafinger circular-grasp relationship, and finally drives a rigged 3D hand model in Unity.

Main Files
1. FingerCalibration.cs
2. Fingerdatastructure.cs
3. HandFingerSerial.cs

Project Overview
The software pipeline is:
1. ESP32 Sensor Data
2. Serial Communication
3. Raw ADC Values
4. Filtering
5. Manual Calibration
6. Normalized Value
7. Finger Flexion Angle
8. MCP / PIP / DIP Estimation
9. 3D Hand Bone Rotation

The current implementation tracks five fingers:
Thumb
Index
Middle
Ring
Pinky
Each finger has an independent FingerState containing its sensor data, calibration parameters, filter state, joint-angle values, and corresponding Unity bone transforms.
Repository Structure
Sensorized-Glove/
│
├── FingerCalibration.cs
├── Fingerdatastructure.cs
├── HandFingerSerial.cs
│
├── Hand Model/
│   └── Rigged 3D Hand Model
│
└── README.md



Main Files
1. FingerCalibration.cs
FingerCalibration.cs implements the manual calibration procedure for all five fingers.
Its purpose is to determine the usable ADC range of each finger sensor before tracking is enabled.
Calibration Process
The calibration follows two poses:
STEP 1
Open / Flat Hand
      ↓
Press SPACE
      ↓
5 sensor readings collected

STEP 2
Fully Bent / Make a Fist
      ↓
Press SPACE
      ↓
5 sensor readings collected

      ↓
Calculate calibration range
      ↓
Enable finger tracking

# Calibration States
The script uses the following internal states:
WaitingForPort
WaitingFlat
SamplingFlat
WaitingBent
SamplingBent
Done

Before calibration starts, the script waits until all five fingers are receiving valid raw ADC data.
# Calibration Sampling
The default calibration settings are:
readingsPerPose = 5;
delayBetweenReadings = 0.15f;
For each pose, five readings are collected for every finger.
The average of the readings is calculated:
Flat Average
Bent Average
The minimum and maximum values are then determined from these two averages.
These values are passed to HandFingerSerial using:
AllowTracking(mins, maxes);
Tracking is then unlocked for all five fingers.
2. Fingerdatastructure.cs
Fingerdatastructure.cs contains the core data definitions used by the tracking system.
It contains:
FingerId
FingerFilterMode
FilterState
FingerState

1] FingerId
The five fingers are represented using:
public enum FingerId
{
    Thumb = 0,
    Index = 1,
    Middle = 2,
    Ring = 3,
    Pinky = 4
}
Therefore, the system internally uses an array of five finger states:
0 → Thumb
1 → Index
2 → Middle
3 → Ring
4 → Pinky

2] FingerFilterMode
The filtering system supports four modes:
RawData
MovingAverage
VelocityOnly
VelocityAssist

These modes can be selected from the Unity Inspector.
1. Raw Data
Uses the incoming ADC value directly.
Raw ADC → Output
No smoothing is applied.

 2. Moving Average
Applies a moving-average filter to reduce sensor noise and jitter.
Here, The window size can be configured between:
2 – 32 samples

The default window we are using is:
3 samples

3. Velocity Only
Uses the moving average together with estimated velocity.The velocity is calculated from the change in the filtered signal over time.

4. Velocity Assist
Uses the moving average and adds a velocity-based correction.This mode is intended to provide a small motion "nudge" based on the current signal velocity.The velocity-assist parameters are :
velSmooth
velGain
These can be adjusted from the Unity Inspector.


3] Filter State
FilterState stores the internal state required by the filtering system. It contains:
maBuf
maHead
maCount
prev
vel
The moving-average buffer has a maximum size of : 32 samples
Each finger has its own filter state.
4] Finger State
FingerState stores all information associated with an individual finger.
It includes:
# Unity Bone References

Mcp Bone
Pip Bone
Dip Bone
These represent the MCP, PIP, and DIP transforms of the corresponding finger.
# Calibration Parameters

adcMin
adcMax
These values are automatically updated by FingerCalibration.cs after calibration.
# Circular-Grasp Parameters

flexionMax
mcpRatio
dipRatio
mcpClamp
dipClamp


Default values:
flexionMax = 0°
mcpRatio   = 4/3
dipRatio     = 3/2
mcpClamp = 90°
dipClamp   = 135°

# Debug / Processing Values
The complete processing pipeline is exposed through:
Raw ADC
Filtered ADC
normalized
Flexion Angle
Filtered Angle
Final MCP
Final PIP
Final DIP
These values can also be used by graphing or debugging tools.








3. HandFingerSerial.cs
HandFingerSerial.cs is the main real-time tracking script.
It connects the ESP32 to Unity, receives the five sensor values, processes them, and controls the hand model.
It implements:
Serial communication
Data parsing
Filtering
Calibration normalization
Finger flexion calculation
Circular-grasp joint estimation
Unity bone rotation

Serial Communication
The default serial configuration is:
Baud Rate : 115200
Port : COM8
The port can be changed from the Unity Inspector. The script attempts to open the serial port multiple times if the initial connection fails.
Default : Maximum attempts = 5

Serial Data Format
The current implementation expects exactly 5 comma-separated numerical values per line.
Example:
3821, 3795, 3810, 3850, 3902
The values correspond to:
Thumb
Index
Middle
Ring
Pinky
Therefore:
Value 1 → Thumb
Value 2 → Index
Value 3 → Middle
Value 4 → Ring
Value 5 → Pinky
Joint angles are generated inside Unity from the five incoming finger sensor values.

    3) Serial Thread
Serial data is read using a separate background thread.
The architecture is:
ESP32
  ↓
Serial Port
  ↓
ReadLoop() [Background Thread]
  ↓
latestADC[5]
  ↓
Unity Update()

The serial thread parses the incoming line and stores the latest five ADC values.
Unity's main thread then copies these values during Update().
This separation prevents the serial reading operation from directly blocking Unity's main thread.



# Real-Time Processing Pipeline
Once our calibration is complete, every finger passes through the following pipeline:
Raw ADC
   ↓
Filtering
   ↓
Filtered ADC
   ↓
Normalization
   ↓
Normalized Value
   ↓
Finger Flexion
   ↓
Circular-Grasp Model
   ↓
MCP / PIP / DIP
   ↓
Bone Rotation


Module 3 – Filtering
The filtering stage converts:
rawADC → filteredADC
The selected filter mode determines how the data is processed.
The moving-average implementation maintains a sample buffer and calculates the mean of the currently available samples.

Module 4 – Calibration
After filtering, the ADC value is normalized using:
Mathf.InverseLerp(adcMin, adcMax, filteredADC)

Conceptually:
Filtered ADC
     ↓
adcMin / adcMax
     ↓
Normalized Value

The normalized value is approximately represented in the range:
0 → 1

The calibration values are finger-specific.
For example:
Thumb  → its own adcMin / adcMax
Index  → its own adcMin / adcMax
Middle → its own adcMin / adcMax
Ring   → its own adcMin / adcMax
Pinky  → its own adcMin / adcMax


Module 5 – Finger Flexion
The normalized value is converted into a finger flexion angle using:
flexionAngle = normalized * flexionMax;
With the default:
flexionMax = 50°
The system maps the normalized signal to a maximum PIP-equivalent flexion angle of approximately 50°.







Module 6 – Circular-Grasp Model
The system does not independently calculate every finger joint from separate sensors.
Instead, the current implementation uses an intrafinger circular-grasp relationship.
The PIP-equivalent flexion angle is:
PIP = flexionAngle

The MCP angle is calculated as:
MCP = flexionAngle × mcpRatio

with:
mcpRatio = 4/3

Therefore:
MCP = (4/3) × PIP

The DIP angle is calculated as:
DIP = flexionAngle × dipRatio

with:
dipRatio = 3/2

Therefore:
DIP = (3/2) × PIP

The calculated angles are then clamped:
MCP maximum = 90°
DIP maximum = 135°
Module 7 – Bone Rotation
The calculated joint angles are applied directly to the Unity hand model.
The script stores the initial local rotation of each bone:
mcpInit
pipInit
dipInit

The current rotation is then calculated using:
initialRotation * Quaternion.Euler(0, 0, -jointAngle)

Therefore, the current implementation rotates the finger bones around the local Z axis using the negative joint angle.
For example:
MCP → -MCP angle
PIP → -PIP angle
DIP → -DIP angle

This allows the original orientation of the hand model to be preserved while applying the calculated finger movement.






# Hand Model
The project includes a rigged 3D hand model.
Each tracked finger requires the following transform references:
MCP Bone
PIP Bone
DIP Bone

These references are assigned to the corresponding FingerState in the Unity Inspector.
The hand model therefore acts as the visualization layer of the sensor processing pipeline.
Complete System Architecture
                ┌───────────────────┐
                 │  Sensorized Glove │
                 │   5 Finger Sensors│
                 └─────────┬─────────┘
                           │
                           ▼
                 ┌───────────────────┐
                 │       ESP32       │
                 │   ADC Acquisition │
                 └─────────┬─────────┘
                           │
                           │ Serial
                           │ 115200
                           ▼
                 ┌───────────────────┐
                 │ HandFingerSerial  │
                 │                   │
                 │   ReadLoop()      │
                 └─────────┬─────────┘
                           │
                           ▼
                     Raw ADC [5]
                           │
                           ▼
                 ┌───────────────────┐
                 │     Filtering        │
                 │ Raw / MA /        │
                 │ Velocity /           │
                 │ Velocity Assist  │
                 └─────────┬─────────┘
                           │
                          ▼
                    Filtered ADC
                           │
                          ▼
                 ┌───────────────────┐
                 │    Calibration        │
                 │ FingerCalibration │
                 └─────────┬─────────┘
                           │
                           ▼
                     Normalized [5]
                           │
                           ▼
                  Finger Flexion [5]
                           │
                           ▼
                 Circular-Grasp Model
                           │
                           ▼
                    MCP / PIP / DIP
                           │
                           ▼
                 ┌───────────────────┐
                 │   Rigged Hand     │
                 │      Model             │
                 └───────────────────┘





# Calibration Workflow
To use the system:
1. Start Unity
Open the Unity project and load the hand-tracking scene.
2. Configure HandFingerSerial
Assign the MCP, PIP, and DIP bones for each finger.
Set the correct serial port:
COM8

or the appropriate port for your ESP32.
3. Connect ESP32
Connect the ESP32 and start sending five ADC values.
4. Start the Scene
HandFingerSerial attempts to open the serial port.
The calibration script waits until all five fingers begin reporting sensor values.
5. Flat Pose
Hold the hand completely open and flat.
Press:
SPACE

The system collects five readings per finger.
6. Bent Pose
Make a fist / fully bend the fingers.
Press:
SPACE

Again, five readings are collected per finger.
7. Calibration Complete
The calibration script calculates the ADC range for every finger and sends the values to HandFingerSerial.
Tracking is then enabled.
8. Real-Time Tracking
The incoming sensor values are processed and the 3D hand model follows the finger movements.

### Important Implementation Detail
Calibration must be completed before finger tracking becomes active.
HandFingerSerial initially receives and stores raw ADC values, but the processing pipeline does not run until:
trackingAllowed == true

FingerCalibration.cs enables tracking by calling:
AllowTracking(mins, maxes);

This prevents uncalibrated sensor values from immediately driving the hand model.



# Debugging and Data Visualization
The FingerState class exposes every important stage of the processing pipeline:
rawADC
    ↓
filteredADC
    ↓
normalized
    ↓
flexionAngle
    ↓
finalMCP
finalPIP
finalDIP

These values can be accessed by additional Unity scripts for:
Console debugging
Sensor analysis
Filter comparison
Calibration analysis
Key Parameters
The following parameters can be adjusted through the Unity Inspector.
Parameter
Purpose
portName
ESP32 serial port
maxAttempts
Number of serial connection attempts
mode
Filtering mode
maWindow
Moving-average window
velSmooth
Velocity smoothing
velGain
Velocity-assist strength
flexionMax
Maximum PIP-equivalent flexion
mcpRatio
MCP/PIP relationship
dipRatio
DIP/PIP relationship
mcpClamp
Maximum MCP angle
dipClamp
Maximum DIP angle




Testing Results : 









Requirements
Hardware
ESP32 development board
Five finger sensors / flex sensors
Sensorized glove
USB cable
Computer
Software
Unity
ESP32 firmware
USB serial communication
Rigged 3D hand model

Current Implementation
The current implementation provides:
Five-finger sensor input
ESP32 serial communication
Background serial reading thread
Manual two-pose calibration
Five samples per calibration pose
Per-finger calibration ranges
Raw sensor mode
Moving-average filtering
Velocity-only processing
Velocity-assist processing
Finger flexion calculation
MCP/PIP/DIP estimation
Configurable joint ratios
Joint-angle clamping
Real-time Unity bone rotation
Processing-stage values exposed for debugging and visualization 

Project Purpose
This software is a research and development component of a broader sensorized glove system for real-time hand pose capture and visualization.
The project explores the complete path from wearable sensor data to real-time virtual hand motion:
Physical Hand
     ↓
Wearable Sensors
     ↓
ESP32
     ↓
Sensor Data
     ↓
Calibration & Filtering
     ↓
Joint Estimation
     ↓
Virtual Hand

The system provides a foundation for future work in:
Wearable robotics
Hand pose estimation
Rehabilitation research
Assistive robotics
Human-machine interaction
Soft robotic gloves
Robotics research

Author
Shraddha Hirewar
Robotics & Artificial Intelligence
MGM University
Chhatrapati Sambhajinagar, Maharashtra, India


