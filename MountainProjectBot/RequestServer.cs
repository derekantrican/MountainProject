using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            Log("Starting server...");
            listener.Start();
            thread = new Thread(WaitForRequests);
            thread.Start();
            Log("Server started");
        }

        public StringWriter Activity { get; set; } = new StringWriter();
        private void Log(string itemToLog) => Activity.WriteLine($"[{DateTime.Now:yyyy.MM.dd.HH.mm.ss.fff}] {itemToLog}");

        private void WaitForRequests()
        {
            while (true)
            {
                string requestString = null;
                try
                {
                    Log("Waiting for client...");
                    TcpClient client = listener.AcceptTcpClient();
                    Log("Accepted client");

                    NetworkStream networkStream = client.GetStream();
                    Log("Opening streamreader...");
                    StreamReader sr = new StreamReader(networkStream);
                    Log("Opened streamreader");
                    Log("Opening streamwriter...");
                    StreamWriter sw = new StreamWriter(networkStream);
                    Log("Opened streamwriter");

                    Log($"Reading streamreader (CanRead: {networkStream.CanRead})...");
                    requestString = sr.ReadLine(); //This line may be the one hanging when the server stops responding
                    Log("Read streamreader");

                    Log($"Writing status (CanWrite: {networkStream.CanWrite})...");
                    sw.WriteLine("HTTP/1.0 200 OK\n"); //Send ok response to requester
                    Log("Wrote status");

                    Log("Parsing request...");
                    ServerRequest request = ServerRequest.Parse(requestString);
                    Log("Parsed request");
                    if (HandleRequest != null && request != null)
                    {
                        Log("Invoking HandleRequest...");
                        Log($"Request: {request.Path}");
                        sw.WriteLine(HandleRequest.Invoke(request));
                        Log("Invoked HandleRequest");
                    }

                    Log("Flushing streamwriter...");
                    sw.Flush();
                    Log("Flushed streamwriter");

                    Log("Closing client connection...");
                    client.Close();
                    Log("Closed client connection");
                }
                catch (Exception ex)
                {
                    if (requestString != null)
                    {
                        ex.Data["path"] = requestString;
                    }

                    Log("Invoking ExceptionHandling...");
                    ExceptionHandling?.Invoke(ex);
                    Log("Invoked ExceptionHandling");
                }
            }
        }

        public void Stop()
        {
            Log("Stopping server...");
            listener.Stop();
            thread.Abort();
            Log("Server stopped");
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
