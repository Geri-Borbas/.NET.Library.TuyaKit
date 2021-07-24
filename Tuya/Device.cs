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
using System.Diagnostics;
using System.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace EPPZ.Tuya
{

    [Serializable]
    public class Device
    {
        // Network properties.
        public string IP;
        public int port = 6668;
        public string protocolVersion = "3.3";

        // Device properties.  
        public string name;
        public string devId;
        public string productId;
        public string localKey;


        #region Accessors

        public async Task<Dictionary<string, object>> Get(bool schema = false)
        {
            int epoch = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            Dictionary<string, object> nulldps = new Dictionary<string, object>();
            // Get the response.
            JObject response;
            if (!schema)
            {
                response = await new Request().SendJSONObjectForCommandToDevice(
                new Dictionary<string, object>
                {
                    ["gwId"] = this.devId,
                    ["devId"] = this.devId,
                    ["t"] = epoch,
                    ["dps"] = nulldps,
                    ["uid"] = this.devId
                },
                Request.Command.GetStatus,
                this, true);
                return response["dps"].ToObject<Dictionary<string, object>>();
            }
            else
            {
                response = await new Request().SendJSONObjectForCommandToDevice(
                new Dictionary<string, object>
                {
                    ["gwId"] = this.devId,
                    ["devId"] = this.devId,
                    ["t"] = epoch,
                    ["dps"] = nulldps,
                    ["uid"] = this.devId,
                    ["schema"] = true
                },
                Request.Command.GetStatus,
                this, true);
                return response.ToObject<Dictionary<string, object>>();
            }
        }

        public async Task<Dictionary<string, object>> Set(Dictionary<string, object> dps)
        {

            int epoch = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            JObject response = await new Request().SendJSONObjectForCommandToDevice(
                new Dictionary<string, object>
                {
                    ["devId"] = this.devId,
                    ["gwId"] = this.devId,
                    ["uid"] = "",
                    ["t"] = epoch,
                    ["dps"] = dps
                },
                Request.Command.SetStatus,
                this,
                true);

            // Return (if any).
            return response.ToObject<Dictionary<string, object>>();
        }

        private async Task<string> getFirst(Device dev)
        {
            Dictionary<string, object> dps = await dev.Get();
            Console.WriteLine("Auto Power Key : " + dps.First().Key);
            return dps.First().Key;
        }


        public async void TurnOffAuto(Device dev)
        {
            string auto = await getFirst(dev);
            Dictionary<string, object> response = await dev.Set(
                new Dictionary<string, object>
                {
                    [auto] = false
                }
            );

            Log.Format("response: `{0}`", response);
        }

        public async void TurnOnAuto(Device dev)
        {
            string auto = await getFirst(dev);
            Dictionary<string, object> response = await dev.Set(
                new Dictionary<string, object>
                {
                    [auto] = true
                }
            );

            Log.Format("response: `{0}`", response);
        }

        public async void ToggleAuto(Device dev)
        {
            Dictionary<string, object> dps = await dev.Get();
            string auto = await getFirst(dev);

            bool isOn = (bool)dps[auto];
            if (isOn) TurnOffAuto(dev); else TurnOnAuto(dev);

        }

        #endregion
    }
}