using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KTI.PAL.Teams.Core.Models;
using KTI.PAL.Teams.Core.Models.Merlot;
using KTI.PAL.Teams.Core.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace KTI.PAL.Teams.Core.Domains.PAL
{
    public interface IPAL
    {
        /// <summary>
        /// Archive teams.
        /// </summary>
        /// <param name="flights">A list of flight records whose teams should be archived.</param>
        /// <returns>An awaitable Task.</returns>
        Task ArchiveTeams(IEnumerable<Models.Dataverse.Flight> flights);
        /// <summary>
        /// Handle cancelled flights - Archive team, cancel the calendar event, and delete the entry from Dataverse.
        /// </summary>
        /// <param name="cancelledFlights">A list of flights that were cancelled.</param>
        /// <returns>An awaitable Task.</returns>
        Task HandleCancelledFlights(IEnumerable<Models.Merlot.Flight> cancelledFlights);
        /// <summary>
        /// Create or Update the crew records in the Dataverse lookup table.
        /// </summary>
        /// <param name="employees">A list of pairing employee data from Merlot.</param>
        /// <returns>An awaitable Task.</returns>
        Task UpdateCrewRecords(IEnumerable<PairingEmployee> employees);
        /// <summary>
        /// Create or Update the crew records in the Dataverse lookup table.
        /// </summary>
        /// <param name="employees">A list of flight crew data from Merlot.</param>
        /// <returns>An awaitable Task.</returns>
        Task UpdateCrewRecords(IEnumerable<Models.Merlot.FlightCrew> employees);
        /// <summary>
        /// Create or Update the duty records in the Dataverse table.
        /// </summary>
        /// <param name="dutyPairings">A list of FPG pairings from Merlot.</param>
        /// <returns>An awaitable Task.</returns>
        Task UpdateDutyRecords(IEnumerable<Pairing> dutyPairings);
        /// <summary>
        /// Create of Update the flight records in the Dataverse table, and their corresponding calendar events.
        /// </summary>
        /// <param name="activeFlights"></param>
        /// <returns>An awaitable Task.</returns>
        Task UpdateFlightRecords(IEnumerable<Models.Merlot.Flight> activeFlights);
        /// <summary>
        /// Update the Teams for each flight, adding additional crew members if necessary.
        /// </summary>
        /// <param name="flights">A list of flights whose teams need to be updated.</param>
        /// <returns>An awaitable Task.</returns>
        Task UpdateFlightTeams(IEnumerable<Models.Dataverse.Flight> flights);

        /// <summary>
        /// Call UpdateFlightRecords and HandleCancelledFlights
        /// </summary>
        /// <param name="flights">A list of flights to update.</param>
        /// <returns>An awaitable Task.</returns>
        Task UpdateFlights(IEnumerable<Models.Merlot.Flight> flights);
        /// <summary>
        /// Update the calendar events for pairings, and call UpdateDutyRecords for FPG pairings.
        /// </summary>
        /// <param name="pairings">A list of pairings to update.</param>
        /// <returns>An awaitable Task.</returns>
        Task UpdatePairings(IEnumerable<Models.Merlot.Pairing> pairings);
    }

    public class PAL : IPAL
    {
        private readonly ILogger<PAL> _logger;
        private readonly Config _config;
        private Core.Domains.Dataverse.IDataverseDuty _duties;
        private Core.Domains.Dataverse.IDataverseFlight _flights;
        private Core.Domains.Dataverse.IDataverseCrew _crew;
        private Core.Domains.Merlot.IMerlotFlight _flightInfo;
        private Core.Domains.Merlot.IMerlotEmployee _employees;
        private Core.Domains.Graph.IGraphUserDomain _users;
        private Core.Domains.Graph.IGraphTeamDomain _teams;
        private Core.Domains.Graph.IGraphCalendarDomain _calendar;

        private bool onlyTestUsers = true;
        List<Models.Dataverse.Crew> testUsers = new();

        public PAL() { }
        public PAL(ILogger<PAL> logger, Config config, Core.Domains.Dataverse.IDataverseDuty duties, Core.Domains.Dataverse.IDataverseFlight flights, Core.Domains.Dataverse.IDataverseCrew crew, Core.Domains.Merlot.IMerlotFlight flightInfo, Core.Domains.Merlot.IMerlotEmployee employees, Core.Domains.Graph.IGraphUserDomain users, Core.Domains.Graph.IGraphTeamDomain teams, Core.Domains.Graph.IGraphCalendarDomain calendar)
        {
            _logger = logger;
            _config = config;
            _duties = duties;
            _flights = flights;
            _crew = crew;
            _flightInfo = flightInfo;
            _employees = employees;
            _users = users;
            _teams = teams;
            _calendar = calendar;


            if (!bool.TryParse(_config.GetValue("App:OnlyTestUsers"), out onlyTestUsers))
            {
                throw new ArgumentNullException(nameof(onlyTestUsers), "Unable to get configuration value for App:OnlyTestUsers");
            }
            else if (onlyTestUsers)
            {
                testUsers = _crew.GetMultipleByAADId(_config.GetValue("App:TestUsers").Split('\n'));
            }
        }

        public async Task UpdateFlightRecords(IEnumerable<Models.Merlot.Flight> activeFlights)
        {
            List<Models.Merlot.Flight> flights = activeFlights.Where(f => !f.cancelled && f.crew.Count() > 0).ToList();

            List<Models.Dataverse.Flight> fromDataverse = _flights.GetMultiple(flights.Select(flight => flight.followId));
            DateTime lastCheck = DateTime.UtcNow.AddMinutes(-30);

            var flightsToCreate = flights.Where(f => fromDataverse.All(e => !e.FollowId.Equals(f.followId))).ToList();
            var flightsToUpdate = flights.Where(f => fromDataverse.All(e => e.FollowId.Equals(f.followId) && f.UpdatedDate > lastCheck)).ToList();

            if (flightsToCreate.Count() == 0 && flightsToUpdate.Count() == 0)
            {
                return;
            }

            List<Microsoft.Graph.Event> eventsToCreate = flightsToCreate.Select(flight => flight.ToEvent()).ToList();
            eventsToCreate.AddRange(flightsToUpdate.Where(flight =>
                fromDataverse.Any(dataverse => flight.followId.Equals(dataverse.FollowId) && string.IsNullOrWhiteSpace(dataverse.EventId)))
                .Select(flight => flight.ToEvent()));
            List<Microsoft.Graph.Event> eventsToUpdate = flightsToUpdate.Where(flight =>
                fromDataverse.Any(dataverse => flight.followId.Equals(dataverse.FollowId) && !string.IsNullOrWhiteSpace(dataverse.EventId)))
                .Select(flight =>
                {
                    var ev = flight.ToEvent();
                    ev.Id = fromDataverse.First(dataverse => dataverse.FollowId.Equals(flight.followId)).EventId;
                    return ev;
                }).ToList();

            if (onlyTestUsers)
            {
                eventsToCreate.ForEach(ev =>
                {
                    var flight = flightsToCreate.First(flight => flight.followId.ToString().Equals(ev.TransactionId));
                    ev.Attendees = testUsers.Where(user => flight.crew.Any(crew => crew.empCode.Equals(user.EmployeeId))).Select(user => user.ToAttendee());
                });

                eventsToUpdate.ForEach(ev =>
                {
                    var eventId = ev.Id;
                    var flight = flightsToUpdate.First(flight => flight.followId.ToString().Equals(ev.TransactionId));
                    ev = flight.ToEvent();
                    ev.Attendees = testUsers.Where(user => flight.crew.Any(crew => crew.empCode.Equals(user.EmployeeId))).Select(user => user.ToAttendee());
                });
            }
            else
            {
                eventsToCreate.ForEach(ev =>
                    ev.Attendees = flightsToCreate.First(flight => flight.followId.ToString().Equals(ev.TransactionId)).crew.Select(crew => crew.ToAttendee())
                );

                eventsToUpdate.ForEach(ev =>
                {
                    var eventId = ev.Id;
                    ev = flightsToUpdate.First(flight => flight.followId.ToString().Equals(ev.TransactionId)).ToEvent();
                    ev.Attendees = flightsToUpdate.First(flight => flight.followId.ToString().Equals(ev.TransactionId)).crew.Select(crew => crew.ToAttendee());
                });
            }


            List<Microsoft.Graph.Event> createdEvents = new();
            List<Microsoft.Graph.Event> updatedEvents = new();

            if (eventsToCreate.Count > 0)
            {
                createdEvents = await _calendar.CreateMultiple(eventsToCreate);
            }

            if (eventsToUpdate.Count > 0)
            {
                updatedEvents = await _calendar.UpdateMultiple(eventsToUpdate);
            }

            List<Models.Dataverse.Flight> toCreate = flightsToCreate.Select(flight => new Models.Dataverse.Flight(flight)).ToList();
            List<Models.Dataverse.Flight> toUpdate = fromDataverse;

            toCreate.ForEach(flight =>
            {
                var ev = createdEvents.FirstOrDefault(ev => ev.TransactionId.Equals(flight.FollowId.ToString()));
                if (ev is not null)
                    flight.EventId = ev.Id;
            });
            toUpdate.ForEach(flight =>
            {
                if (string.IsNullOrWhiteSpace(flight.EventId))
                {
                    var ev = createdEvents.FirstOrDefault(ev => ev.TransactionId.Equals(flight.FollowId.ToString()));
                    if (ev is not null)
                        flight.EventId = ev.Id;
                }
            });

            if (toCreate.Count() > 0)
                _flights.CreateMultiple(toCreate);

            if (toUpdate.Count() > 0)
                _flights.UpdateMultiple(toUpdate);

            _logger.LogInformation("UpdateFlightRecords: finished processing.");
        }


        public async Task UpdateDutyRecords(IEnumerable<Models.Merlot.Pairing> dutyPairings)
        {
            List<Models.Merlot.Pairing> duties = dutyPairings.Where(p => p.activeFlagId.Equals(2) && p.pairingWorkTypeId.Equals(3)).ToList();
            _duties.UpsertMultiple(duties.Select(p => new Models.Dataverse.Duty(p)));
        }

        public async Task UpdateCrewRecords(IEnumerable<Models.Merlot.PairingEmployee> employees)
        {
            employees = employees.DistinctBy(e => e.empCode);
            List<Models.Dataverse.Crew> fromDataverse = _crew.GetMultiple(employees.Select(e => e.empCode));
            List<Models.Merlot.PairingEmployee> toAdd = employees.Where(e => fromDataverse.All(d => d.EmployeeId != e.empCode)).ToList();

            if (toAdd.Count() == 0)
                return;

            List<Models.Merlot.Employee> fromMerlot = new();
            List<Task> tasks = new();
            SemaphoreSlim throttler = new(20);

            foreach (var e in toAdd)
            {
                await throttler.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        fromMerlot.Add(await _employees.Get(e.empCode));
                    }
                    finally
                    {
                        throttler.Release();
                    }
                }));
            }
            await Task.WhenAll(tasks);

            var emails = fromMerlot.Select(m =>
            {
                if (m.employeeEmails.Count() == 0)
                {
                    _logger.LogWarning($"Employee {m.knownAs} ({m.empCode}) does not have any emails.");
                    return null;
                }
                var primary = m.employeeEmails.FirstOrDefault(e => e.primary);
                if (primary is not null)
                    return primary.email;

                return m.employeeEmails.First().email;
            }).ToList();
            emails.RemoveAll(e => string.IsNullOrWhiteSpace(e));
            List<Microsoft.Graph.User> fromGraph = await _users.GetMultiple(emails);

            List<Models.Dataverse.Crew> newCrewRecords = fromMerlot.Select(c => new Models.Dataverse.Crew(c)).ToList();
            newCrewRecords.ForEach(crew =>
            {
                crew.Rank = toAdd.First(e => e.empCode.Equals(crew.EmployeeId)).rank;
                var user = fromGraph.FirstOrDefault(e => e.Mail.Equals(crew.Email));
                if (user is not null)
                    crew.AadUserId = fromGraph.FirstOrDefault(e => e.Mail.Equals(crew.Email)).Id;
            });

            _crew.CreateMultiple(newCrewRecords);
            _logger.LogInformation($"UpdateCrew: finished processing {toAdd.Count()} new crew records.");
        }

        public async Task UpdateCrewRecords(IEnumerable<Models.Merlot.FlightCrew> crew)
        {
            crew = crew.DistinctBy(e => e.empCode);
            List<Models.Dataverse.Crew> fromDataverse = _crew.GetMultiple(crew.Select(e => e.empCode));
            List<Models.Merlot.FlightCrew> toAdd = crew.Where(c => fromDataverse.All(d => d.EmployeeId != c.empCode)).ToList();

            if (toAdd.Count() == 0)
                return;

            List<Models.Merlot.Employee> fromMerlot = new();
            List<Task> tasks = new();
            SemaphoreSlim throttler = new(20);

            foreach (var e in toAdd)
            {
                await throttler.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        fromMerlot.Add(await _employees.Get(e.empCode));
                    }
                    finally
                    {
                        throttler.Release();
                    }
                }));
            }
            await Task.WhenAll(tasks);

            var emails = fromMerlot.Select(m =>
            {
                if (m.employeeEmails.Count() == 0)
                {
                    _logger.LogWarning($"Employee {m.knownAs} ({m.empCode}) does not have any emails.");
                    return null;
                }
                var primary = m.employeeEmails.FirstOrDefault(e => e.primary);
                if (primary is not null)
                    return primary.email;

                return m.employeeEmails.First().email;
            }).ToList();
            emails.RemoveAll(e => string.IsNullOrWhiteSpace(e));
            List<Microsoft.Graph.User> fromGraph = await _users.GetMultiple(emails);

            List<Models.Dataverse.Crew> newCrewRecords = fromMerlot.Select(c => new Models.Dataverse.Crew(c)).ToList();
            newCrewRecords.ForEach(crew =>
            {
                crew.Rank = toAdd.First(e => e.empCode.Equals(crew.EmployeeId)).rank;
                var user = fromGraph.FirstOrDefault(e => e.Mail.Equals(crew.Email));
                if (user is not null)
                    crew.AadUserId = fromGraph.FirstOrDefault(e => e.Mail.Equals(crew.Email)).Id;
            });

            _crew.CreateMultiple(newCrewRecords);
            _logger.LogInformation($"UpdateCrew: finished processing {toAdd.Count()} new crew records.");
        }

        public async Task HandleCancelledFlights(IEnumerable<Models.Merlot.Flight> cancelledFlights)
        {
            List<Models.Merlot.Flight> flights = cancelledFlights.Where(f => f.cancelled).ToList();
            List<Models.Dataverse.Flight> fromDataverse = _flights.GetMultiple(flights.Select(f => f.followId));

            await _calendar.CancelMultiple(fromDataverse.Where
            (flight => !string.IsNullOrWhiteSpace(flight.EventId) && flight.FlightNumber.Contains("(cancelled)")).Select(flight => flight.EventId));

            fromDataverse.ForEach(flight =>
            {
                flight.FlightNumber = $"(cancelled) {flight.FlightNumber}";
                // flight.EventId = null;
            });

            _flights.UpdateMultiple(fromDataverse);
            _logger.LogInformation("HandleCancelledFlights: finished processing.");
        }

        public async Task UpdateFlightTeams(IEnumerable<Models.Dataverse.Flight> flights)
        {
            List<Models.Merlot.Flight> fromMerlot = await _flightInfo.GetManyById(flights.Select(f => f.FollowId));

            List<Models.Dataverse.Flight> toCreate = flights.Where(flight => string.IsNullOrWhiteSpace(flight.TeamId)).ToList();
            List<Models.Dataverse.Flight> toUpdate = flights.Where(flight => !string.IsNullOrWhiteSpace(flight.TeamId)).ToList();

            string owner = _config.GetValue("Teams:Owner");
            string[] additionalMembers = { };
            if (!string.IsNullOrWhiteSpace(_config.GetValue("Teams:AdditionalMembers")))
                additionalMembers = _config.GetValue("Teams:AdditionalMembers").Split('\n');

            List<Models.Dataverse.Flight> created = new();
            List<Models.Dataverse.Flight> updated = new();

            Dictionary<string, IEnumerable<string>> membersToAdd = new();
            foreach (var flight in toCreate)
            {
                var flightInfo = fromMerlot.First(m => m.followId.Equals(flight.FollowId));
                var crew = flight.OperatingCrew.Concat(flight.NonOperatingCrew);
                List<string> members = new();
                if (onlyTestUsers)
                    members.AddRange(testUsers.Where(user => crew.Any(c => c.empCode.Equals(user.EmployeeId))).Select(user => user.AadUserId));
                else
                {
                    var crewInfo = _crew.GetMultiple(flight.OperatingCrew.Concat(flight.NonOperatingCrew).Select(c => c.empCode));
                    members.AddRange(crewInfo.Select(c => c.AadUserId));
                }
                var team = flightInfo.ToTeam(owner);
                flight.TeamId = await _teams.Create(team);
                var m = members.Concat(additionalMembers).Distinct();
                if (m.Count() > 0)
                    membersToAdd.Add(flight.TeamId, m);
                created.Add(flight);
            }


            foreach (var flight in toUpdate)
            {
                var flightInfo = fromMerlot.First(m => m.followId.Equals(flight.FollowId));
                var crew = flight.OperatingCrew.Concat(flight.NonOperatingCrew);
                List<string> members = new();
                if (onlyTestUsers)
                    members.AddRange(testUsers.Where(user => crew.Any(c => c.empCode.Equals(user.EmployeeId))).Select(user => user.AadUserId));
                else
                {
                    var crewInfo = _crew.GetMultiple(flight.OperatingCrew.Concat(flight.NonOperatingCrew).Select(c => c.empCode));
                    members.AddRange(crewInfo.Select(c => c.AadUserId));
                }
                var team = flightInfo.ToTeam(owner);
                team.Id = flight.TeamId;
                await _teams.Update(team);
                var m = members.Concat(additionalMembers).Distinct();
                if (m.Count() > 0)
                    membersToAdd.Add(team.Id, m);
                updated.Add(flight);
            }

            if (membersToAdd.Count() > 0)
                await _teams.AddMembersMultiple(membersToAdd);

            _flights.UpdateMultiple(created);
        }

        public async Task ArchiveTeams(IEnumerable<Models.Dataverse.Flight> flights)
        {
            if (flights.Count() > 0)
            {
                await _teams.ArchiveMultiple(flights.Where(flight => !string.IsNullOrWhiteSpace(flight.TeamId)).Select(flight => flight.TeamId));
                _flights.DeleteMultiple(flights.Select(flight => flight.Id.Value));
            }
        }


        public async Task UpdateFlights(IEnumerable<Models.Merlot.Flight> flights)
        {
            var active = flights.Where(flight => !flight.cancelled);
            var cancelled = flights.Where(flight => flight.cancelled);

            if (active.Count() > 0)
                await UpdateFlightRecords(flights);
            if (cancelled.Count() > 0)
                await HandleCancelledFlights(cancelled);
        }

        public async Task UpdatePairings(IEnumerable<Models.Merlot.Pairing> pairings)
        {
            // exclude FPG (3), RES (2), TRN (12)
            var individualDuties = pairings.Where(p => p.pairingEmployees.Count() == 1 && p.pairingWorkTypeId != 3 && p.pairingWorkTypeId != 2 && p.pairingWorkTypeId != 12).ToList();
            // exclude FPG, RES
            var groupDuties = pairings.Where(p => p.pairingWorkTypeId != 3 && p.pairingWorkTypeId != 2 && p.pairingEmployees.Count() > 1).ToList();
            // only FPG
            var flightDuties = pairings.Where(p => p.pairingWorkTypeId.Equals(3)).ToList();

            var testUserEmpCodes = testUsers.Select(user => user.EmployeeId);


            if (onlyTestUsers)
            {
                individualDuties = individualDuties.Where(duty => duty.pairingEmployees.Any(employee => testUserEmpCodes.Contains(employee.empCode))).ToList();
                groupDuties = groupDuties.Where(duty => duty.pairingEmployees.Any(employee => testUserEmpCodes.Contains(employee.empCode))).ToList();
                // flightDuties = flightDuties.Where(duty => duty.pairingEmployees.Any(employee => testUserEmpCodes.Contains(employee.empCode))).ToList();
            }

            List<Microsoft.Graph.Event> events = new();
            List<Microsoft.Graph.Event> groupEvents = new();

            if (onlyTestUsers)
            {
                individualDuties.ForEach(duty =>
                {
                    var e = duty.ToEvent();
                    e.Attendees = testUsers.Where(user => duty.pairingEmployees.Any(employee => employee.empCode.Equals(user.EmployeeId))).Select(user => user.ToAttendee());
                    events.Add(e);
                });
                groupDuties.ForEach(duty =>
                {
                    var e = duty.ToEvent();
                    e.Attendees = testUsers.Where(user => duty.pairingEmployees.Any(employee => employee.empCode.Equals(user.EmployeeId))).Select(user => user.ToAttendee());
                    groupEvents.Add(e);
                });
            }
            else
            {
                // TODO
            }

            Console.WriteLine(JsonConvert.SerializeObject(events));

            await Task.WhenAll(_calendar.CreateMultiple(events), _calendar.CreateMultiple(groupEvents));
            _logger.LogInformation($"Processed {events.Count()} individual events, {groupEvents.Count()} group events.");

            // add flight duties to dataverse
            _duties.UpsertMultiple(flightDuties.Select(p => new Core.Models.Dataverse.Duty(p)));
        }
    }
}
