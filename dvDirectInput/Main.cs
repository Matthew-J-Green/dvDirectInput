using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityModManagerNet;
using UnityEngine;
using SharpDX.DirectInput;
using DV.HUD;
using static DV.HUD.InteriorControlsManager;


namespace dvDirectInput
{
	public class Settings : UnityModManager.ModSettings
	{
		public bool configEnableRecentInputGUI = true;

		public List<ConfigControls> configControls = new();

		public class ConfigControls
		{
			public bool Enabled = false;
			public int DeviceId = 0;
			public JoystickOffset DeviceOffset = JoystickOffset.X;
			public bool InvertControl = false;
		}
		public override void Save(UnityModManager.ModEntry modEntry)
		{
			Save(this, modEntry);
		}

		public void OnChange()
		{
		}
	}

	public static class Main
	{
		public static UnityModManager.ModEntry mod;
		public static Settings settings = new();

		// Items used to identify a control device. we could actually pass along the joystick object here instead of the ID
		public class Input
		{
			public Joystick JoystickObj { get; set; }
			public JoystickOffset Offset { get; set; }
			public int Value { get; set; }

			// There are 3 types of inputs with associated ranges
			// Axes 0 - 65535
			// Button 0, 128
			// POV -1 (released), 0 (up), 4500, 9000(right), 13500, 18000(down), 22500, 27000(left), 31500
			public float NormalisedValue()
			{
				return (float)Value / UInt16.MaxValue;
			}
			public int Timestamp { get; set; }

			public override string ToString()
			{
				return string.Format(CultureInfo.InvariantCulture, "ID: {0}, Offset: {1}, Value: {2}, Timestamp {3}", JoystickObj.Properties.JoystickId, Offset, Value, Timestamp);
			}

			// Just go get a bunch of properties and fields of the joystick and its objects
			public static void JoystickDebug(DeviceInstance device, Joystick joystick)
			{
				mod.Logger.Log($"");
				mod.Logger.Log($"Device Fields");
				foreach (var field in device.GetType().GetFields())
				{
					mod.Logger.Log($"{field.Name}, {field.GetValue(device)}");
				}

				mod.Logger.Log($"");
				mod.Logger.Log($"Joystick Properties");
				foreach (var prop in joystick.GetType().GetProperties())
				{
					mod.Logger.Log($"Joystick Properties for {prop.Name}");
					if (joystick.GetType().GetProperty(prop.Name).GetValue(joystick) == null)
						continue;
					if (joystick.GetType().GetProperty(prop.Name).GetValue(joystick).GetType().GetProperties().Length > 0)
					{
						foreach (var subprop in joystick.GetType().GetProperty(prop.Name).GetValue(joystick).GetType().GetProperties())
						{
							string val = "";
							try
							{
								val = subprop.GetValue(prop.GetValue(joystick)).ToString();
							}
							catch (Exception e)
							{
								val = e.Message;
							}
							mod.Logger.Log($"{prop.Name}, {subprop.Name}, {val}");
						}
					}
					mod.Logger.Log($"");
					mod.Logger.Log($"Joystick Fields for {prop.Name}");
					if (joystick.GetType().GetProperty(prop.Name).GetValue(joystick).GetType().GetProperties().Length > 0)
					{
						foreach (var field in joystick.GetType().GetProperty(prop.Name).GetValue(joystick).GetType().GetFields())
						{
							string val = "";
							try
							{
								val = field.GetValue(prop.GetValue(joystick)).ToString();
							}
							catch (Exception e)
							{
								val = e.Message;
							}
							mod.Logger.Log($"{prop.Name}, {field.Name}, {val}");
						}
					}
					mod.Logger.Log($"");
				}

				mod.Logger.Log($"");
				mod.Logger.Log($"Joystick Objects");
				foreach (var obj in joystick.GetObjects())
				{
					mod.Logger.Log($"Joystick Object Fields");
					foreach (var field in obj.GetType().GetFields())
					{
						mod.Logger.Log($"ID: {joystick.Properties.JoystickId}, Device: {joystick.Properties.ProductName}, {field.Name}: {field.GetValue(obj)}");
					}

					mod.Logger.Log($"Joystick Object Properties");
					mod.Logger.Log($"Deadzone: {Try<ObjectProperties>(() => joystick.GetObjectPropertiesById(obj.ObjectId).DeadZone.ToString())}");
					mod.Logger.Log($"Granularity: {Try<ObjectProperties>(() => joystick.GetObjectPropertiesById(obj.ObjectId).Granularity.ToString())}");
					mod.Logger.Log($"LogicalRange: {Try<ObjectProperties>(() => joystick.GetObjectPropertiesById(obj.ObjectId).LogicalRange.Minimum.ToString())}, {Try<ObjectProperties>(() => joystick.GetObjectPropertiesById(obj.ObjectId).LogicalRange.Maximum.ToString())}");
					mod.Logger.Log($"PhysicalRange: {Try<ObjectProperties>(() => joystick.GetObjectPropertiesById(obj.ObjectId).PhysicalRange.Minimum.ToString())}, {Try<ObjectProperties>(() => joystick.GetObjectPropertiesById(obj.ObjectId).PhysicalRange.Maximum.ToString())}");
					mod.Logger.Log($"Range: {Try<ObjectProperties>(() => joystick.GetObjectPropertiesById(obj.ObjectId).Range.Minimum.ToString())}, {Try<ObjectProperties>(() => joystick.GetObjectPropertiesById(obj.ObjectId).Range.Maximum.ToString())}");
					mod.Logger.Log($"Saturation: {Try<ObjectProperties>(() => joystick.GetObjectPropertiesById(obj.ObjectId).Saturation.ToString())}");
				}
				mod.Logger.Log($"");
			}

		}

