using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using m.Http.Handlers;
using m.Http.Routing;

namespace m.Http
{
    using HandlerSignature1 = Func<IHttpRequest, Task<HttpResponse>>;
    using HandlerSignature2 = Func<IHttpRequest, HttpResponse>;
    using HandlerSignature3 = Func<IWebSocketUpgradeRequest, WebSocketUpgradeResponse>;

    public static class Endpoints
    {
        public static Endpoint[] From(Type classType)
        {
            var staticEndpointMethods = GetEndpointMethods(classType, true);

            return From(staticEndpointMethods, null);
        }

        public static Endpoint[] From(object classInstance)
        {
            var endpointMethods = GetEndpointMethods(classInstance.GetType(), false);

            return From(endpointMethods, classInstance);
        }

        static Endpoint[] From(IEnumerable<Tuple<MethodInfo, EndpointAttribute>> endpointMethods, object targetClassInstance)
        {
            var endpoints = new List<Endpoint>();

            foreach (var pair in endpointMethods)
            {
                var method = pair.Item1;
                var endpointAttrb = pair.Item2;

                if (IsValidEndpointHandler(method, targetClassInstance, out var handler))
                {
                    if (endpointAttrb.RequestsPerSecond > 0)
                    {
                        endpoints.Add(new RateLimitedEndpoint(endpointAttrb.Method,
                                                              new Routing.Route(endpointAttrb.PathTemplate),
                                                              handler,
                                                              endpointAttrb.RequestsPerSecond,
                                                              endpointAttrb.BurstRequestsPerSecond));
                    }
                    else
                    {
                        endpoints.Add(new Endpoint(endpointAttrb.Method,
                                                   new Routing.Route(endpointAttrb.PathTemplate),
                                                   handler));
                    }
                }
                else
                {
                    throw new Exception($"[{method.DeclaringType.Name}.{method.Name}] does not have a recognized endpoint method signature");
                }
            }

            return endpoints.ToArray();
        }

        static bool IsValidEndpointHandler(MethodInfo method, object targetClassInstance, out Func<IHttpRequest, Task<HttpResponse>> asHandler)
        {
            try // `Func<Request, Task<HttpResponse>>` ?
            {
                if (method.IsStatic)
                {
                    asHandler = (HandlerSignature1)method.CreateDelegate(typeof(HandlerSignature1));
                }
                else
                {
                    asHandler = (HandlerSignature1)method.CreateDelegate(typeof(HandlerSignature1), targetClassInstance);
                }
                    
                return true;
            }
            catch
            {
                asHandler = null;
            }

            try // `Func<Request, HttpResponse>` ?
            {
                if (method.IsStatic)
                {
                    var handler = (HandlerSignature2)method.CreateDelegate(typeof(HandlerSignature2));
                    asHandler = Handler.From(handler);
                }
                else
                {
                    var handler = (HandlerSignature2)method.CreateDelegate(typeof(HandlerSignature2), targetClassInstance);
                    asHandler = Handler.From(handler);
                }
                return true;
            }
            catch
            {
                asHandler = null;
            }

            try // `Func<IWebSocketUpgradeRequest, WebSocketUpgradeResponse>` ?
            {
                if (method.IsStatic)
                {
                    var handler = (HandlerSignature3)method.CreateDelegate(typeof(HandlerSignature3));
                    asHandler = Handler.From(handler);
                }
                else
                {
                    var handler = (HandlerSignature3)method.CreateDelegate(typeof(HandlerSignature3), targetClassInstance);
                    asHandler = Handler.From(handler);
                }
                return true;
            }
            catch
            {
                asHandler = null;
            }

            return false;
        }

        static IEnumerable<Tuple<MethodInfo, EndpointAttribute>> GetEndpointMethods(Type classType, bool staticOnly)
        {
            var methods = classType.GetMethods();

            var endpointMethods = methods.Select(m => new {
                                              Method = m,
                                              EndpointAttribute = m.GetCustomAttributes(typeof(EndpointAttribute), false)
                                                                   .SingleOrDefault() as EndpointAttribute
                                          })
                                         .Where(m => m.EndpointAttribute != null)
                                         .Select(m => Tuple.Create(m.Method, m.EndpointAttribute));

            if (staticOnly)
            {
                endpointMethods = endpointMethods.Where(m => m.Item1.IsStatic);
            }

            return endpointMethods;
        }
    }
}
