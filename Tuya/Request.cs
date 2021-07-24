//
// Copyright (c) 2017 Geri Borbás http://www.twitter.com/_eppz
//
//  Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
//  The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace EPPZ.Tuya
{


    public class Request
    {
        int headerSize = 16;

        // Packet protocol from https://github.com/codetheweb/tuyapi/wiki/Packet-Structure
        byte[] prefixBytes = new byte[] { 0x00, 0x00, 0x55, 0xaa };
        byte[] versionBytes = new byte[] { 0x00, 0x00, 0x00, 0x00 };
        byte[] commandBytes = new byte[] { 0x00, 0x00, 0x00, 0x00 };
        byte[] payloadLengthBytes = new byte[] { 0x00, 0x00, 0x00, 0x00 };
        byte[] payloadLengthBytesReq = new byte[] { 0x00, 0x00, 0x00, 0x00 };
        byte[] spacingBytes = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        byte[] ver33Bytes = new byte[] { 0x33, 0x2e, 0x33 };

        // Payload (data, checksum, suffix).
        byte[] checksumBytes = new byte[] { 0x00, 0x00, 0x00, 0x00 };
        byte[] suffixBytes = new byte[] { 0x00, 0x00, 0xaa, 0x55 };

        // Command values from https://github.com/codetheweb/tuyapi/wiki/TUYA-Commands
        public enum Command
        {
            SetStatus = 0x07,
            GetStatus = 0x0a,
            GetSSIDList = 0x0b
        }

        private byte[] SubArray(byte[] array, int startIndex, int endIndex)
        {
            byte[] result = new byte[endIndex - startIndex];
            Array.Copy(array, startIndex, result, 0, endIndex - startIndex);
            return result;
        }

        #region Features

        public async Task<JObject> SendJSONObjectForCommandToDevice(object JSON, Command command, Device device, bool encrypt = false)
        { return await SendJSONStringForCommandToDevice(JsonConvert.SerializeObject(JSON), command, device, encrypt); }

        public async Task<JObject> SendJSONStringForCommandToDevice(string JSON, Command command, Device device, bool encrypt)
        {
            Log.Format("Request.SendJSONStringForCommandToDevice, JSON: `{0}`", JSON);

            // Request.
            byte[] dataBytes = (encrypt) ? Encrypt(JSON, device) : Encoding.UTF8.GetBytes(JSON);
            byte[] packetBytes = PacketFromDataForCommand(dataBytes, command, device);

            // Send.
            byte[] responsePacketBytes = await SendPacketToDevice(packetBytes, device);

            // Validate.
            if (IsValidPacket(responsePacketBytes) == false)
            { return null; }

            // Parse.
            string responseJSONString = DataStringFromPacket(responsePacketBytes, device);
            Log.Format("responseJSONString: `{0}`", responseJSONString);

            // Only if any.
            if (responseJSONString == string.Empty || responseJSONString.Length <= 1)
                return new JObject();

            // Create object.
            JObject responseJSONObject = JObject.Parse(responseJSONString);

            return responseJSONObject;
        }

        #endregion


        #region Communication

        public async Task<byte[]> SendPacketToDevice(byte[] packetBytes, Device device)
        {
            bool dataSuccess = false;
        START:
            Log.Format("Request.SendDataToDevice(), packetBytes.Length: `{0}`", packetBytes.Length);

            TcpClient tcpClient = new TcpClient();
            while (!tcpClient.Connected)
            {
                try
                {
                    tcpClient.Connect(device.IP, device.port);
                }
                catch (Exception) { }
            }
            byte[] responseStream;
            using (tcpClient)
            using (NetworkStream networkStream = tcpClient.GetStream())
            using (MemoryStream responseMemoryStream = new MemoryStream())
            {
                try
                {
                    // Write request.
                    await networkStream.WriteAsync(packetBytes, 0, packetBytes.Length);

                    // Read response.
                    byte[] responseBytes = new byte[1024];
                    int numberOfBytesResponded = await networkStream.ReadAsync(responseBytes, 0, responseBytes.Length);
                    responseMemoryStream.Write(responseBytes, 0, numberOfBytesResponded);

                    // Close client.
                    networkStream.Close();
                    tcpClient.Close();

                    // Return byte array.
                    responseStream = responseMemoryStream.ToArray();
                    dataSuccess = true;
                }
                catch (Exception) { responseStream = new byte[] { 0x00 }; }
            }
            if (!dataSuccess)
            {
                System.Threading.Thread.Sleep(100);
                goto START;
            }
            tcpClient.Close();
            tcpClient.Dispose();
            return responseStream;
        }

        #endregion


        #region Packet assembly

        bool IsValidPacket(byte[] packetBytes)
        {
            // Emptyness.
            if (packetBytes == null)
            {
                Log.Format("Empty packet.");
                return false;
            }

            // Length.
            if (packetBytes.Length < 24)
            {
                Log.Format("Invalid packet length.");
                return false;
            }

            // Prefix.
            if (packetBytes.Take(4).SequenceEqual(prefixBytes) == false)
            {
                Log.Format("Invalid prefix.");
                return false;
            }

            // Suffix.
            if (packetBytes.Skip(packetBytes.Length - 4).Take(4).SequenceEqual(suffixBytes) == false)
            {
                Log.Format("Invalid suffix.");
                return false;
            }

            // Payload.
            int payloadLength = BitConverter.ToInt32(packetBytes.Skip(prefixBytes.Length + versionBytes.Length + commandBytes.Length).Take(payloadLengthBytes.Length).Reverse().ToArray(), 0);
            if (packetBytes.Length < payloadLength)
            {
                Log.Format("Missing payload.");
                return false;
            }

            // Valid.
            return true;
        }

        protected string DataStringFromPacket(byte[] packetBytes, Device dev)
        {
            if (dev.protocolVersion == "3.1")
            {
                // Lengths.
                int headerLength = prefixBytes.Length + versionBytes.Length + commandBytes.Length + payloadLengthBytes.Length;
                int suffixLength = checksumBytes.Length + suffixBytes.Length;

                // Data.
                byte[] packetPayloadBytes = packetBytes.Skip(headerLength).ToArray(); // Skip header
                byte[] packetDataBytes = packetPayloadBytes.Take(packetPayloadBytes.Length - suffixLength).ToArray(); // Trim suffix

                // To string.
                byte[] packetDataBytesWithoutLeadingZeroes = packetDataBytes.SkipWhile((byte eachByte, int eachIndex) => eachByte == 0x00).ToArray();
                string packetDataString = Encoding.UTF8.GetString(packetDataBytesWithoutLeadingZeroes);

                return packetDataString;
            } 
            else
            {
                // Remove prefix and suffix
                packetBytes = SubArray(packetBytes, 4, packetBytes.Length - 4);

                // Get length of payload from returned data
                byte[] payloadLengthBytes = new byte[] { packetBytes[8], packetBytes[9], packetBytes[10], packetBytes[11] };

                if (BitConverter.IsLittleEndian)
                    Array.Reverse(payloadLengthBytes);

                int payloadLength = BitConverter.ToInt32(payloadLengthBytes, 0);


                // Get return code
                byte returnCode = packetBytes[15];

                byte[] payload;

                // Extract payload from data
                try
                {
                    if (returnCode == 0)
                    {
                        payload = SubArray(packetBytes, headerSize, headerSize + payloadLength - 12);
                    }
                    else
                    {
                        payload = SubArray(packetBytes, headerSize + 4, headerSize + payloadLength - 12);
                    }
                }
                catch (Exception)
                {
                    payload = new byte[] { 0x00 };
                }

                // Decrypt payload
                string d = Encoding.UTF8.GetString(payload);
                try
                {
                    d = Decrypt(payload, dev);
                }
                catch (Exception) { }

                return d;
            }
        }

        protected byte[] PacketFromDataForCommand(byte[] dataBytes, Command command, Device dev)
        {
            // Set command.
            commandBytes[3] = (byte)command;

            // Count payload length
            int payloadLengthInt = ver33Bytes.Length + spacingBytes.Length + dataBytes.Length + checksumBytes.Length + suffixBytes.Length;
            int payloadLengthIntReq = dataBytes.Length + checksumBytes.Length + suffixBytes.Length;

            payloadLengthBytes = BitConverter.GetBytes(payloadLengthInt);
            if (BitConverter.IsLittleEndian) Array.Reverse(payloadLengthBytes); // Big endian

            payloadLengthBytesReq = BitConverter.GetBytes(payloadLengthIntReq);
            if (BitConverter.IsLittleEndian) Array.Reverse(payloadLengthBytesReq); // Big endian

            // Assemble packet.
            using (MemoryStream memoryStream = new MemoryStream())
            {
                if (dev.protocolVersion == "3.1")
                {
                    // Header (prefix, version, command, payload length).
                    memoryStream.Write(prefixBytes, 0, prefixBytes.Length);
                    memoryStream.Write(versionBytes, 0, versionBytes.Length);
                    memoryStream.Write(commandBytes, 0, commandBytes.Length);
                    memoryStream.Write(payloadLengthBytes, 0, payloadLengthBytes.Length);

                    // Payload (data, checksum, suffix).
                    memoryStream.Write(dataBytes, 0, dataBytes.Length);
                    memoryStream.Write(checksumBytes, 0, checksumBytes.Length);
                    memoryStream.Write(suffixBytes, 0, suffixBytes.Length);

                    return memoryStream.ToArray();
                } 
                else
                {
                    // Header (prefix, version, command, payload length).
                    memoryStream.Write(prefixBytes, 0, prefixBytes.Length);
                    memoryStream.Write(versionBytes, 0, versionBytes.Length);
                    memoryStream.Write(commandBytes, 0, commandBytes.Length);

                    //Add 3.3 header
                    if (command == Command.SetStatus)
                    {
                        memoryStream.Write(payloadLengthBytes, 0, payloadLengthBytes.Length);
                        memoryStream.Write(ver33Bytes, 0, ver33Bytes.Length);
                        memoryStream.Write(spacingBytes, 0, spacingBytes.Length);
                    }
                    else
                    {
                        memoryStream.Write(payloadLengthBytesReq, 0, payloadLengthBytesReq.Length);
                    }

                    // Payload (data, checksum, suffix).
                    memoryStream.Write(dataBytes, 0, dataBytes.Length);

                    byte[] checksumTarget = memoryStream.ToArray();
                    uint crc = Crc32.crc32(checksumTarget) & 0xFFFFFFFF;
                    checksumBytes = Crc32.intToBytes(crc);
                    memoryStream.Write(checksumBytes, 0, checksumBytes.Length);

                    memoryStream.Write(suffixBytes, 0, suffixBytes.Length);

                    return memoryStream.ToArray();
                }
            }
        }

        #endregion


        #region Encryption

        // From https://github.com/codetheweb/tuyapi/blob/master/index.js#L300
        byte[] Encrypt(string JSON, Device device)
        {
            Log.Format("Data.Encrypt()");

            // Key.
            byte[] key = Encoding.UTF8.GetBytes(device.localKey);

            // Encrypt with key.
            string encryptedJSON;
            using (AesManaged aes = new AesManaged() { Mode = CipherMode.ECB, Key = key })
            using (MemoryStream encryptedStream = new MemoryStream())
            using (CryptoStream cryptoStream = new CryptoStream(encryptedStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                byte[] JSONBytes = Encoding.UTF8.GetBytes(JSON);
                cryptoStream.Write(JSONBytes, 0, JSONBytes.Length);
                cryptoStream.FlushFinalBlock();
                cryptoStream.Close();
                if (device.protocolVersion == "3.1")
                    encryptedJSON = Convert.ToBase64String(encryptedStream.ToArray()); // Proceed with 3.1 algorithm.
                else
                    return encryptedStream.ToArray(); // Return 3.3 syntax encrypted bytes.
            }

            // Create hash if 3.1.
            string hashString;
            using (MD5 md5 = MD5.Create())
            using (MemoryStream hashBaseStream = new MemoryStream())
            {
                byte[] encryptedPayload = Encoding.UTF8.GetBytes($"data={encryptedJSON}||lpv={device.protocolVersion}||");
                hashBaseStream.Write(encryptedPayload, 0, encryptedPayload.Length);
                hashBaseStream.Write(key, 0, key.Length);
                byte[] hashBytes = md5.ComputeHash(hashBaseStream.ToArray());
                string hash = BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLower();
                hashString = hash.Substring(8, 16);
            }
            

            // Stitch together if 3.1.
            return Encoding.UTF8.GetBytes($"{device.protocolVersion}{hashString}{encryptedJSON}");
        }

        string Decrypt(byte[] data, Device device)
        {
            Log.Format("Data.Decrypt()");

            // Key.
            byte[] xkey = Encoding.UTF8.GetBytes(device.localKey);

            // Decrypt with key.
            using (AesManaged aes = new AesManaged() { Mode = CipherMode.ECB, Key = xkey })
            using (MemoryStream DecryptedStream = new MemoryStream())
            using (CryptoStream cryptoStream = new CryptoStream(DecryptedStream, aes.CreateDecryptor(), CryptoStreamMode.Write))
            {
                byte[] JSONBytes = data;
                cryptoStream.Write(JSONBytes, 0, JSONBytes.Length);
                cryptoStream.FlushFinalBlock();
                cryptoStream.Close();
                return Encoding.UTF8.GetString(DecryptedStream.ToArray());
            }
        }

        #endregion

    }
}