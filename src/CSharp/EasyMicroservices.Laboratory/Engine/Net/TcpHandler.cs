﻿using EasyMicroservices.Laboratory.Constants;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace EasyMicroservices.Laboratory.Engine.Net
{
    /// <summary>
    /// Handle the Tcp services
    /// </summary>
    public abstract class TcpHandler
    {
        /// <summary>
        /// 
        /// </summary>
        public TcpHandler(ResourceManager resourceManager)
        {
            _requestHandler = new RequestHandler(resourceManager);
        }

        /// <summary>
        /// 
        /// </summary>
        protected readonly RequestHandler _requestHandler;
        /// <summary>
        /// Start the Tcp listener
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        public Task Start(int port)
        {
            return InternalStart(port);
        }

        static Random _random = new Random();
        /// <summary>
        /// start on any random port
        /// </summary>
        /// <returns>port to listen</returns>
        public async Task<int> Start()
        {
            int port;
            while (true)
            {
                port = _random.Next(1111, 9999);
                try
                {
                    await InternalStart(port);
                    break;
                }
                catch
                {
                }
            }
            return port;
        }

        Task InternalStart(int port)
        {
            TcpListener tcpListener = new TcpListener(IPAddress.Any, port);
            tcpListener.Start();

            _ = Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    try
                    {
                        var tcpClient = await tcpListener.AcceptTcpClientAsync();
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await HandleTcpClient(tcpClient);
                            }
                            catch
                            {
                                tcpClient.Close();
                            }
                        });
                    }
                    catch
                    {

                    }
                }
            });
            return TaskHelper.GetCompletedTask();
        }

        /// <summary>
        /// Handle a Tcp client
        /// </summary>
        /// <param name="tcpClient"></param>
        /// <returns></returns>
        protected abstract Task HandleTcpClient(TcpClient tcpClient);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public async Task<string> ReadLineAsync(Stream stream)
        {
            List<byte> data = new List<byte>();
            do
            {
                byte[] bytes;
                if (data.LastOrDefault() == 13)
                {
                    bytes = await ReadBlockAsync(stream, 1);
                    data.AddRange(bytes);
                    if (bytes[0] == 10)
                        break;
                }
                else
                {
                    bytes = await ReadBlockAsync(stream, 2);
                    data.AddRange(bytes);
                    if (bytes[0] == 13 && bytes[1] == 10)
                        break;
                }
            }
            while (true);
            return Encoding.UTF8.GetString(data.ToArray()).TrimEnd();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<byte[]> ReadBlockAsync(Stream stream, int length)
        {
            byte[] result = new byte[length];
            int index = 0;
            do
            {
                var bytes = new byte[length];
                var readCount = await stream.ReadAsync(bytes, 0, bytes.Length);
                if (readCount == 0)
                    throw new Exception("Client Disconnected!");
                for (int i = 0; i < readCount; i++)
                {
                    result[i + index] = bytes[i];
                }
                index += readCount;
            }
            while (index < length);
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="firstLine"></param>
        /// <param name="requestHeaders"></param>
        /// <param name="requestBody"></param>
        /// <param name="stream"></param>
        /// <returns></returns>
        public async Task WriteResponseAsync(string firstLine, Dictionary<string, string> requestHeaders, string requestBody, Stream stream)
        {
            string responseBody = "No response found!";
            if (requestHeaders.TryGetValue(RequestTypeHeaderConstants.RequestTypeHeader, out string headerTypeValue))
            {
                switch (headerTypeValue)
                {
                    case RequestTypeHeaderConstants.GiveMeFullRequestHeaderValue:
                        {
                            responseBody = GetGiveMeFullRequestHeaderValueResponse(firstLine, requestHeaders, requestBody);
                            break;
                        }
                }
            }
            else
                responseBody = await _requestHandler.FindResponseBody(requestBody);
            var responseBodyBytes = Encoding.UTF8.GetBytes(responseBody);
            await stream.WriteAsync(responseBodyBytes, 0, responseBodyBytes.Length);
        }

        string GetGiveMeFullRequestHeaderValueResponse(string firstLine, Dictionary<string, string> requestHeaders, string requestBody)
        {
            StringBuilder responseBuilder = new();
            StringBuilder bodyBuilder = new();
            responseBuilder.AppendLine(DefaultResponse());
            bodyBuilder.AppendLine(firstLine);
            foreach (var header in requestHeaders.Where(x => !x.Key.Equals(RequestTypeHeaderConstants.RequestTypeHeader, StringComparison.OrdinalIgnoreCase)))
            {
                bodyBuilder.Append(header.Key);
                bodyBuilder.Append(": ");
                bodyBuilder.AppendLine(header.Value);
            }
            bodyBuilder.AppendLine();
            bodyBuilder.Append(requestBody);

            responseBuilder.AppendLine($"Content-Length: {bodyBuilder.Length}");
            responseBuilder.AppendLine();
            responseBuilder.Append(bodyBuilder);

            return responseBuilder.ToString();
        }

        string DefaultResponse()
        {
            return @$"HTTP/1.1 200 OK
Cache-Control: no-cache
Pragma: no-cache
Content-Type: text/plain; charset=utf-8
Vary: Accept-Encoding";
        }
    }
}