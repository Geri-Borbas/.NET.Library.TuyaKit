//
// Copyright (c) 2017 Geri Borbás http://www.twitter.com/_eppz
//
//  Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
//  The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
using System;


namespace EPPZ
{


    using Tuya;
    using Tuya.Devices;


    [Serializable]
	public class App
	{


		public Woox_R4026 socket;


        public App()
        {
            socket = new Woox_R4026()
            {
                // Network properties.
                IP = "192.168.0.106",
                port = 6668,
                protocolVersion = "3.1",

                // Device properties.   
				name = "Lounge Light Socket",
		        devId = "58205000840d8e46ebb0",
		        gwId = "58205000840d8e46ebb0",
		        productId = "TEZ6oluErOidJz6L",
		        localKey = "c914fffc4755fc93"
            };
        }


		public async void GetIsSocketOn()
		{
			bool isOn = await socket.GetIsOn();
			Log.Format("GetIsSocketOn: `{0}`", isOn);
		}

		public void TurnSocketOff()
		{ socket.TurnOff(); }

		public void TurnSocketOn()
		{ socket.TurnOn(); }

		public void ToggleSocket()
		{ socket.Toggle(); }
	}
}