using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;


namespace KTI.PAL.Teams.Functions
{
    public class ArchiveAndCleanup
    {
        private readonly ILogger _logger;
        private Core.Domains.Dataverse.IDataverseFlight _flights;
        private Core.Domains.Dataverse.IDataverseDuty _duties;
        private Core.Domains.PAL.IPAL _pal;

        public ArchiveAndCleanup(ILogger<ArchiveAndCleanup> logger, Core.Domains.Dataverse.IDataverseFlight flights, Core.Domains.Dataverse.IDataverseDuty duties, Core.Domains.PAL.IPAL pal)
        {
            _logger = logger;
            _pal = pal;
            _flights = flights;
            _duties = duties;
        }

        [Function("ArchiveAndCleanup")]
        public async Task Run([TimerTrigger("0 0 */24 * * *")] MyInfo myTimer)
        {
            _logger.LogInformation($"Executing teams archival and database cleanup at: {DateTime.UtcNow}");
            Stopwatch time = Stopwatch.StartNew();

            var flights = _flights.GetFinished();
            var duties = _duties.GetFinished();

            if (flights.Count() > 0)
                await _pal.ArchiveTeams(flights);
            if (duties.Count() > 0)
                _duties.DeleteMultiple(duties.Select(duty => duty.Id.Value));

            // await Task.WhenAll(flights.Select(flight => _palTeam.Archive(flight)));

            time.Stop();
            _logger.LogInformation($"Removed {flights.Count()} old flight records ({flights.Where(flight => !string.IsNullOrWhiteSpace(flight.TeamId)).Count()} teams archived) and {duties.Count()} old flight duties in {time.Elapsed.TotalSeconds:0.00}sec. Next schedule at: {myTimer.ScheduleStatus.Next}");
        }
    }
}
