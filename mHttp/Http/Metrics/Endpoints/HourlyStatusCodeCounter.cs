using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using m.Utils;

namespace m.Http.Metrics.Endpoints
{
    //TODO: abstract to 'HourlyCounter<HttpStatusCode>'
    class HourlyStatusCodeCounter : IEnumerable<HourlyStatusCodeCounter.Entry>
    {
        public struct Entry
        {
            public readonly int TimeHours;
            public readonly IReadOnlyDictionary<HttpStatusCode, int> StatusCodes;

            public Entry(int timeHours, ConcurrentDictionary<HttpStatusCode, int> statusCodes)
            {
                TimeHours = timeHours;
                StatusCodes = new Dictionary<HttpStatusCode, int>(statusCodes);
            }
        }

        readonly int hoursToKeep;
        readonly ConcurrentDictionary<int, ConcurrentDictionary<HttpStatusCode, int>> statusCodesByHour;
        readonly List<int> hoursToDelete;
        
        public HourlyStatusCodeCounter(int hoursToKeep)
        {
            this.hoursToKeep = hoursToKeep;
            statusCodesByHour = new ConcurrentDictionary<int, ConcurrentDictionary<HttpStatusCode, int>>();
            hoursToDelete = new List<int>(1);
        }

        public IEnumerator<Entry> GetEnumerator()
        {
            return statusCodesByHour.Select(kvp => new Entry(kvp.Key, kvp.Value)).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Update(IEnumerable<RequestLogs.Log> logs)
        {
            foreach (var log in logs)
            {
                var completedOnTimeHours = log.CompletedOnTimeHours;
                var statusCode = log.Response.StatusCode;

                if (!statusCodesByHour.TryGetValue(completedOnTimeHours, out ConcurrentDictionary<HttpStatusCode, int> statusCodes))
                {
                    statusCodes = new ConcurrentDictionary<HttpStatusCode, int>();
                    statusCodesByHour[completedOnTimeHours] = statusCodes;
                }

                statusCodes[statusCode] = statusCodes.TryGetValue(statusCode, out int count) ? count + 1 : 1;
            }

            // Trim 
            var currentTimeHours = Time.CurrentTimeHours;
            var cutOffTimeHours = currentTimeHours - hoursToKeep;
            hoursToDelete.Clear();

            foreach (var kvp in statusCodesByHour)
            {
                if (kvp.Key < cutOffTimeHours)
                {
                    hoursToDelete.Add(kvp.Key);
                }
            }

            if (hoursToDelete.Count > 0)
            {
                foreach (var hourToDelete in hoursToDelete)
                {
                    statusCodesByHour.TryRemove(hourToDelete, out ConcurrentDictionary<HttpStatusCode, int> ignored);
                }
            }
        }
    }
}
