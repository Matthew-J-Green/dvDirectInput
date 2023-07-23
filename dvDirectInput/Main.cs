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
	public static class Main
	{
		public static UnityModManager.ModEntry mod;
		public static Settings settings = new();

		public static List<Joystick> joysticks = new();
		public static Queue<Input> inputQueue = new();
		public static List<Queue<JoystickUpdate>> joysticksRecentInputs = new();
		public static List<int> acceptableIDs = new();

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

			Input.Initialise();

			mod.OnUpdate = OnUpdate;
			mod.OnGUI = OnGUI;
			mod.OnSaveGUI = OnSaveGUI;
			mod.OnFixedGUI = OnFixedGUI;

			return true;
		}

		// Every Frame
		static void OnUpdate(UnityModManager.ModEntry modEntry, float dt)
		{
			Input.GetInputs();
			LocoControl.ApplyInputs();
		}

		// Render to game window
		static void OnFixedGUI(UnityModManager.ModEntry modEntry)
		{

			if (settings.configEnableRecentInputGUI)
				Input.RenderRecentInputs();
		}

		// UMM settings window
		static void OnGUI(UnityModManager.ModEntry modEntry)
		{
			settings.Render();
		}

		// UMM settings window save button
		static void OnSaveGUI(UnityModManager.ModEntry modEntry)
		{
			settings.Save(modEntry);
		}

		public static string Try<T>(Func<string> func)
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

	}

}
