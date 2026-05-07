import serial
import time
import math

PORT = "COM5"  # 🔴 Change this to your actual COM port
BAUD_RATE = 115200
print("Sending on port:", PORT)

print("Starting Serial Data Generator...")

try:
    ser = serial.Serial(PORT, BAUD_RATE)
    time.sleep(2)
    print("Serial Port Opened Successfully")
except Exception as e:
    print("Error opening serial port:", e)
    input("Press Enter to exit...")
    exit()

t = 0

try:
    while True:
        angles = []

        for i in range(20):
            angle = 45 + 45 * math.sin(t + i * 0.3)
            angles.append(int(angle))

        packet = "S," + ",".join(map(str, angles)) + "\n"
        ser.write(packet.encode())

        print(packet.strip())

        t += 0.1
        time.sleep(0.02)

except KeyboardInterrupt:
    print("\nStopped by user")
    ser.close()