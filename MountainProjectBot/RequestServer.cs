using System;
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

        public StringWriter Activity { get; set; } = new StringWriter();

        private void WaitForRequests()
        {
            void log(string itemToLog) => Activity.WriteLine($"[{DateTime.Now}] {itemToLog}");

            while (true)
            {
                string requestString = null;
                try
                {
                    log("Waiting for client...");
                    TcpClient client = listener.AcceptTcpClient();
                    log("Accepted client");

                    log("Opening streamreader...");
                    StreamReader sr = new StreamReader(client.GetStream());
                    log("Opened streamreader");
                    log("Opening streamwriter...");
                    StreamWriter sw = new StreamWriter(client.GetStream());
                    log("Opened streamwriter");

                    log("Reading streamreader...");
                    requestString = sr.ReadLine();
                    log("Read streamreader");

                    log("Writing status...");
                    sw.WriteLine("HTTP/1.0 200 OK\n"); //Send ok response to requester
                    log("Wrote status");

                    log("Parsing request...");
                    ServerRequest request = ServerRequest.Parse(requestString);
                    log("Parsed request");
                    if (HandleRequest != null && request != null)
                    {
                        log("Invoking HandleRequest...");
                        sw.WriteLine(HandleRequest.Invoke(request));
                        log("Invoked HandleRequest");
                    }

                    log("Flushing streamwriter...");
                    sw.Flush();
                    log("Flushed streamwriter");

                    log("Closing client connection...");
                    client.Close();
                    log("Closed client connection");
                }
                catch (Exception ex)
                {
                    if (requestString != null)
                    {
                        ex.Data["path"] = requestString;
                    }

                    log("Invoking ExceptionHandling...");
                    ExceptionHandling?.Invoke(ex);
                    log("Invoked ExceptionHandling");
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
