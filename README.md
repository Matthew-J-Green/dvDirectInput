# dvDirectInput

This plugin adds input device support to Derail Valley

# Features

* Analogue axis support for all interior control types
* GUI configuration via BepInEx Configuration Manager
* Support for multiple game controllers at once

# Limitations

* Some interior control types do not behave as expected. They may require discrete values rather than analogue inputs
* Button support isn't officially setup, however they may work. It would be useful to allow buttons to only toggle on press to enable support for converting a momentary button to a latching one. This would additionally allow for rotary encoders to work
* While the diesel locos have discrete throttle steps. Analogue inputs work fine but may be better with thresholds
* Diesel locos have a 3 state reverser. Neutral is exactly 50%, you won't get neutral unless you set up a deadzone on your input device
* Xbox gamepads don't seem to work, not that they're of much use for controlling a loco
* Unplugging devices will crash the mod. It'll be fixed at some point.
* Inputs are not currently invertable
* There is no deadzone support. Configure this in you devices software for now
* This mod assumes the device outputs its axis with values between 0 and 65535. It's unclear if all devices use this range. If you are having issues not being able to reach full/zero throttle - first check windows game controllers to see if that reports the full range, then check your device software utility to see if it's calibrated/configured correctly. If that doesnt fix it, open the log or view the comand line to see what range of values are reported and raise an issue.
* This mod hasn't been tested that much. Prepare for things to break

# Installation

This mod requires [BepInEx](https://github.com/BepInEx/BepInEx/releases/latest). [Installation Guide](https://www.nexusmods.com/derailvalley/videos/7)

It is highly recommended to enable the logging console by setting `Enabled` to true under the heading `[Logging.Console]` within `BepInEx\config\BepInEx.cfg`

Once BepInEx is installed, download the [release](https://github.com/Matthew-J-Green/dvDirectInput/releases/latest) and throw the contents of the zip into the Derail Valley root installation folder (typically `C:\Program Files (x86)\Steam\steamapps\common\Derail Valley\`). The folder structure of the zip is organised as such that the plugin will end up in the right directory.

You will need to change the default keybind as it conflicts. This is done by changing `Show config manager` under the heading `[General]` within `com.bepis.bepinex.configurationmanager.cfg`

# Configuration

The `dvDirectInput.cfg` file is in the `BepInEx\config` folder. However, it's recommended to use the [Configuration Manager plugin](https://github.com/BepInEx/BepInEx.ConfigurationManager) to change the settings.

Make sure your input devices are plugged in before launching Derail Valley. This mod currently does not dynamically load/unload devices so don't unplug devices mid game too.

On first launch a GUI is presented in the top left. It lists all the input devices with its name, a numerical ID and the mapping of the most recent inputs used. This can be disabled in the Configuration Manager

Open BepInEx Configuration Manager to configure the mod. Here you can map the input devices to exposed locomotive controls. Make sure the control is enabled for it to take effect.

The next time you jump in a locomotive, try out your new control interface.

# Development

This is a complete overhaul of the original [AnalogueLocoControlMod](https://github.com/Matthew-J-Green/dv-loco-analogue-control-mod).

## Design

There are a few ways to go about getting input devices into Derail Valley.
1. Use the [Unity inputs](https://docs.unity3d.com/ScriptReference/Input.html) - this is what AnalogueLocoControlMod did. It doesnt support that many inputs and is a bit clunky to get the data (at least with the original implementation in the mod).
2. [DirectInput](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/ee416842(v=vs.85)) - a legacy method of getting inputs. It supports 8 axis and 128 buttons.
3. [XInput](https://learn.microsoft.com/en-us/windows/win32/xinput/getting-started-with-xinput) - a now legacy method designed for use with Xbox compatible controllers. It is simpler to use with controllers but is limited on input count.
4. [GameInput](https://learn.microsoft.com/en-us/gaming/gdk/_content/gc/input/overviews/input-overview) - the recommended way to interract with input devices on modern Windows platforms (Windows 10 19H1 onwards). It is a superset of all the legacy APIs, is earier to use and has much better performance.

This mod uses DirectInput hence the mod name.
Why wasn't GameInput chosen? Because the mod author didn't know it was a thing until compiling this documentation.

The logic flow of the plugin is as follows:

On loading the plugin:
1. Get all DirectInput GameControl devices, initialise and aquire them.
2. Get all the available loco controls from the game

On each game update:
1. Poll all the devices and retieve all the input data from their buffers.
2. Shove all the input data into a single queue. Theres also a recent inputs queue for GUI debug if enabled. This holds on to the input data for a specifed time.
3. Iterate through all the queue items and set the locomotive controls based on the inputs received

On each GUI update:
1. If the debug GUI is enabled it takes a look at the recent inputs queue and displays which inputs have been most recently used.

## Contributing

There are some hardcoded paths in [dvDirectInput.csproj](dvDirectInput/dvDirectInput.csproj)\
Change them before attempting to build this project for yourself

Fork away!

The author of this mod is not really a developer. You might have better ways of doing all this. Feel free to teach him

## License

Source code is distributed under the MIT license. See [LICENSE](LICENSE) for more information.