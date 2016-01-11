﻿using System;

namespace m.Http.Extensions
{
    public static class HttpRequestExtensions
    {
        public static bool TryGetIfLastModifiedSince(this IHttpRequest req, out DateTime utcDate)
        {
            string value;
            if (req.Headers.TryGetValue(HttpHeader.IfModifiedSince, out value))
            {
                utcDate = DateTime.Parse(value).ToUniversalTime();
                return true;
            }
            else
            {
                utcDate = DateTime.UtcNow;
                return false;
            }
        }

        public static bool IsAcceptGZip(this IHttpRequest req)
        {
            string value;
            if (req.Headers.TryGetValue(HttpHeader.AcceptEncoding, out value))
            {
                return value.IndexOf(HttpHeaderValue.GZip, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            else
            {
                return false;
            }
        }
    }
}