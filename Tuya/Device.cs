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
        public string protocolVersion = "3.1";

        // Device properties.  
        public string name; 
		public string devId;
		public string gwId;
		public string productId;
		public string localKey;


    #region Accessors

        public async Task<Dictionary<string, object>> Get()
        {
            // Get the response.
            JObject response = await new Request().SendJSONObjectForCommandToDevice(
				new Dictionary<string, object>
            	{
                	["gwId"] = this.gwId,
                	["devId"] = this.devId
            	},
				Request.Command.GetStatus,
				this);

            // Pick "dps" only (if any).
            return response["dps"].ToObject<Dictionary<string, object>>();
        }

        public async Task<Dictionary<string, object>> Set(Dictionary<string, object> dps)
        {
            int epoch = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            JObject response = await new Request().SendJSONObjectForCommandToDevice(
				new Dictionary<string, object>
				{
					["t"] = epoch,
					["devId"] = this.devId,
					["dps"] = dps,
					["uid"] = ""
				},
				Request.Command.SetStatus,
				this,
				true);

            // Return (if any).
            return response.ToObject<Dictionary<string, object>>();            
        }

    #endregion


    }
}