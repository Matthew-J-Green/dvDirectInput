using SharpDX.DirectInput;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityModManagerNet;
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

		public void Render()
		{
			GUILayout.Label("Debug");
			GUILayout.BeginVertical();
			this.configEnableRecentInputGUI = GUILayout.Toggle(this.configEnableRecentInputGUI, "Enable GUI");
			GUILayout.EndVertical();


			GUILayout.Label("Controls");
			GUILayout.BeginVertical();
			foreach (var configControl in this.configControls.Select((val, idx) => new { idx, val }))
			{
				var style = new GUIStyle
				{
					alignment = TextAnchor.MiddleLeft,
					stretchWidth = false
				};
				style.normal.textColor = Color.white;
				style.normal.background = Texture2D.grayTexture;

				GUILayout.Label($"\t{(ControlType)configControl.idx}");
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
	}
}
