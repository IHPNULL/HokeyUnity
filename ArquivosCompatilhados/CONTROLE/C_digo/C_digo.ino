#include<Keyboard.h>

uint8_t pinCH[] = {7, 8}; //Define the pins that the CH are attached to

uint8_t usedCH = sizeof(pinCH);

void setup() {

  //Set the channel pins to INPUT
  for(int i = 0; i < usedCH; i++){
    pinMode(pinCH[i], INPUT);
  }

  //Starts serial comunication
//  Serial.begin(9600);

  //Starts Keyboard
  Keyboard.begin();
}

void loop() {
  
  int CHRead[usedCH];
  int delayX;
  int delayY;
  
  readChannels(pinCH, CHRead, usedCH);
  filterReads(CHRead, usedCH);
  //printCHReads(CHRead, usedCH);

  if(CHRead[1] > 1500){
    Keyboard.press('W');
  }
  else if(CHRead[1] < 1500){
    Keyboard.press('S');
  }
  if(CHRead[0] > 1500){
    Keyboard.press('D');
  }
  else if(CHRead[0] < 1500){
    Keyboard.press('A');
  }

  Keyboard.releaseAll();
  
}

void readChannels(uint8_t *pinCH, int *CHRead, uint8_t nChannel){

  for(int i = 0; i < nChannel; i++) CHRead[i] = pulseIn(pinCH[i], HIGH); 
}

void filterReads(int *CHRead, uint8_t nChannel){
  
  CHRead[0] = constrain(CHRead[0], 1200, 1800);
  CHRead[1] = constrain(CHRead[1], 1150, 1900);

  if(CHRead[0] >= 1485 && CHRead[0] <= 1515) CHRead[0] = 1500;
  if(CHRead[1] >= 1485 && CHRead[1] <= 1515) CHRead[1] = 1500;  
}

void printCHReads(int *CHRead, uint8_t nChannel){

  for(int i = 0; i < nChannel; i++){
    
    Serial.print("CH");
    Serial.print(i + 1);
    Serial.print(": ");
    Serial.print(CHRead[i]);
    Serial.print("   ");
  }

  Serial.print("\n");
}
