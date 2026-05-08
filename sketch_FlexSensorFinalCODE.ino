void setup() {
    Serial.begin(115200);
    analogReadResolution(12);
    analogSetAttenuation(ADC_11db);
}

void loop() {
    Serial.println(analogRead(33));
    delay(50);
}