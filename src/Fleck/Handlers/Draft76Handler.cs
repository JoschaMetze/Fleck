using System;
using System.Collections.Generic;
using System.Linq;
#if !PORTABLE
using System.Security.Cryptography;
#else
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Windows.Storage.Streams;
//using Windows.Security.Cryptography;
//using Windows.Security.Cryptography.Core;
#endif
using System.Text;

namespace Fleck.Handlers
{
    public static class Draft76Handler
    {
        private const byte End = 255;
        private const byte Start = 0;
        private const int MaxSize = 1024 * 1024 * 5;
                
        public static IHandler Create(WebSocketHttpRequest request, Action<string> onMessage)
        {
            return new ComposableHandler
            {
                TextFrame = Draft76Handler.FrameText,
                Handshake = sub => Draft76Handler.Handshake(request, sub),
                ReceiveData = data => ReceiveData(onMessage, data)
            };
        }
        
        public static void ReceiveData(Action<string> onMessage, List<byte> data)
        {
            while (data.Count > 0)
            {
                if (data[0] != Start)
                    throw new WebSocketException(WebSocketStatusCodes.InvalidFramePayloadData);
                
                var endIndex = data.IndexOf(End);
                if (endIndex < 0)
                    return;
                
                if (endIndex > MaxSize)
                    throw new WebSocketException(WebSocketStatusCodes.MessageTooBig);
                
                var bytes = data.Skip(1).Take(endIndex - 1).ToArray();
                
                data.RemoveRange(0, endIndex + 1);
#if !PORTABLE
                var message = Encoding.UTF8.GetString(bytes);
#else
                var message = Encoding.UTF8.GetString(bytes, 0, endIndex - 1);
#endif

                onMessage(message);
            }
        }
        
        public static byte[] FrameText(string data)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            // wrap the array with the wrapper bytes
            var wrappedBytes = new byte[bytes.Length + 2];
            wrappedBytes[0] = Start;
            wrappedBytes[wrappedBytes.Length - 1] = End;
            Array.Copy(bytes, 0, wrappedBytes, 1, bytes.Length);
            return wrappedBytes;
        }
        
        public static byte[] Handshake(WebSocketHttpRequest request, string subProtocol)
        {
            FleckLog.Debug("Building Draft76 Response");
            
            var builder = new StringBuilder();
            builder.Append("HTTP/1.1 101 WebSocket Protocol Handshake\r\n");
            builder.Append("Upgrade: WebSocket\r\n");
            builder.Append("Connection: Upgrade\r\n");
            builder.AppendFormat("Sec-WebSocket-Origin: {0}\r\n",  request["Origin"]);
            builder.AppendFormat("Sec-WebSocket-Location: {0}://{1}{2}\r\n", request.Scheme, request["Host"], request.Path);

            if (subProtocol != null)
              builder.AppendFormat("Sec-WebSocket-Protocol: {0}\r\n", subProtocol);
                
            builder.Append("\r\n");
            
            var key1 = request["Sec-WebSocket-Key1"]; 
            var key2 = request["Sec-WebSocket-Key2"]; 
            var challenge = new ArraySegment<byte>(request.Bytes, request.Bytes.Length - 8, 8);
            
            var answerBytes = CalculateAnswerBytes(key1, key2, challenge);
#if !PORTABLE
            byte[] byteResponse = Encoding.ASCII.GetBytes(builder.ToString());
#else
            byte[] byteResponse = Encoding.GetEncoding("ISO-8859-1").GetBytes(builder.ToString());
#endif
            int byteResponseLength = byteResponse.Length;
            Array.Resize(ref byteResponse, byteResponseLength + answerBytes.Length);
            Array.Copy(answerBytes, 0, byteResponse, byteResponseLength, answerBytes.Length);
            
            return byteResponse;
        }
        
        public static byte[] CalculateAnswerBytes(string key1, string key2, ArraySegment<byte> challenge)
        {
            byte[] result1Bytes = ParseKey(key1);
            byte[] result2Bytes = ParseKey(key2);

            var rawAnswer = new byte[16];
            Array.Copy(result1Bytes, 0, rawAnswer, 0, 4);
            Array.Copy(result2Bytes, 0, rawAnswer, 4, 4);
            Array.Copy(challenge.Array, challenge.Offset, rawAnswer, 8, 8);

#if PORTABLE

            IDigest hash = new MD5Digest();

            byte[] result = new byte[hash.GetDigestSize()];


            hash.BlockUpdate(rawAnswer, 0, rawAnswer.Length);

            hash.DoFinal(result, 0);


            return result;

            //// Convert the message string to binary data.
            //IBuffer buffUtf8Msg = CryptographicBuffer.CreateFromByteArray(rawAnswer);

            //// Create a HashAlgorithmProvider object.
            //HashAlgorithmProvider objAlgProv = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Md5);

            //// Demonstrate how to retrieve the name of the hashing algorithm.
            //String strAlgNameUsed = objAlgProv.AlgorithmName;

            //// Hash the message.
            //IBuffer buffHash = objAlgProv.HashData(buffUtf8Msg);

            //// Verify that the hash length equals the length specified for the algorithm.
            //if (buffHash.Length != objAlgProv.HashLength)
            //{
            //    throw new Exception("There was an error creating the hash");
            //}

            //// Convert the hash to a string (for display).
            //String strHashBase64 = CryptographicBuffer.EncodeToBase64String(buffHash);

            //byte[] resultArray = new byte[buffHash.Length];
            //CryptographicBuffer.CopyToByteArray(buffHash, out resultArray);

            //return resultArray;
#else
            return MD5.Create().ComputeHash(rawAnswer);
#endif
        }

        private static byte[] ParseKey(string key)
        {

#if !PORTABLE
            int spaces = key.Count(x => x == ' ');
            var digits = new String(key.Where(Char.IsDigit).ToArray());
#else
            int spaces = key.ToCharArray().Count(x => x == ' ');
            var digits = new String(key.ToCharArray().Where(Char.IsDigit).ToArray());
#endif
            var value = (Int32)(Int64.Parse(digits) / spaces);

            byte[] result = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(result);
            return result;
        }
    }
}
