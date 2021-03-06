﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

using NUnit.Framework;

using m.Http.Backend.Tcp;
using m.Utils;

namespace m.Http.Backend.Tcp
{
    [TestFixture]
    public class RequestParserTests : BaseTest
    {
        static readonly IPEndPoint RemoteEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12345);

        [Test]
        [Ignore("TODO: guard rogue client")]
        public void TestMixedNewLines()
        {
            var request = new MemoryStream(8192);
            var buffer = request.GetBuffer();
            var start = 0;
            request.WriteAscii("User-Agent: Mozilla/5.0 (Windows NT 6.1; WOW64; rv:42.0) Gecko/20100101 Firefox/42.0\n");
            request.WriteAscii("Accept: text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8\n");
            request.WriteAscii("Accept-language: en-US;q=0.7,en;q=0.3\n");
            request.WriteAscii("Connection: close\r\n");

            Assert.IsTrue(RequestParser.TryReadLine(buffer, ref start, (int)request.Length, out int lineStart, out int lineEnd));
            var line = Encoding.ASCII.GetString(buffer, lineStart, lineEnd);
            Assert.AreEqual("User-Agent: Mozilla/5.0 (Windows NT 6.1; WOW64; rv:42.0) Gecko/20100101 Firefox/42.0", line);
        }

        [Test]
        public void TestTryReadLine()
        {
            var request = new MemoryStream(8192);
            var buffer = request.GetBuffer();
            var start = 0;
            Assert.IsFalse(RequestParser.TryReadLine(buffer, ref start, (int)request.Length, out int lineStart, out int lineEnd));

            request.WriteAscii("\r\n");
            Assert.IsTrue(RequestParser.TryReadLine(buffer, ref start, (int)request.Length, out lineStart, out lineEnd));

            request.WriteAscii("GET /");
            Assert.IsFalse(RequestParser.TryReadLine(buffer, ref start, (int)request.Length, out lineStart, out lineEnd));

            request.WriteAscii(" HTTP/1.1\r\nUser-Agent: ");
            Assert.IsTrue(RequestParser.TryReadLine(buffer, ref start, (int)request.Length, out lineStart, out lineEnd));

            Assert.AreEqual(Encoding.ASCII.GetString(buffer, lineStart, lineEnd - lineStart), "GET / HTTP/1.1");
        }

        [Test]
        public void TestParseRequestLine1()
        {
            var request = new MemoryStream(8192);
            var buffer = request.GetBuffer();
            var start = 0;
            request.WriteAscii("POST /accounts?flag=x HTTP/1.1\r\n");
            Assert.IsTrue(RequestParser.TryReadLine(buffer, ref start, (int)request.Length, out int lineStart, out int lineEnd));
            RequestParser.ParseRequestLine(buffer, lineStart, lineEnd, out Method method, out string path, out string query, out string version);
            Assert.AreEqual(Method.POST, method);
            Assert.AreEqual("/accounts", path);
            Assert.AreEqual("?flag=x", query);
            Assert.AreEqual("HTTP/1.1", version);
        }

        [Test]
        public void TestParseRequestLine2()
        {
            var request = new MemoryStream(8192);
            var buffer = request.GetBuffer();
            var start = 0;
            request.WriteAscii("POST /accounts? HTTP/1.1\r\n");
            Assert.IsTrue(RequestParser.TryReadLine(buffer, ref start, (int)request.Length, out int lineStart, out int lineEnd));
            RequestParser.ParseRequestLine(buffer, lineStart, lineEnd, out Method method, out string path, out string query, out string version);
            Assert.AreEqual(Method.POST, method);
            Assert.AreEqual("/accounts", path);
            Assert.AreEqual("?", query);
            Assert.AreEqual("HTTP/1.1", version);
        }

        [Test]
        public void TestParseHeader()
        {
            var request = new MemoryStream(8192);
            var buffer = request.GetBuffer();
            var start = 0;
            request.WriteAscii("Host : localhost:8080\r\n");
            Assert.IsTrue(RequestParser.TryReadLine(buffer, ref start, (int)request.Length, out int lineStart, out int lineEnd));
            RequestParser.ParseHeader(buffer, lineStart, lineEnd, out string name, out string value);
            Assert.AreEqual("Host", name);
            Assert.AreEqual("localhost:8080", value);
        }

        [Test]
        public void TestParseHeaders()
        {
            var request = new MemoryStream(8192);
            var buffer = request.GetBuffer();
            var start = 0;

            request.WriteAscii("User-Agent: curl/7.35.0\r\n");
            request.WriteAscii("Host: localhost:8080\r\n");
            request.WriteAscii("Accept: */*\r\n");
            request.WriteAscii("\r\n");

            var headers = new Dictionary<string, string>();
            Assert.IsTrue(RequestParser.TryParseHeaders(buffer, ref start, (int)request.Length, headers.Add));
        }

        [Test]
        public void TestTryParseHttpGetRequest()
        {
            var request = new MemoryStream(8192);
            var buffer = request.GetBuffer();
            var start = 0;

            request.WriteAscii("GET /index.jsp HTTP/1.1\r\n");
            request.WriteAscii("User-Agent: curl/7.35.0\r\n");
            request.WriteAscii("Host: localhost:8080\r\n");
            request.WriteAscii("Accept: */*\r\n");

            var state = new HttpRequest(RemoteEndPoint, false);
            Assert.IsFalse(RequestParser.TryParseHttpRequest(buffer, ref start, (int)request.Length, state, out HttpRequest httpRequest));

            request.WriteAscii("\r\n");
            Assert.IsTrue(RequestParser.TryParseHttpRequest(buffer, ref start, (int)request.Length, state, out httpRequest));
            Assert.AreEqual(Method.GET, httpRequest.Method);
            Assert.AreEqual("http://localhost:8080/index.jsp", httpRequest.Url.AbsoluteUri);
            Assert.AreEqual("curl/7.35.0", httpRequest.Headers["User-Agent"]);
        }

        [Test]
        public void TestTryParseHttpPostRequest()
        {
            var request = new MemoryStream(8192);
            var buffer = request.GetBuffer();
            var start = 0;

            request.WriteAscii("POST /accounts/create HTTP/1.1\r\n");
            request.WriteAscii("Host: localhost:8080\r\n");
            request.WriteAscii("Content-Length: 23\r\n");
            request.WriteAscii("\r\n");
            request.WriteAscii(@"{ ""username"" : ""test"" }");

            var state = new HttpRequest(RemoteEndPoint, false);
            Assert.IsTrue(RequestParser.TryParseHttpRequest(buffer, ref start, (int)request.Length, state, out HttpRequest httpRequest));
            Assert.AreEqual(Method.POST, httpRequest.Method);
            Assert.AreEqual("http://localhost:8080/accounts/create", httpRequest.Url.AbsoluteUri);

            var requestBody = httpRequest.Body.ReadToEnd();
            Assert.AreEqual(@"{ ""username"" : ""test"" }", requestBody);
        }
    }
}