		static string Try<T>(Func<string> func)
		{
			try
			{
				return func();
			}
			catch (Exception e)
			{
				return e.Message;
			}
		}

		private static List<Joystick> joysticks = new();
		private static Queue<Input> inputQueue = new();
		private static List<Queue<JoystickUpdate>> joysticksRecentInputs = new();
		static List<int> acceptableIDs = new();

		// Loading Mod
		static bool Load(UnityModManager.ModEntry modEntry)
		{
			mod = modEntry;

			// Load settings
			settings = Settings.Load<Settings>(modEntry);

			// Wipe the controls config if its not the size we are expecting
			if (settings.configControls.Count != Enum.GetNames(typeof(ControlType)).Length)
				settings.configControls.Clear();

			// Create the number of controls we desire if we havent loaded a suitable config
			if (settings.configControls.Count == 0)
			{
				// ControlType contains all the controllable elements in a loco
				// We can create a list to allow a mapping of inputs to controls
				// The ControlType is the index of this list
				for (int i = 0; i < Enum.GetNames(typeof(ControlType)).Length; i++)
				{
					settings.configControls.Add(new Settings.ConfigControls());
				}
			}

			// Initialise all Direct Input game controllers
			// We may want to run this in the main logic in case of devices attached on the fly
			// Could be more complicated than it sounds
			var directInput = new DirectInput();
			var devices = directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AllDevices);
			foreach (var device in devices)
			{
				var joystick = new Joystick(directInput, device.InstanceGuid);

				// Set some device properties
				joystick.Properties.BufferSize = 128;

				// Open the Joystick and add it to a list
				joystick.Acquire();

				joysticks.Add(joystick);
				joysticksRecentInputs.Add(new Queue<JoystickUpdate>());
				acceptableIDs.Add(joystick.Properties.JoystickId);

				if (settings.configEnableRecentInputGUI)
					Input.JoystickDebug(device, joystick);
			}

			if (joysticksRecentInputs.Count() == 0)
				mod.Logger.Warning($"No input devices found");

			mod.OnGUI = OnGUI;
			mod.OnFixedGUI = OnFixedGUI;
			mod.OnUpdate = OnUpdate;
			mod.OnSaveGUI = OnSaveGUI;

			return true;
		}

