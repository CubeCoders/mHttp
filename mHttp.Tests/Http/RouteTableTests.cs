﻿using System;
using System.Collections.Generic;

using NUnit.Framework;

using m.Http;
using m.Http.Routing;

namespace m.Http
{
    [TestFixture]
    public class RouteTableTests : BaseTest
    {
        [Test]
        public void TestRouteTable()
        {
            Action noOp = () => {};

            Endpoint ep1 = Route.Get("/files/*").WithAction(noOp);
            Endpoint ep2 = Route.Get("/accounts/{id}").WithAction(noOp);
            Endpoint ep3 = Route.Get("/").WithAction(noOp);
            Endpoint ep4 = Route.Post("/accounts/").WithAction(noOp);
            Endpoint ep5 = Route.Get("/accounts/{id}/data").WithAction(noOp);
            Endpoint ep6 = Route.Get("/*").WithAction(noOp);

            var routeTable = new RouteTable(ep1, ep2, ep3, ep4, ep5, ep6);

            int matchedIndex;

            matchedIndex = routeTable.TryMatchEndpoint(Method.GET, new Uri("http://localhost/"), out IReadOnlyDictionary<string, string> pathVariables);
            Assert.AreSame(ep3, routeTable[matchedIndex]);

            matchedIndex = routeTable.TryMatchEndpoint(Method.GET, new Uri("http://localhost/accounts/111/data?keys=name"), out pathVariables);
            Assert.AreSame(ep5, routeTable[matchedIndex]);
            Assert.AreEqual("111", pathVariables["id"]);
            Assert.AreEqual(-1, routeTable.TryMatchEndpoint(Method.POST, new Uri("http://localhost/accounts/111/data"), out pathVariables));
            Assert.IsNull(pathVariables);

            matchedIndex = routeTable.TryMatchEndpoint(Method.GET, new Uri("http://localhost/files/images/test.png"), out pathVariables);
            Assert.AreSame(routeTable[matchedIndex], ep1);

            matchedIndex = routeTable.TryMatchEndpoint(Method.GET, new Uri("http://localhost/accounts/222"), out pathVariables);
            Assert.AreSame(ep2, routeTable[matchedIndex]);
            Assert.AreEqual("222", pathVariables["id"]);

            matchedIndex = routeTable.TryMatchEndpoint(Method.GET, new Uri("http://localhost/whatever"), out pathVariables);
            Assert.AreSame(ep6, routeTable[matchedIndex]);

            Assert.AreEqual(-1, routeTable.TryMatchEndpoint(Method.POST, new Uri("http://localhost/"), out pathVariables));
        }
    }
}
