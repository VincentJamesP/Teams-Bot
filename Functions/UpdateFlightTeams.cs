// using System;
// using System.Diagnostics;
// using System.Linq;
// using System.Threading.Tasks;

// using Microsoft.Azure.Functions.Worker;
// using Microsoft.Extensions.Logging;


// namespace KTI.PAL.Teams.Functions
// {
//     public class UpdateFlightTeams
//     {
//         private readonly ILogger _logger;
//         private Core.Domains.PAL.IPALTeam _pal;
//         private Core.Domains.Dataverse.IDataverseFlight _dataverse;

//         public UpdateFlightTeams(ILogger<UpdateFlightTeams> logger, Core.Domains.PAL.IPALTeam pal, Core.Domains.Dataverse.IDataverseFlight dataverse)
//         {
//             _logger = logger;
//             _pal = pal;
//             _dataverse = dataverse;
//         }

//         [Function("UpdateFlightTeams")]
//         public async Task Run([TimerTrigger("0 */15 * * * *")] MyInfo myTimer, FunctionContext context)
//         {
//             _logger.LogInformation($"Executing Flight database update at: {DateTime.UtcNow}");
//             Stopwatch time = Stopwatch.StartNew();

//             var flights = _dataverse.GetWithin(TimeSpan.FromHours(24));
//             int newTeams = flights.Select(flight => string.IsNullOrWhiteSpace(flight.TeamId)).Count();
//             int total = flights.Count();

//             await Task.WhenAll(flights.Select(async (flight) =>
//             {
//                 if (string.IsNullOrWhiteSpace(flight.TeamId))
//                 {
//                     await _pal.Create(flight);
//                 }

//                 await _pal.Update(flight.TeamId, flight.OperatingCrew.Concat(flight.NonOperatingCrew).Select(c => c.empCode));
//             }));

//             time.Stop();
//             _logger.LogInformation($"Updated {total} teams ({newTeams} new) in {time.Elapsed.TotalMinutes}min. Next schedule at: {myTimer.ScheduleStatus.Next}");
//         }
//     }
// }
