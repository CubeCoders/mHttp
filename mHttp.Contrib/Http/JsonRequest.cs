using System;
using System.Collections.Generic;
using System.Net;

using m.Http.Backend;

namespace m.Http
{
    public sealed class JsonRequest<TReq>
    {
        readonly IHttpRequest req;

        public TReq Req { get; private set; }

        public Method Method => req.Method;
        public string ContentType => req.ContentType;
        public IReadOnlyDictionary<string, string> Headers => req.Headers;
        public Uri Url => req.Url;

        public string Path => Url.AbsolutePath;
        public string Query => Url.Query;

        public IReadOnlyDictionary<string, string> PathVariables { get; private set; }

        internal JsonRequest(IHttpRequest req, TReq tReq, IReadOnlyDictionary<string, string> pathVariables)
        {
            this.req = req;
            Req = tReq;
            PathVariables = pathVariables;
        }

        internal static JsonRequest<TReq> From(IHttpRequest req)
        {
            TReq reqObj;
            try
            {
                reqObj = req.Body.FromJson<TReq>();
            }
            catch (Exception e)
            {
                throw new RequestException($"Error deserializing inputstream to <{typeof(TReq).Name}> - {e.Message}", e, HttpStatusCode.BadRequest);
            }

            return new JsonRequest<TReq>(req, reqObj, req.PathVariables);
        }
    }
}
