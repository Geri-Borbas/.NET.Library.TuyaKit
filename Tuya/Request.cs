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


        // Packet protocol from https://github.com/codetheweb/tuyapi/wiki/Packet-Structure
        byte[] prefixBytes = new byte[]{ 0x00, 0x00, 0x55, 0xaa };
        byte[] versionBytes = new byte[]{ 0x00, 0x00, 0x00, 0x00 };
        byte[] commandBytes = new byte[]{ 0x00, 0x00, 0x00, 0x00 };
        byte[] payloadLengthBytes = new byte[]{ 0x00, 0x00, 0x00, 0x00 };

        // Payload (data, checksum, suffix).
        byte[] checksumBytes = new byte[]{ 0x00, 0x00, 0x00, 0x00 };
        byte[] suffixBytes = new byte[]{ 0x00, 0x00, 0xaa, 0x55 };

        // Command values from https://github.com/codetheweb/tuyapi/wiki/TUYA-Commands
        public enum Command
        {
            SetStatus = 0x07,
            GetStatus = 0x0a,
            GetSSIDList = 0x0b
        }


    #region Features

        public async Task<JObject> SendJSONObjectForCommandToDevice(object JSON, Command command, Device device, bool encrypt = false)
        { return await SendJSONStringForCommandToDevice(JsonConvert.SerializeObject(JSON), command, device, encrypt); }

        public async Task<JObject> SendJSONStringForCommandToDevice(string JSON, Command command, Device device, bool encrypt)
        {
            Log.Format("Request.SendJSONStringForCommandToDevice, JSON: `{0}`", JSON);

            // Request.
            byte[] dataBytes = (encrypt) ? EncryptedBytesFromJSONForDevice(JSON, device) : Encoding.UTF8.GetBytes(JSON);
            byte[] packetBytes = PacketFromDataForCommand(dataBytes, command);

            // Send.
            byte[] responsePacketBytes = await SendPacketToDevice(packetBytes, device);

            // Validate.
            if (IsValidPacket(responsePacketBytes) == false)
            { return null; }

            // Parse.
            string responseJSONString = DataStringFromPacket(responsePacketBytes);
            Log.Format("responseJSONString: `{0}`", responseJSONString);

            // Only if any.
            if (responseJSONString == string.Empty)
            return new JObject();

            // Create object.
            JObject responseJSONObject = JObject.Parse(responseJSONString);

            return responseJSONObject;
        }

    #endregion


    #region Communication

        public async Task<byte[]> SendPacketToDevice(byte[] packetBytes, Device device)
        {
            Log.Format("Request.SendDataToDevice(), packetBytes.Length: `{0}`", packetBytes.Length);

            using (TcpClient tcpClient = new TcpClient(device.IP, device.port))
            using (NetworkStream networkStream = tcpClient.GetStream())
            using (MemoryStream responseMemoryStream = new MemoryStream())
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
                return responseMemoryStream.ToArray();
            }
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

            // Lengths.
            int headerLength = prefixBytes.Length + versionBytes.Length + commandBytes.Length + payloadLengthBytes.Length;
            int minimumPayloadLength = checksumBytes.Length + suffixBytes.Length;
            int minimumPacketLength = headerLength + minimumPayloadLength;

            // Length.
            if (packetBytes.Length < minimumPacketLength)
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
            if (packetBytes.Length < headerLength + payloadLength)
            {
                Log.Format("Missing payload.");
                return false;
            }

            // Valid.
            return true;
        }

        protected string DataStringFromPacketWithLogs(byte[] packetBytes)
        {
            // Header.
            byte[] packetPrefixBytes = packetBytes.Take(prefixBytes.Length).ToArray();
            byte[] packetVersionBytes = packetBytes.Skip(prefixBytes.Length).Take(versionBytes.Length).ToArray();
            byte[] packetCommandBytes = packetBytes.Skip(prefixBytes.Length + versionBytes.Length).Take(commandBytes.Length).ToArray();
            byte[] packetPayloadLengthBytes = packetBytes.Skip(prefixBytes.Length + versionBytes.Length + commandBytes.Length).Take(payloadLengthBytes.Length).ToArray();

            // Lengths.
            int headerLength = prefixBytes.Length + versionBytes.Length + commandBytes.Length + payloadLengthBytes.Length;
            int suffixLength = checksumBytes.Length + suffixBytes.Length;

            // Data.
            byte[] packetPayloadBytes = packetBytes.Skip(headerLength).ToArray();
            byte[] packetDataBytes = packetPayloadBytes.Take(packetPayloadBytes.Length - suffixLength).ToArray();

            // Suffix.
            byte[] packetChecksumBytes = packetBytes.Skip(packetBytes.Length - suffixLength).Take(checksumBytes.Length).ToArray();
            byte[] packetSuffixBytes = packetBytes.Skip(packetBytes.Length - suffixBytes.Length).ToArray();

            // To string.
            byte[] packetDataBytesWithoutLeadingZeroes = packetDataBytes.SkipWhile((byte eachByte, int eachIndex) => eachByte == 0x00).ToArray();
            string packetDataString = Encoding.UTF8.GetString(packetDataBytesWithoutLeadingZeroes);

            // Log bytes.
            Log.Format("Request.DataFromPacketWithLogs(), packetPrefixBytes: `{0}`", BitConverter.ToString(packetPrefixBytes));
            Log.Format("Request.DataFromPacketWithLogs(), packetVersionBytes: `{0}`", BitConverter.ToString(packetVersionBytes));
            Log.Format("Request.DataFromPacketWithLogs(), packetCommandBytes: `{0}`", BitConverter.ToString(packetCommandBytes));
            Log.Format("Request.DataFromPacketWithLogs(), packetPayloadLengthBytes: `{0}`", BitConverter.ToString(packetPayloadLengthBytes));
            Log.Format("Request.DataFromPacketWithLogs(), packetPayloadBytes: `{0}`", BitConverter.ToString(packetPayloadBytes));
            Log.Format("Request.DataFromPacketWithLogs(), packetDataBytes: `{0}`", BitConverter.ToString(packetDataBytes));
            Log.Format("Request.DataFromPacketWithLogs(), packetChecksumBytes: `{0}`", BitConverter.ToString(packetChecksumBytes));
            Log.Format("Request.DataFromPacketWithLogs(), packetSuffixBytes: `{0}`", BitConverter.ToString(packetSuffixBytes));

            // Log lengths.
            int payloadLength = BitConverter.ToInt32(packetPayloadLengthBytes.Reverse().ToArray(), 0);
            Log.Format("Device.DataFromPacketWithLogs(), payloadLength: `{0}`", payloadLength);
            Log.Format("Device.DataFromPacketWithLogs(), packetPayloadBytes.Length: `{0}`", packetPayloadBytes.Length);
            Log.Format("Device.DataFromPacketWithLogs(), packetDataBytes.Length: `{0}`", packetDataBytes.Length);

            // Log string.
            Log.Format("Device.DataFromPacketWithLogs(), packetDataString: `{0}`", packetDataString);

            return packetDataString;
        }

        protected string DataStringFromPacket(byte[] packetBytes)
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

        protected byte[] PacketFromDataForCommand(byte[] dataBytes, Command command)
        {
            // Set command.
            commandBytes[3] = (byte)command;

            // Count payload length (data with checksum and suffix).
            payloadLengthBytes = BitConverter.GetBytes(dataBytes.Length + checksumBytes.Length + suffixBytes.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(payloadLengthBytes); // Big endian

            // Assemble packet.
            using (MemoryStream memoryStream = new MemoryStream())
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
        }

    #endregion


    #region Encryption

        // From https://github.com/codetheweb/tuyapi/blob/master/index.js#L300
        byte[] EncryptedBytesFromJSONForDevice(string JSON, Device device)
        {
            Log.Format("Request.EncryptedBytesFromJSONForDevice()");

            // Key.
            byte[] key = Encoding.UTF8.GetBytes(device.localKey);

            // Encrypt with key.
            string encryptedJSONBase64String;
            using (AesManaged aes = new AesManaged(){ Mode = CipherMode.ECB, Key = key })
            using (MemoryStream encryptedStream = new MemoryStream())
            using (CryptoStream cryptoStream = new CryptoStream(encryptedStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                byte[] JSONBytes = Encoding.UTF8.GetBytes(JSON);
                cryptoStream.Write(JSONBytes, 0, JSONBytes.Length);
                cryptoStream.Close();
                encryptedJSONBase64String = Convert.ToBase64String(encryptedStream.ToArray());
            }

            // Create hash.
            string hashString;
            using (MD5 md5 = MD5.Create())
            using (MemoryStream hashBaseStream = new MemoryStream())
            {
                byte[] encryptedPayload = Encoding.UTF8.GetBytes($"data={encryptedJSONBase64String}||lpv={device.protocolVersion}||");
                hashBaseStream.Write(encryptedPayload, 0, encryptedPayload.Length);
                hashBaseStream.Write(key, 0, key.Length);
                byte[] hashBytes = md5.ComputeHash(hashBaseStream.ToArray());
                string hash = BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLower();
                hashString = hash.Substring(8, 16);
            }

            // Stitch together.
            return Encoding.UTF8.GetBytes($"{device.protocolVersion}{hashString}{encryptedJSONBase64String}");
        }

    #endregion

    }
}