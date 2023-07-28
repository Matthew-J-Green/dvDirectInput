using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace dvDirectInput
{
	public static class Input
	{
		public static List<Joystick> joysticks = new();
		public static Queue<Input.InputItem> inputQueue = new();
		public static List<Queue<JoystickUpdate>> joysticksRecentInputs = new();

		public class InputItem
		{
			public Joystick JoystickObj { get; set; }
			public JoystickOffset Offset { get; set; }
			public int Value { get; set; }
			public int Timestamp { get; set; }

			// There are 3 types of inputs with associated ranges. I assume the DirectInput uses these values for all input devices
			// Axes 0 - 65535
			// Button 0, 128
			// POV -1 (released), 0 (up), 4500, 9000(right), 13500, 18000(down), 22500, 27000(left), 31500
			public float NormalisedValue()
			{
				var inputFlags = JoystickObj.GetObjectInfoByOffset((int)Offset).ObjectId.Flags;

				if ((inputFlags & DeviceObjectTypeFlags.Axis) != 0)
					return (float)Value / 65535;

				if ((inputFlags & DeviceObjectTypeFlags.Button) != 0)
					return (float)Value / 128;

				return 0;
			}

			public override string ToString()
			{
				return string.Format(CultureInfo.InvariantCulture, $"ID: {JoystickObj.Properties}, Offset: {Offset}, Value: {Value}, Normalised Value {NormalisedValue()}, Timestamp {Timestamp}");
			}
		}

		// Initialise all Direct Input game controllers and associated queues
		public static void Initialise()
		{
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

				if (Main.settings.configEnableRecentInputGUI)
					JoystickDebug(device, joystick);
			}

			if (joysticks.Count() == 0)
				Main.mod.Logger.Warning($"No input devices found");
		}

		public static void GetInputs()
		{
			int currentTimestamp = 0;

			// Grab inputs for all controllers
			foreach (var joystick in joysticks.Select((val, idx) => new { idx, val }))
			{
				try { joystick.val.Poll(); } catch { continue; }
				foreach (var data in joystick.val.GetBufferedData())
				{
					// Chuck all the inputs on a queue
					var input = new InputItem() { JoystickObj = joystick.val, Offset = data.Offset, Value = data.Value, Timestamp = data.Timestamp };
					inputQueue.Enqueue(input);

					// GUI Logic - Copy of inputs
					if (Main.settings.configEnableRecentInputGUI)
					{
						Main.mod.Logger.Log($"{input}");
						joysticksRecentInputs[joystick.idx].Enqueue(data);
						currentTimestamp = data.Timestamp;
					}
				}
				// GUI Logic - Remove any inputs if they have been displayed for a suitable period
				if (Main.settings.configEnableRecentInputGUI)
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
		}

		// Just go log a bunch of properties and fields of the joystick and its objects
		public static void JoystickDebug(DeviceInstance device, Joystick joystick)
		{
			Main.mod.Logger.Log($"");
			Main.mod.Logger.Log($"Device Fields");
			foreach (var field in device.GetType().GetFields())
			{
				Main.mod.Logger.Log($"{field.Name}, {field.GetValue(device)}");
			}

			Main.mod.Logger.Log($"");
			Main.mod.Logger.Log($"Joystick Properties");
			foreach (var prop in joystick.GetType().GetProperties())
			{
				Main.mod.Logger.Log($"Joystick Properties for {prop.Name}");
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
						Main.mod.Logger.Log($"{prop.Name}, {subprop.Name}, {val}");
					}
				}
				Main.mod.Logger.Log($"");
				Main.mod.Logger.Log($"Joystick Fields for {prop.Name}");
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
						Main.mod.Logger.Log($"{prop.Name}, {field.Name}, {val}");
					}
				}
				Main.mod.Logger.Log($"");
			}

			Main.mod.Logger.Log($"");
			Main.mod.Logger.Log($"Joystick Objects");
			foreach (var obj in joystick.GetObjects())
			{
				Main.mod.Logger.Log($"Joystick Object Fields");
				foreach (var field in obj.GetType().GetFields())
				{
					Main.mod.Logger.Log($"ID: {joystick.Properties.JoystickId}, Device: {joystick.Properties.ProductName}, {field.Name}: {field.GetValue(obj)}");
				}

				Main.mod.Logger.Log($"Joystick Object Properties");
				Main.mod.Logger.Log($"Deadzone: {Main.Try<ObjectProperties>(() => joystick.GetObjectPropertiesById(obj.ObjectId).DeadZone.ToString())}");
				Main.mod.Logger.Log($"Granularity: {Main.Try<ObjectProperties>(() => joystick.GetObjectPropertiesById(obj.ObjectId).Granularity.ToString())}");
				Main.mod.Logger.Log($"LogicalRange: {Main.Try<ObjectProperties>(() => joystick.GetObjectPropertiesById(obj.ObjectId).LogicalRange.Minimum.ToString())}, {Main.Try<ObjectProperties>(() => joystick.GetObjectPropertiesById(obj.ObjectId).LogicalRange.Maximum.ToString())}");
				Main.mod.Logger.Log($"PhysicalRange: {Main.Try<ObjectProperties>(() => joystick.GetObjectPropertiesById(obj.ObjectId).PhysicalRange.Minimum.ToString())}, {Main.Try<ObjectProperties>(() => joystick.GetObjectPropertiesById(obj.ObjectId).PhysicalRange.Maximum.ToString())}");
				Main.mod.Logger.Log($"Range: {Main.Try<ObjectProperties>(() => joystick.GetObjectPropertiesById(obj.ObjectId).Range.Minimum.ToString())}, {Main.Try<ObjectProperties>(() => joystick.GetObjectPropertiesById(obj.ObjectId).Range.Maximum.ToString())}");
				Main.mod.Logger.Log($"Saturation: {Main.Try<ObjectProperties>(() => joystick.GetObjectPropertiesById(obj.ObjectId).Saturation.ToString())}");
			}
			Main.mod.Logger.Log($"");
		}

		// Display recent inputs to the user
		public static void RenderRecentInputs()
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
}
