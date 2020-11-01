﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;

namespace MountainProjectBot
{
    public enum HttpMethod
    {
        Get,
        Post,
        Put,
        Head,
        Delete,
        Patch,
        Options
    }

    public class Server
    {
        private TcpListener listener;
        private Thread thread;

        public bool IsAlive
        {
            get
            {
                return this.thread != null && this.thread.IsAlive;
            }
        }

        public int Port { get; private set; }

        public Server(int port)
        {
            listener = new TcpListener(IPAddress.Any, port);
            Port = port;
        }

        public void Start()
        {
            listener.Start();
            thread = new Thread(WaitForRequests);
            thread.Start();
        }

        private void WaitForRequests()
        {
            while (true)
            {
                string requestString = null;
                try
                {
                    TcpClient client = listener.AcceptTcpClient();

                    StreamReader sr = new StreamReader(client.GetStream());
                    StreamWriter sw = new StreamWriter(client.GetStream());

                    requestString = sr.ReadLine();

                    sw.WriteLine("HTTP/1.0 200 OK\n"); //Send ok response to requester

                    ServerRequest request = ServerRequest.Parse(requestString);
                    if (HandleRequest != null && request != null)
                    {
                        sw.WriteLine(HandleRequest.Invoke(request));
                    }

                    sw.Flush();

                    client.Close();
                }
                catch (Exception ex)
                {
                    if (requestString != null)
                    {
                        ex.Data["path"] = requestString;
                    }

                    ExceptionHandling?.Invoke(ex);
                }
            }
        }

        public void Stop()
        {
            listener.Stop();
            thread.Abort();
        }

        public Func<ServerRequest, string> HandleRequest { get; set; }

        public Action<Exception> ExceptionHandling { get; set; }
    }

    public class ServerRequest
    {
        public static ServerRequest Parse(string data)
        {
            if (!string.IsNullOrEmpty(data))
            {
                Match match = Regex.Match(data, @"^(?<method>[^\s]*)\s(?<page>[^\s]*)\s");

                if (Enum.TryParse(match.Groups["method"].ToString(), true, out HttpMethod method))
                {
                    return new ServerRequest
                    {
                        RequestMethod = method,
                        Path = match.Groups["page"].ToString().Trim('/'),
                    };
                }
            }

            return null;
        }

        public HttpMethod RequestMethod { get; set; }
        public string Path { get; set; }

        public bool IsDefaultPageRequest
        {
            get { return Path == ""; }
        }

        public bool IsFaviconRequest
        {
            get { return Path == "favicon.ico"; }
        }

        public Dictionary<string, string> GetParameters()
        {
            if (!Path.Contains("?"))
            {
                return new Dictionary<string, string>();
            }

            return Path.Split('?')[1].Split('&').Select(p => p.Split('=')).ToDictionary(kvp => kvp[0].ToLower(), kvp => kvp.Length > 1 ? WebUtility.HtmlDecode(kvp[1]) : "");
        }
    }
}