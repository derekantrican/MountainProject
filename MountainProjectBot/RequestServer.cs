using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using NetCoreServer;

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

    public class Server : HttpServer
    {
        public class HandleRequestSession : HttpSession
        {
            public Func<ServerRequest, string> HandleRequest { get; set; }

            public HandleRequestSession(HttpServer server, Func<ServerRequest, string> handleRequest) : base(server)
            {
                HandleRequest = handleRequest;
            }

            protected override void OnReceivedRequest(HttpRequest request)
            {
                ServerRequest serverRequest = new ServerRequest
                {
                    RequestMethod = (HttpMethod)Enum.Parse(typeof(HttpMethod), request.Method, true),
                    Path = request.Url.Trim('/'),
                };

                Console.WriteLine($"received request... {serverRequest.RequestMethod} {serverRequest.Path} ");
                if (HandleRequest != null && serverRequest != null)
                {
                    string response = HandleRequest(new ServerRequest
                    {
                        RequestMethod = (HttpMethod)Enum.Parse(typeof(HttpMethod), request.Method, true),
                        Path = request.Url.Trim('/'),
                    });
                    Console.WriteLine($"sending response to {serverRequest.Path} ...");
                    SendResponseAsync(Response.MakeGetResponse(response, "text/html; charset=UTF-8"));
                }
            }
        }

        public int Port { get; private set; }

        public Server(int port) : base(IPAddress.Any, port)
        {
            Port = port;
        }

        protected override HttpSession CreateSession()
        {
            return new HandleRequestSession(this, HandleRequest);
        }

        public StringWriter Activity { get; set; } = new StringWriter();

        public Func<ServerRequest, string> HandleRequest { get; set; }
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

            return Path.Split('?')[1].Split('&').Select(p => p.Split('=')).ToDictionary(kvp => kvp[0].ToLower(), kvp => kvp.Length > 1 ? WebUtility.UrlDecode(kvp[1]) : "");
        }
    }
}