		// Every Frame
		static void OnUpdate(UnityModManager.ModEntry modEntry, float dt)
		{
			int currentTimestamp = 0;

			// Grab inputs for all controllers
			foreach (var joystick in joysticks.Select((val, idx) => new { idx, val }))
			{
				joystick.val.Poll();
				foreach (var data in joystick.val.GetBufferedData())
				{
					// Chuck all the inputs on a queue
					var input = new Input() { JoystickObj = joystick.val, Offset = data.Offset, Value = data.Value, Timestamp = data.Timestamp };
					inputQueue.Enqueue(input);

					// GUI Logic - Copy of inputs
					if (settings.configEnableRecentInputGUI)
					{
						mod?.Logger.Log($"{input}");
						joysticksRecentInputs[joystick.idx].Enqueue(data);
						currentTimestamp = data.Timestamp;
					}
				}
				// GUI Logic - Remove any inputs if they have been displayed for a suitable period
				if (settings.configEnableRecentInputGUI)
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
				foreach (var configControl in settings.configControls.Select((val, idx) => new { idx, val }))
				{
					// We should probably do a lookup for the inputs against the mappings instead of iterating
					if (configControl.val.Enabled && input.JoystickObj.Properties.JoystickId == configControl.val.DeviceId && input.Offset == configControl.val.DeviceOffset)
					{
						var control = new ControlReference();
						if (!PlayerManager.Car?.interior.GetComponentInChildren<InteriorControlsManager>().TryGetControl((ControlType)configControl.idx, out control) ?? true) return;
						control.controlImplBase?.SetValue(configControl.val.InvertControl ? 1.0f - input.NormalisedValue() : input.NormalisedValue());
						break;
					}
				}
			}
		}

		// Draw to screen
		static void OnFixedGUI(UnityModManager.ModEntry modEntry)
		{

			if (settings.configEnableRecentInputGUI)
			{
				// Show all the recognised game controlers and inputs to the user
				foreach (var joystick in joysticks.Select((val, idx) => new { idx, val }))
				{
					// Gets unique sorted input names from the recent input list
					var offsetList = new SortedSet<JoystickOffset>(joysticksRecentInputs[joystick.idx].Select(val => val.Offset).ToList().Distinct());
					// Just do a bunch of GUI stuff
					var style = new GUIStyle
					{
						alignment = TextAnchor.MiddleLeft,
						stretchWidth = false
					};
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
					foreach (var offset in offsetList)
						GUILayout.Label($"{(int)offset}({offset}), ", style);

					GUILayout.EndHorizontal();
				}
			}
		}

		// Draw to UMM settings
		static void OnGUI(UnityModManager.ModEntry modEntry)
		{
			GUILayout.Label("Debug");
			GUILayout.BeginVertical();
			settings.configEnableRecentInputGUI = GUILayout.Toggle(settings.configEnableRecentInputGUI, "Enable GUI");
			GUILayout.EndVertical();


			GUILayout.Label("Controls");
			GUILayout.BeginVertical();
			foreach (var configControl in settings.configControls.Select((val, idx) => new { idx, val }))
			{
				var style = new GUIStyle
				{
					alignment = TextAnchor.MiddleLeft,
					stretchWidth = false
				};
				style.normal.textColor = Color.white;
				style.normal.background = Texture2D.grayTexture;

				GUILayout.Label($"\t{((ControlType)configControl.idx).ToString()}");
				GUILayout.BeginVertical(style);

				configControl.val.Enabled = GUILayout.Toggle(configControl.val.Enabled, "Enabled");

				GUILayout.BeginHorizontal(GUILayout.Width(200));
				GUILayout.Label("Device ID", GUILayout.Width(100));
				configControl.val.DeviceId = int.Parse(GUILayout.TextField(configControl.val.DeviceId.ToString()));
				GUILayout.EndHorizontal();
				GUILayout.BeginHorizontal(GUILayout.Width(200));
				GUILayout.Label("Device Offset", GUILayout.Width(100));
				configControl.val.DeviceOffset = (JoystickOffset)int.Parse(GUILayout.TextField(((int)configControl.val.DeviceOffset).ToString()));
				GUILayout.EndHorizontal();
				configControl.val.InvertControl = GUILayout.Toggle(configControl.val.InvertControl, "Invert");

				GUILayout.EndVertical();
			}
			GUILayout.EndVertical();
		}

		static void OnSaveGUI(UnityModManager.ModEntry modEntry)
		{
			settings.Save(modEntry);
		}

	}

}
