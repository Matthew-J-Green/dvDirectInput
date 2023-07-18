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
using static DV.HUD.InteriorControlsManager;
using static dvDirectInput.Plugin;
using static System.Collections.Specialized.BitVector32;

namespace dvDirectInput
{
	[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	public class Plugin : BaseUnityPlugin
	{
		// Items corresponding to the controls config
		public class ConfigControls
		{
			public ConfigEntry<bool> Enabled { get; set; }
			public ConfigEntry<int> DeviceId { get; set; }
			public ConfigEntry<JoystickOffset> DeviceOffset { get; set; }

		}

		// Config - Debug
		private ConfigEntry<bool> configEnableRecentInputGUI;

		// Config - Controls
		// Just a big list storing every input we want to control
		public List<ConfigControls> configControls = new List<ConfigControls>();

		// Items used to identify a control device. we could actually pass along the joystick object here instead of the ID
		public struct Input
		{
			public int JoystickId { get; set; }
			public int Value { get; set; }
			public float NormalisedValue => (float)Value / UInt16.MaxValue;
			public int Timestamp { get; set; }
			public JoystickOffset Offset;

		}

		private List<Joystick> joysticks = new List<Joystick>();
		private Queue<Input> inputQueue = new Queue<Input>();
		private List<Queue<JoystickUpdate>> joysticksRecentInputs = new List<Queue<JoystickUpdate>>();

		// Loading Mod
		private void Awake()
		{
			// Plugin startup logic
			Logger.LogInfo($"Plugin [{PluginInfo.PLUGIN_GUID}|{PluginInfo.PLUGIN_NAME}|{PluginInfo.PLUGIN_VERSION}] is loaded!");

			// Initialise configControls
			// ControlType contains all the controllable elements. We can just create a list with all the elements.
			// The ControlType is the index of this list.
			for (int i = 0; i < Enum.GetNames(typeof(ControlType)).Length; i++)
			{
				configControls.Add(new ConfigControls());
			}
			
			// Initialise all Direct Input game controllers as joysticks
			// We may want to run this in the main logic in case of devices attached on the fly
			// Could be more complicated than it sounds
			var directInput = new DirectInput();
			var devices = directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AllDevices);
			foreach (var device in devices)
			{
				Joystick joystick = new Joystick(directInput, device.InstanceGuid);
				// joystick.Properties.Range is not implemented and will throw an error
				
				foreach (var obj in joystick.GetObjects())
					Logger.LogInfo($"ID: {joystick.Properties.JoystickId}, Device: {joystick.Properties.ProductName}, Input:{obj.Name}");

				// Set the max number of entries the joystick will return from a poll event via GetBufferedData()
				joystick.Properties.BufferSize = 128;

				// Open the Joystick and add it to a list
				joystick.Acquire();
				joysticks.Add(joystick);

				joysticksRecentInputs.Add(new Queue<JoystickUpdate>());
			}
			if (joysticksRecentInputs.Count() == 0)
				Logger.LogInfo($"No input devices found");

			BindAllConfigs();
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
			while (inputQueue.Count > 0)
			{
				// Dont bother doing anything if we arent in a loco
				if (!PlayerManager.Car?.IsLoco ?? true)
				{
					// Probbaly not going to get in a loco this game update so just clear the queue
					inputQueue.Clear();
					break;
				}

				// Eat up all the queue items
				var input = inputQueue.Dequeue();

				// Assign Inputs
				foreach (var configControl in configControls.Select((val, idx) => new { idx, val }))
				{
					// We should probably do a lookup for the inputs against the mappings instead of iterating
					if (configControl.val.Enabled.Value && input.JoystickId == configControl.val.DeviceId.Value && input.Offset == configControl.val.DeviceOffset.Value)
					{
						ControlReference control = new ControlReference();
						if(!PlayerManager.Car?.interior.GetComponentInChildren<InteriorControlsManager>().TryGetControl((ControlType)configControl.idx, out control) ?? true) return;
						control.controlImplBase?.SetValue(input.NormalisedValue);
						break;
					}
				}				
			}
		}

		private void BindAllConfigs()
		{
			// GUI
			configEnableRecentInputGUI = Config.Bind("Debug - GUI",
				"Enable",
				true,
				"Enable/Disable displaying recent inputs. Use this to identify the inputs for configuring the controls");

			// Controls
			foreach (var configControl in configControls.Select((val, idx) => new { idx, val }))
			{
				BindControlsConfigs($"{((ControlType)configControl.idx).ToString()}", configControl.val);
			}
		}

		private void BindControlsConfigs(String section, ConfigControls config)
		{
			// We should probably bind and unbind the ID and offset based on the enable signal to de-clutter the GUI
			config.Enabled = Config.Bind($"Controls - {section}",
				"Enable",
				false,
				"Enables this input");

			config.DeviceId = Config.Bind($"Controls - {section}",
				"Input Device ID",
				0,
				"ID of input device provided by GUI");

			config.DeviceOffset = Config.Bind($"Controls - {section}",
				"Input Device Offset",
				JoystickOffset.X,
				"Input device offset axis/button provided by GUI");

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
