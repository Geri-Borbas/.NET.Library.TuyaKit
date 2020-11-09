using System;
using System.Collections.Generic;
using System.Threading.Tasks;

//LIGHT VARIABLES
/*					["1"] = true,
					["2"] = "colour",
					["3"] = 25,
					["5"] = "0000ff00f0ffff",
					["6"] = "00ff0000000000",
					["7"] = "ffff500100ff00",
					["8"] = "ffff8003ff000000ff000000ff000000000000000000",
					["9"] = "ffff5001ff0000",
					["10"] = "ffff0505ff000000ff00ffff00ff00ff0000ff000000",
					["11"] = ""
					
*/

namespace EPPZ.Tuya.Devices
{


	[Serializable]
	public class Vtac_5011 : Device
	{


		public async Task<bool> GetIsOn()
		{
			Dictionary<string, object> dps = await Get();
			if (dps.ContainsKey("1")) return (bool)dps["1"];
			return false;
		}

		public async void TurnOff()
		{
			Dictionary<string, object> response = await Set(
				new Dictionary<string, object>
				{
					["1"] = false
				}
			);

			Log.Format("response: `{0}`", response);
		}

		public async void TurnOn()
		{
			Dictionary<string, object> response = await Set(
				new Dictionary<string, object>
				{
					["1"] = true
				}
			);

			Log.Format("response: `{0}`", response);
		}

		public async void setColour(int R, int G, int B)
		{
			Dictionary<string, object> response;
			if (R == 255 && G == 255 && B == 255)
			{
				response = await Set(
				new Dictionary<string, object>
				{
					["1"] = true,
					["2"] = "white",
				});
			} else {
				response = await Set(
				   new Dictionary<string, object>
				   {
					   ["1"] = true,
					   ["2"] = "colour",
					   ["5"] = R.ToString("X2").ToLower() + G.ToString("X2").ToLower() + B.ToString("X2").ToLower() + "0000ffff",
					   ["6"] = "00ff0000000000",
					   ["7"] = "ffff500100ff00",
					   ["8"] = "ffff8003ff000000ff000000ff000000000000000000",
					   ["9"] = "ffff5001ff0000",
					   ["10"] = "ffff0505ff000000ff00ffff00ff00ff0000ff000000",
					   ["11"] = ""
				   });
			}

			Log.Format("response: `{0}`", response);
		}

		public async void setBrightness(int brightness)
		{
			Dictionary<string, object> response = await Set(
				new Dictionary<string, object>
				{
					["1"] = true,
					["3"] = brightness
				}
			);

			Log.Format("response: `{0}`", response);
		}

		public async void Toggle()
		{
			Dictionary<string, object> dps = await Get();
			if (dps.ContainsKey("1"))
			{
				bool isOn = (bool)dps["1"];
				if (isOn) TurnOff(); else TurnOn();
			}
		}
	}
}