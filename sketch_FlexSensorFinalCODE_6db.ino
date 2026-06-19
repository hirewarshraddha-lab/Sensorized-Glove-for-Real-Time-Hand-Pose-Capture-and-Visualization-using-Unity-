void setup() {
    Serial.begin(115200);
    analogReadResolution(12);
    analogSetAttenuation(ADC_6db);
}

void loop() {
    Serial.println(analogRead(32));
    delay(100);
}