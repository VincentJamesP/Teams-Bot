using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;


namespace KTI.PAL.Teams.Functions
{
    public class SyncCache
    {
        private readonly ILogger _logger;
        private Core.Domains.PAL.IPAL _pal;

        private Core.Domains.Merlot.IMerlotFlight _flights;
        private Core.Domains.Merlot.IMerlotPairing _pairings;
        private Core.Domains.Dataverse.IDataverseDuty _dutyRecords;
        private Core.Domains.Dataverse.IDataverseFlight _flightRecords;
        private Core.Domains.Graph.IGraphCalendarDomain _calendar;

        public SyncCache(ILogger<SyncCache> logger, Core.Domains.PAL.IPAL pal, Core.Domains.Merlot.IMerlotFlight flights, Core.Domains.Merlot.IMerlotPairing pairings, Core.Domains.Dataverse.IDataverseDuty dutyRecords, Core.Domains.Dataverse.IDataverseFlight flightRecords, Core.Domains.Graph.IGraphCalendarDomain calendar)
        {
            _logger = logger;
            _pal = pal;
            _flights = flights;
            _pairings = pairings;
            _dutyRecords = dutyRecords;
            _flightRecords = flightRecords;
            _calendar = calendar;
        }

        [Function("SyncFlights")]
        public async Task SyncFlights([TimerTrigger("0 0/30 * * * *")] MyInfo myTimer, FunctionContext context)
        {
            _logger.LogInformation($"Syncing flight records at {DateTime.UtcNow}");
            Stopwatch time = Stopwatch.StartNew();

            DateTime now = DateTime.UtcNow;
            DateTime from = new DateTime(now.Year, now.Month, now.Day);
            DateTime to = from.AddMonths(1);
            to = new DateTime(to.Year, to.Month, 1).AddTicks(-1);
            // fetch next month if within a week before next month
            if ((to - from).TotalDays < 7)
            {
                to = to.AddMonths(1);
            }

            List<Task<HttpResponseMessage>> flightTasks = _flights.GetManyAsync(from, to, true);
            List<Core.Models.Merlot.Flight> flights = new();

            while (flightTasks.Any())
            {
                _logger.LogInformation($"{flightTasks.Count} remaining batch(es) to receive.");
                var task = await Task.WhenAny(flightTasks);
                flightTasks.Remove(task);
                flights.AddRange(JsonConvert.DeserializeObject<List<Core.Models.Merlot.Flight>>(await (await task).Content.ReadAsStringAsync()));
            }

            Cache.SetFlights(flights.Where(f => !f.cancelled).ToList());

            var batches = flights.Chunk(1000).ToList();
            await _pal.UpdateCrewRecords(flights.SelectMany(flight => flight.crew).Distinct());
            await Task.WhenAll(batches.Select(batch => _pal.UpdateFlights(batch)));

            time.Stop();
            _logger.LogInformation($"Processed {flights.Count()} flights in {time.Elapsed.TotalMinutes}min. Next sync is at {myTimer.ScheduleStatus.Next}.");
        }

        [Function("SyncPairings")]
        public async Task SyncPairings([TimerTrigger("0 5/30 * * * *")] MyInfo myTimer, FunctionContext context)
        {
            _logger.LogInformation($"Syncing pairing records at {DateTime.UtcNow}");
            Stopwatch time = Stopwatch.StartNew();

            DateTime now = DateTime.UtcNow;
            DateTime from = new DateTime(now.Year, now.Month, now.Day);
            DateTime to = from.AddMonths(1);
            to = new DateTime(to.Year, to.Month, 1).AddTicks(-1);
            // fetch next month if within a week before next month
            if ((to - from).TotalDays < 7)
            {
                to = to.AddMonths(1);
            }

            List<Task<HttpResponseMessage>> pairingTasks = _pairings.GetManyAsync(from, to);
            List<Core.Models.Merlot.Pairing> pairings = new();

            while (pairingTasks.Any())
            {
                _logger.LogInformation($"{pairingTasks.Count} remaining batch(es) to receive.");
                var task = await Task.WhenAny(pairingTasks);
                pairingTasks.Remove(task);
                List<Core.Models.Merlot.Pairing> p = new();
                pairings.AddRange(JsonConvert.DeserializeObject<Core.Models.Merlot.PairingResponse>(await (await task).Content.ReadAsStringAsync()).pairings);
                pairings = pairings.Where(p => p.activeFlagId.Equals(2)).ToList();
                pairings.AddRange(p);
            }

            Cache.SetPairings(pairings);

            await Task.WhenAll(
                _pal.UpdatePairings(pairings),
                _pal.UpdateCrewRecords(pairings.SelectMany(p => p.pairingEmployees).Distinct())
            );

            time.Stop();
            _logger.LogInformation($"Processed {pairings.Count()} pairings in {time.Elapsed.TotalMinutes}min. Next sync is at {myTimer.ScheduleStatus.Next}.");
        }

        [Function("SyncTeams")]
        public async Task SyncTeams([TimerTrigger("0 10/30 * * * *")] MyInfo myTimer, FunctionContext context)
        {
            _logger.LogInformation($"Syncing teams at {DateTime.UtcNow}");
            Stopwatch time = Stopwatch.StartNew();

            var flights = _flightRecords.GetWithin(TimeSpan.FromHours(24));

            List<Core.Models.Dataverse.Flight> toArchive = flights.Where(flight => flight.FlightNumber.Contains("cancelled")).ToList();
            List<Core.Models.Dataverse.Flight> toUpdate = flights.Where(flight => !flight.FlightNumber.Contains("cancelled")).ToList();

            await Task.WhenAll(
                _pal.ArchiveTeams(toArchive),
                _pal.UpdateFlightTeams(toUpdate)
            );

            time.Stop();
            _logger.LogInformation($"Processed {flights.Count()} teams ({flights.Select(flight => string.IsNullOrWhiteSpace(flight.TeamId)).Count()} new) in {time.Elapsed.TotalSeconds}sec. Next sync is at {myTimer.ScheduleStatus.Next}.");
        }
    }
}
