using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using KTI.PAL.Teams.Core.Models.Merlot;
using KTI.PAL.Teams.Core.Services;
using System.Net.Http;
using System.Linq;
using Newtonsoft.Json;

namespace KTI.PAL.Teams.Core.Domains.Merlot
{
    public interface IMerlotPairing
    {
        /// <summary>
        /// Fetch all pairings within a given date range.
        /// </summary>
        /// <param name="from">The start of the date range to get pairings.</param>
        /// <param name="to">The end of the date range.</param>
        /// <returns>An awaitable Task containing a list of pairing information.</returns>
        Task<List<Core.Models.Merlot.Pairing>> GetAsync(DateTime? from = null, DateTime? to = null, string merlotId = null);
        /// <summary>
        /// Fetch all pairings within a given date range.
        /// <para>
        /// Differs from GetAsync in that this method should return the Tasks of each API response, allowing the calling method to await them individually,
        /// whereas IMerlotPairing.GetAsync will wait for all batched API requests to finish before returning the final aggregated list of pairings.
        /// </para>
        /// </summary>
        /// <param name="from">Start of range to get pairings.</param>
        /// <param name="to">End of range.</param>
        /// <returns>A list of awaitable Tasks containing the HttpResponseMessage from Merlot.</returns>
        List<Task<HttpResponseMessage>> GetManyAsync(DateTime? from = null, DateTime? to = null, string merlotId = null);
    }

    public class Pairings : IMerlotPairing
    {
        private readonly ILogger<Pairing> _logger;
        private readonly IMerlotService _service;

        public Pairings() { }

        public Pairings(ILogger<Pairing> logger, IMerlotService service)
        {
            _logger = logger;
            _service = service;
        }

        private (DateTime from, DateTime to) GetDefaultRange(DateTime? from, DateTime? to)
        {
            if (!from.HasValue || !to.HasValue)
            {
                from = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day);
                to = new DateTime(from.Value.Year, from.Value.Month + 1, 1).AddTicks(-1);
            }
            if (from > to)
            {
                DateTime temp = from.Value;
                from = to;
                to = temp;
            }
            return (from.Value, to.Value);
        }

        public async Task<List<Core.Models.Merlot.Pairing>> GetAsync(DateTime? from = null, DateTime? to = null, string merlotId = null)
        {
            List<Task<HttpResponseMessage>> tasks = GetManyAsync(from, to, merlotId);
            List<Core.Models.Merlot.Pairing> pairings = new();
            while (tasks.Any())
            {
                var task = await Task.WhenAny(tasks);
                tasks.Remove(task);

                var content = (await task).Content;

                pairings.AddRange(JsonConvert.DeserializeObject<Core.Models.Merlot.PairingResponse>(await content.ReadAsStringAsync()).pairings);
            }

            return pairings;
        }

        public List<Task<HttpResponseMessage>> GetManyAsync(DateTime? from = null, DateTime? to = null, string merlotId = null)
        {
            // use defaults of start and end of this month if missing parameters
            (DateTime start, DateTime end) = GetDefaultRange(from, to);

            var batchCount = 6;

            var totalDays = (int)(end - start).TotalDays;

            _logger.LogInformation($"Fetching pairings from {start.ToString("yyyy-MM-dd")} to {end.ToString("yyyy-MM-dd")}");

            List<Task<HttpResponseMessage>> responses = new();

            for (int daysLeft = totalDays; daysLeft >= 0; daysLeft -= batchCount + 1)
            {
                var query = new Dictionary<string, string>()
                {
                    {"request.fromDate", start.ToString("yyyy-MM-dd")},
                    {"request.toDate", start.AddDays(daysLeft > batchCount ? batchCount : daysLeft).ToString("yyyy-MM-dd")}
                };
                if (!string.IsNullOrWhiteSpace(merlotId))
                    query.Add("request.employeeIds", merlotId);

                start = start.AddDays((daysLeft > batchCount ? batchCount : daysLeft) + 1);

                responses.Add(_service.AuthenticatedApiCall("/pairing/api/pairing/Pairing", "GET", query: query));
            }

            return responses;
        }
    }
}
