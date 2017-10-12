# JoyconLib
testing Nintendo Switch Joy-Con input with HoloPlayer One

with thanks/apologies to [CTCaer](https://github.com/ctcaer/jc_toolkit/), [dekuNukem](https://github.com/dekuNukem/Nintendo_Switch_Reverse_Engineering), [shinyquagsire23](https://github.com/shinyquagsire23/HID-Joy-Con-Whispering), [mfosse](https://github.com/mfosse/JoyCon-Driver), and [riking](https://github.com/riking/joycon).

GetVector method is still unreliable! enable in JoyconManager at your own risk. improvements to come in the next few days. sensor fusion code is in JoyCon.ProcessIMU. it's really bad, you've been warned! feel free to submit pull requests. sensor fusion code based on [this guide](starlino.com/imu_guide.html).

uses C# glue code and [HIDAPI](https://github.com/signal11/hidapi) binaries from [Unity-Wiimote](https://github.com/Flafla2/Unity-Wiimote)

if you have errors cloning this, run `git lfs install --skip-smudge`.