using BepInEx;
using BepInEx.Configuration;
using DV.HUD;
using DV.Simulation.Cars;
using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace dvDirectInput
{
	[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	public class Plugin : BaseUnityPlugin
	{
		// Config - Gui
		private ConfigEntry<bool> configEnableRecentInputGUI;
		// Config - Controls - Throttle
		public ConfigEntry<bool> configThrottleInputEnabled;
		public ConfigEntry<int> configThrottleInputJoystickId;
		public ConfigEntry<JoystickOffset> configThrottleInputJoystickOffset;
		// Config - Controls - Train Brake
		public ConfigEntry<bool> configTrainBrakeInputEnabled;
		public ConfigEntry<int> configTrainBrakeInputJoystickId;
		public ConfigEntry<JoystickOffset> configTrainBrakeInputJoystickOffset;
		// Config - Controls - Independent Brake
		public ConfigEntry<bool> configIndependentBrakeInputEnabled;
		public ConfigEntry<int> configIndependentBrakeInputJoystickId;
		public ConfigEntry<JoystickOffset> configIndependentBrakeInputJoystickOffset;
		// Config - Controls - Dynamic Brake
		public ConfigEntry<bool> configDynamicBrakeInputEnabled;
		public ConfigEntry<int> configDynamicBrakeInputJoystickId;
		public ConfigEntry<JoystickOffset> configDynamicBrakeInputJoystickOffset;
		// Config - Controls - Reverser
		public ConfigEntry<bool> configReverserInputEnabled;
		public ConfigEntry<int> configReverserInputJoystickId;
		public ConfigEntry<JoystickOffset> configReverserInputJoystickOffset;

		public struct Input
		{
			public int JoystickId { get; set; }
			public int Value { get; set; }
			public float NormalisedValue => (float)Value / UInt16.MaxValue;
			public int Timestamp { get; set; }
			public JoystickOffset Offset;

			public override string ToString()
			{
				return string.Format(CultureInfo.InvariantCulture, "ID: {0}, Offset: {1}, Value: {2}, Timestamp {3}", JoystickId, Offset, Value, Timestamp);
			}
		}

		private List<Joystick> joysticks = new List<Joystick>();
		private Queue<Input> inputQueue = new Queue<Input>();

		private List<Queue<JoystickUpdate>> joysticksRecentInputs = new List<Queue<JoystickUpdate>>();


		// Loading Mod
		private void Awake()
		{
			// Config - GIU
			configEnableRecentInputGUI = Config.Bind("Debug",
				"Enable GUI",
				true,
				"Enable/Disable displaying recent inputs. Use this to identify the inputs for configuring the controls");

			// Copy/paste cause im lazy
			// Config - Controls - Throttle
			configThrottleInputEnabled = Config.Bind("Throttle Input",
				"Enable",
				false,
				"Enables this input");

			configThrottleInputJoystickId = Config.Bind("Throttle Input",
				"Input Device ID",
				0,
				"ID of input device provided by GUI");

			configThrottleInputJoystickOffset = Config.Bind("Throttle Input",
				"Input Device Offset",
				JoystickOffset.X,
				"Input device offset(axis/button provided by GUI");

			// Config - Controls - Train Brake
			configTrainBrakeInputEnabled = Config.Bind("Train Brake Input",
				"Enable",
				false,
				"Enables this input");

			configTrainBrakeInputJoystickId = Config.Bind("Train Brake Input",
				"Input Device ID",
				0,
				"ID of input device provided by GUI");

			configTrainBrakeInputJoystickOffset = Config.Bind("Train Brake Input",
				"Input Device Offset",
				JoystickOffset.X,
				"Input device offset(axis/button provided by GUI");

			// Config - Controls - Independent Brake
			configIndependentBrakeInputEnabled = Config.Bind("Independent Brake Input",
				"Enable",
				false,
				"Enables this input");

			configIndependentBrakeInputJoystickId = Config.Bind("Independent Brake Input",
				"Input Device ID",
				0,
				"ID of input device provided by GUI");

			configIndependentBrakeInputJoystickOffset = Config.Bind("Independent Brake Input",
				"Input Device Offset",
				JoystickOffset.X,
				"Input device offset(axis/button provided by GUI");

			// Config - Controls - Dynamic Brake
			configDynamicBrakeInputEnabled = Config.Bind("Dynamic Brake Input",
				"Enable",
				false,
				"Enables this input");

			configDynamicBrakeInputJoystickId = Config.Bind("Dynamic Brake Input",
				"Input Device ID",
				0,
				"ID of input device provided by GUI");

			configDynamicBrakeInputJoystickOffset = Config.Bind("Dynamic Brake Input",
				"Input Device Offsett",
				JoystickOffset.X,
				"Input device offset(axis/button provided by GUI");

			// Config - Controls - Reverser
			configReverserInputEnabled = Config.Bind("Reverser Input",
				"Enable",
				false,
				"Enables this input");

			configReverserInputJoystickId = Config.Bind("Reverser Input",
				"Input Device ID",
				0,
				"ID of input device provided by GUI");

			configReverserInputJoystickOffset = Config.Bind("Reverser Input",
				"Input Device Offset",
				JoystickOffset.X,
				"Input device offset(axis/button provided by GUI");

			// Plugin startup logic
			Logger.LogInfo($"Plugin [{PluginInfo.PLUGIN_GUID}|{PluginInfo.PLUGIN_NAME}|{PluginInfo.PLUGIN_VERSION}] is loaded!");

			// Initialise all Direct Input game controllers as joysticks
			// We may want to run this in the main logic in case of devices attached on the fly
			// Could be more complicated than it sounds
			var directInput = new DirectInput();
			var devices = directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AllDevices);
			foreach (var device in devices)
			{
				Joystick joystick = new Joystick(directInput, device.InstanceGuid);
				Logger.LogInfo($"DirectInput found device: {joystick.Information.ProductName}, {joystick.Properties.JoystickId}");

				// Set the max number of entries the joystick will return from a poll event via GetBufferedData()
				joystick.Properties.BufferSize = 128;

				// Open the Joystick and add it to a list
				joystick.Acquire();
				joysticks.Add(joystick);

				joysticksRecentInputs.Add(new Queue<JoystickUpdate>());
			}
		}

		// Every Frame
		void Update()
		{
			int currentTimestamp = 0;

			// Grab inputs for all controllers
			foreach (var joystick in joysticks.Select((val, idx) => new { idx, val }))
			{
				joystick.val.Poll();
				foreach (var data in joystick.val.GetBufferedData())
				{
					//Logger.LogInfo($"Device {joystick.val.Properties.JoystickId}: {data}");
					// Chuck all the inputs on a queue
					var input = new Input() { JoystickId = joystick.val.Properties.JoystickId, Offset = data.Offset, Value = data.Value, Timestamp = data.Timestamp };
					inputQueue.Enqueue(input);

					// GUI Logic - Copy of inputs
					if (configEnableRecentInputGUI.Value)
					{
						joysticksRecentInputs[joystick.idx].Enqueue(data);
						currentTimestamp = data.Timestamp;
					}
				}
				// GUI Logic - Remove any inputs if they have been displayed for a suitable period
				if (configEnableRecentInputGUI.Value)
				{
					if (joysticksRecentInputs[joystick.idx].Count > 0)
					{
						while (currentTimestamp - joysticksRecentInputs[joystick.idx].Peek().Timestamp > 1000)
						{
							joysticksRecentInputs[joystick.idx].Dequeue();
							if (joysticksRecentInputs[joystick.idx].Count == 0)
								break;
						}
					}
				}
			}

			// Main Logic
			// Eat up all the queue items
			while (inputQueue.Count > 0)
			{
				var input = inputQueue.Dequeue();
				// Logger.LogInfo($"Inputs: {input}");
				// Dont bother doing anything if we arent in a loco
				if (!PlayerManager.Car?.IsLoco ?? true)
					continue;

				// Map inputs
				// Todo MORE INPUTS
				// DirectInput exposes buttons so these should be added for other loco functions that havent been implemented here
				// These could include flipping breakers, starter, horn, sander etc.
				// Additionally those controllers with multi state dials/switcher/levers could use them for the reverser (3 state), lights (5 state?) etc

				// Copy and paste cause im lazy
				// Throttle
				if (configThrottleInputEnabled.Value && input.JoystickId == configThrottleInputJoystickId.Value && input.Offset == configThrottleInputJoystickOffset.Value)
					PlayerManager.Car?.GetComponent<SimController>()?.controlsOverrider?.GetControl(InteriorControlsManager.ControlType.Throttle)?.Set(input.NormalisedValue);

				// Train Brake
				if (configTrainBrakeInputEnabled.Value && input.JoystickId == configTrainBrakeInputJoystickId.Value && input.Offset == configTrainBrakeInputJoystickOffset.Value)
					PlayerManager.Car?.GetComponent<SimController>()?.controlsOverrider?.GetControl(InteriorControlsManager.ControlType.TrainBrake).Set(input.NormalisedValue);

				// Independent Brake
				if (configIndependentBrakeInputEnabled.Value && input.JoystickId == configIndependentBrakeInputJoystickId.Value && input.Offset == configIndependentBrakeInputJoystickOffset.Value)
					PlayerManager.Car?.GetComponent<SimController>()?.controlsOverrider?.GetControl(InteriorControlsManager.ControlType.IndBrake)?.Set(input.NormalisedValue);

				// Dynamic Brake
				if (configDynamicBrakeInputEnabled.Value && input.JoystickId == configDynamicBrakeInputJoystickId.Value && input.Offset == configDynamicBrakeInputJoystickOffset.Value)
					PlayerManager.Car?.GetComponent<SimController>()?.controlsOverrider?.GetControl(InteriorControlsManager.ControlType.DynamicBrake)?.Set(input.NormalisedValue);

				// Reverser
				// Diesel locos specify neutral as exactly 50%. Set up a deadzone on your input device (I might make one here eventually)
				if (configReverserInputEnabled.Value && input.JoystickId == configReverserInputJoystickId.Value && input.Offset == configReverserInputJoystickOffset.Value)
					PlayerManager.Car?.GetComponent<SimController>()?.controlsOverrider?.GetControl(InteriorControlsManager.ControlType.Reverser)?.Set(input.NormalisedValue);

				//		  Throttle,
				//        TrainBrake,
				//        Reverser,
				//        IndBrake,
				//        Handbrake,
				//        Sander,
				//        Horn,
				//        HeadlightsFront,
				//        HeadlightsRear,
				//        StarterFuse,
				//        ElectricsFuse,
				//        TractionMotorFuse,
				//        StarterControl,
				//        DynamicBrake,
				//        CabLight,
				//        Wipers,
				//        FuelCutoff,
				//        ReleaseCyl,
				//        IndHeadlightsTypeFront,
				//        IndHeadlights1Front,
				//        IndHeadlights2Front,
				//        IndHeadlightsTypeRear,
				//        IndHeadlights1Rear,
				//        IndHeadlights2Rear,
				//        IndWipers1,
				//        IndWipers2,
				//        IndCabLight,
				//        IndDashLight,
				//        GearboxA,
				//        GearboxB,
				//        CylCock,
				//        Injector,
				//        Firedoor,
				//        Blower,
				//        Damper,
				//        Blowdown,
				//        CoalDump,
				//        Dynamo,
				//        AirPump,
				//        Lubricator,
				//        Bell
			}
		}

		// Every GUI event (possibly multiple times per frame)
		void OnGUI()
		{
			if (configEnableRecentInputGUI.Value)
			{
				// Show all the recognised game controlers and inputs to the user
				foreach (var joystick in joysticks.Select((val, idx) => new { idx, val }))
				{
					// Gets unique sorted input names from the recent input list
					var offsetList = new SortedSet<JoystickOffset>(joysticksRecentInputs[joystick.idx].Select(val => val.Offset).ToList().Distinct());

					// Just do a bunch of GUI stuff
					GUIStyle style = new GUIStyle();
					style.alignment = TextAnchor.MiddleLeft;
					style.stretchWidth = false;
					style.normal.textColor = Color.white;
					style.normal.background = Texture2D.grayTexture;

					GUILayout.BeginHorizontal(style);

					GUILayout.Label("Device Name: ", style);
					GUILayout.Label($"[{joystick.val.Information.ProductName}] ", style);

					style.normal.textColor = Color.gray;
					style.normal.background = Texture2D.whiteTexture;
					GUILayout.Label("Joystick ID:", style);
					GUILayout.Label($"[{joystick.val.Properties.JoystickId}] ", style);

					style.normal.textColor = Color.white;
					style.normal.background = Texture2D.grayTexture;
					GUILayout.Label(" Inputs: ", style);
					GUILayout.Label($"{String.Join(", ", offsetList)}", style);

					GUILayout.EndHorizontal();
				}
			}
		}
		// Unloading Mod
		void OnDestroy()
		{
			// Probably the right way to do this
			foreach (var joystick in joysticks)
			{
				joystick.Dispose();
			}

			// If the mod was reloaded at runtime we would want empty lists (is this done automatically?)
			joysticks.Clear();
			inputQueue.Clear();
			joysticksRecentInputs.Clear();
		}
	}

}
