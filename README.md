# JoyconLib

![Imgur](https://i.imgur.com/BbV6Srg.gif)

Nintendo Switch Joy-Con library for Unity. Featuring: button/stick polling, HD rumble, and accelerometer data processing.

To use, add an empty GameObject to your scene and attach JoyconManager.cs. Look at JoyconDemo.cs for sample code to get you up and running.

With thanks/apologies to [CTCaer](https://github.com/ctcaer/jc_toolkit/), [dekuNukem](https://github.com/dekuNukem/Nintendo_Switch_Reverse_Engineering), [shinyquagsire23](https://github.com/shinyquagsire23/HID-Joy-Con-Whispering), [mfosse](https://github.com/mfosse/JoyCon-Driver), and [riking](https://github.com/riking/joycon).

Uses C# glue code and [HIDAPI](https://github.com/signal11/hidapi) binaries from [Unity-Wiimote](https://github.com/Flafla2/Unity-Wiimote)

GetVector method (attempt at sensor fusion implementation) is still unreliable! Enable in JoyconManager at your own risk. Sensor fusion code is in Joycon.ProcessIMU. Feel free to submit pull requests; sensor fusion code based on [this guide](starlino.com/imu_guide.html).
