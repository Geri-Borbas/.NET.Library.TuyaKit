//
// Copyright (c) 2017 Geri Borbás http://www.twitter.com/_eppz
//
//  Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
//  The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace EPPZ.Tuya.Devices
{


    [Serializable]
    public class Woox_R4026 : Device
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
					["1"] = false,
					["2"] = 0
				}
			);

			Log.Format("response: `{0}`", response);	
		}

		public async void TurnOn()
		{
			Dictionary<string, object> response = await Set(
				new Dictionary<string, object>
				{
					["1"] = true,
					["2"] = 0
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