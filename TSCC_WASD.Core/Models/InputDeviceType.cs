namespace TSCC_WASD.Core.Models;

public enum InputDeviceType
{
    XInput,
    DirectInput,
    KeyboardMouse,
    Switch2ProUsb,
    // Appended so numeric values in older profiles.json files keep their meaning.
    Auto,
    PlayStation,
    SwitchPro,
    Switch2ProBluetooth
}
