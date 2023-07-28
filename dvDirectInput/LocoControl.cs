using DV.HUD;
using System.Linq;
using static DV.HUD.InteriorControlsManager;

namespace dvDirectInput
{
	public static class LocoControl
	{
		public static void ApplyInputs()
		{
			while (Input.inputQueue.Count > 0)
			{
				// Dont bother doing anything if we arent in a loco
				if (!PlayerManager.Car?.IsLoco ?? true)
				{
					// Probbaly not going to get in a loco this game update so just clear the queue
					Input.inputQueue.Clear();
					break;
				}

				// Eat up all the queue items
				var input = Input.inputQueue.Dequeue();

				// Assign Inputs
				foreach (var configControl in Main.settings.configControls.Select((val, idx) => new { idx, val }))
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
	}
}
