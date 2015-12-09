﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Linq;

namespace m.Http
{
    public sealed class HttpWebSocketRequest : IHttpRequest
    {
        public IReadOnlyDictionary<string, string> Headers { get; private set; }

        public string WebSocketVersion { get; private set; }
        public string WebSocketKey { get; private set; }
        public string WebSocketExtensions { get; private set; }

        public Uri Url { get; private set; }
        public string Path { get; private set; }
        public string Query { get; private set; }

        public HttpWebSocketRequest(IReadOnlyDictionary<string, string> headers,
                                    string webSocketVersion,
                                    string webSocketKey,
                                    string webSocketExtensions,
                                    Uri url)
        {
            Headers = headers;
            WebSocketVersion = webSocketVersion;
            WebSocketKey = webSocketKey;
            WebSocketExtensions = webSocketExtensions;
            Url = url;
        }
    }

    public sealed class HttpRequest : IHttpRequest
    {
        public string Host { get; private set; }
        public Method Method { get; private set; }
        public string ContentType { get; private set; }
        public IReadOnlyDictionary<string, string> Headers  { get; private set; }
        public Uri Url { get; private set; }

        public string Path { get { return Url.AbsolutePath; } }
        public string Query { get { return Url.Query; } }

        public bool IsKeepAlive { get; private set; }

        public Stream Body { get; private set; }

        public HttpRequest(Method method,
                           string contentType,
                           IReadOnlyDictionary<string, string> headers,
                           Uri url,
                           bool isKeepAlive,
                           Stream body)
        {
            Method = method;
            ContentType = contentType;
            Url = url;
            IsKeepAlive = isKeepAlive;
            Headers = headers;
            Body = body;

            string host;
            if (headers.TryGetValue("Host", out host)) { Host = host; }
        }

        public static implicit operator HttpRequest(HttpListenerRequest req)
        {
            return new HttpRequest(req.GetMethod(),
                                   req.ContentType,
                                   req.Headers.AllKeys.ToDictionary(k => k, k => req.Headers[k], StringComparer.OrdinalIgnoreCase),
                                   req.Url,
                                   req.KeepAlive,
                                   req.InputStream);
        }
    }
}
