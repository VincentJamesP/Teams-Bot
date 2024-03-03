using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using KTI.PAL.Teams.Core.Services;


namespace KTI.PAL.Teams.Core.Domains.Merlot
{
    public interface IMerlotFlight
    {
        /// <summary>
        /// Get flight information from Merlot.
        /// </summary>
        /// <param name="id">The ID of the flight to get.</param>
        /// <returns>An awaitable Task containing flight information.</returns>
        Task<Models.Merlot.Flight> Get(int id);
        /// <summary>
        /// Get multiple flight information from  Merlot.
        /// </summary>
        /// <param name="ids">The IDs of the flights to get.</param>
        /// <returns>An awaitable Task containing a list of flight information.</returns>
        Task<List<Core.Models.Merlot.Flight>> GetManyById(IEnumerable<int> ids);
        /// <summary>
        /// Get flight information from Merlot based on a date range.
        /// </summary>
        /// <param name="from">The starting date to get flights from.</param>
        /// <param name="to">The ending date to get flights from.</param>
        /// <param name="getCrew">Whether or not to include crew information.</param>
        /// <returns>An awaitable Task containing a list of flight information.</returns>
        Task<List<Models.Merlot.Flight>> GetMany(DateTime? from, DateTime? to, bool getCrew = false);
        /// <summary>
        /// Get flight information from Merlot based on a date range, but return the Tasks so they can be awaited individually as soon as they finish.
        /// </summary>
        /// <param name="from">The starting date to get flights from.</param>
        /// <param name="to">The ending date to get flights from.</param>
        /// <param name="getCrew">Whether or not to include crew information.</param>
        /// <returns>A list of awaitable Tasks containing the HttpResponseMessage from Merlot.</returns>
        List<Task<HttpResponseMessage>> GetManyAsync(DateTime? from, DateTime? to, bool getCrew = false);
    }

    public class Flights : IMerlotFlight
    {
        private readonly ILogger<Flights> _logger;
        private readonly IMerlotService _service;

        public Flights() { }

        public Flights(ILogger<Flights> logger, IMerlotService service)
        {
            _logger = logger;
            _service = service;
        }

        public async Task<Core.Models.Merlot.Flight> Get(int id)
        {
            var query = new Dictionary<string, string>()
            {
                {"flightIds", id.ToString()}
            };

            var response = await _service.AuthenticatedApiCall("/flight/api/Flight/GetFilteredFlightInformationById", "GET", query: query);

            var flights = JsonConvert.DeserializeObject<List<Core.Models.Merlot.Flight>>(await response.Content.ReadAsStringAsync());
            if (flights.Count > 0)
                return flights.First();

            return null;
        }

        public async Task<List<Core.Models.Merlot.Flight>> GetManyById(IEnumerable<int> ids)
        {
            var query = new Dictionary<string, string>()
            {
                {"flightIds", string.Join(',', ids)}
            };

            var response = await _service.AuthenticatedApiCall("/flight/api/Flight/GetFilteredFlightInformationById", "GET", query: query);

            return JsonConvert.DeserializeObject<List<Core.Models.Merlot.Flight>>(await response.Content.ReadAsStringAsync());
        }

        public async Task<List<Core.Models.Merlot.Flight>> GetMany(DateTime? from, DateTime? to, bool getCrew = false)
        {
            // use defaults of start and end of this month if missing parameters
            if (!from.HasValue || !to.HasValue)
            {
                from = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
                to = from.Value.AddMonths(1).AddTicks(-1);
            }

            // make sure start date is earlier than end date
            if (from.Value > to.Value)
            {
                DateTime temp = from.Value;
                from = to;
                to = temp;
            }

            var batchCount = 6;

            var totalDays = (int)(to.Value - from.Value).TotalDays;
            DateTime start = from.Value;
            Stopwatch requestTime = Stopwatch.StartNew();

            _logger.LogInformation($"Fetching flights from {from.Value.ToString("yyyy-MM-dd")} to {to.Value.ToString("yyyy-MM-dd")}");

            List<Core.Models.Merlot.Flight> flights = new();
            List<Task<HttpResponseMessage>> responses = new();

            for (int daysLeft = totalDays; daysLeft >= 0; daysLeft -= batchCount + 1)
            {
                var query = new Dictionary<string, string>()
                {
                    {"searchCriteria.departureDate", start.ToString("yyyy-MM-dd")},
                    {"searchCriteria.departureEndDate", start.AddDays(daysLeft > batchCount ? batchCount : daysLeft).ToString("yyyy-MM-dd")},
                    {"searchCriteria.getCrew", getCrew.ToString().ToLower()},
                    {"searchCriteria.timeModeRequest", "0"},
                    {"searchCriteria.timeModeResponse", "0"}
                };

                start = start.AddDays((daysLeft > batchCount ? batchCount : daysLeft) + 1);

                responses.Add(_service.AuthenticatedApiCall("/flight/api/Flight/GetFilteredFlightInformation", "GET", query: query));
            }

            foreach (Task<HttpResponseMessage> response in responses)
            {
                flights.AddRange(JsonConvert.DeserializeObject<List<Core.Models.Merlot.Flight>>(await (await response).Content.ReadAsStringAsync()));
            }

            requestTime.Stop();

            _logger.LogInformation($"Completed after {requestTime.Elapsed.TotalMinutes}min");

            return flights;
        }

        public List<Task<HttpResponseMessage>> GetManyAsync(DateTime? from, DateTime? to, bool getCrew = false)
        {
            // use defaults of start and end of this month if missing parameters
            if (!from.HasValue || !to.HasValue)
            {
                from = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
                to = from.Value.AddMonths(1).AddTicks(-1);
            }
            // make sure start date is earlier than end date
            if (from.Value > to.Value)
            {
                DateTime temp = from.Value;
                from = to;
                to = temp;
            }

            var batchCount = 6;
            var totalDays = (int)(to.Value - from.Value).TotalDays;
            DateTime start = from.Value;

            _logger.LogInformation($"Fetching flights from {from.Value.ToString("yyyy-MM-dd")} to {to.Value.ToString("yyyy-MM-dd")}");

            List<Task<HttpResponseMessage>> responses = new();

            for (int daysLeft = totalDays; daysLeft >= 0; daysLeft -= batchCount + 1)
            {
                var query = new Dictionary<string, string>()
                {
                    {"searchCriteria.departureDate", start.ToString("yyyy-MM-dd")},
                    {"searchCriteria.departureEndDate", start.AddDays(daysLeft > batchCount ? batchCount : daysLeft).ToString("yyyy-MM-dd")},
                    {"searchCriteria.getCrew", getCrew.ToString().ToLower()},
                    {"searchCriteria.timeModeRequest", "0"},
                    {"searchCriteria.timeModeResponse", "0"}
                };

                start = start.AddDays((daysLeft > batchCount ? batchCount : daysLeft) + 1);

                responses.Add(_service.AuthenticatedApiCall("/flight/api/Flight/GetFilteredFlightInformation", "GET", query: query));
            }

            return responses;
        }
    }
}
