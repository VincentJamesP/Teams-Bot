using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

using KTI.PAL.Teams.Core.Services;
using KTI.PAL.Teams.Core.Models.Dataverse;
using KTI.PAL.Teams.Core.Models;

namespace KTI.PAL.Teams.Core.Domains.Dataverse
{
    public interface IDataverseFlight
    {
        /// <summary>
        /// Get a single flight from the Dataverse table.
        /// </summary>
        /// <param name="id">The Follow ID of the flight to retrieve.</param>
        /// <returns>The flight record whose Follow ID matches the provided one; null if the entry doesn't exist.</returns>
        Models.Dataverse.Flight Get(int id);
        /// <summary>
        /// Get flights from dataverse by follow id.
        /// </summary>
        /// <param name="ids">The list of flight follow ids to fetch.</param>
        /// <returns>A list of flight records whose follow id matches.</returns>
        List<Models.Dataverse.Flight> GetMultiple(IEnumerable<int> ids);

        /// <summary>
        /// Add an entry to the Dataverse table.
        /// </summary>
        /// <param name="flight">The flight from Merlot to create, cast into a Dataverse.Flight object.</param>
        /// <returns></returns>
        Guid Create(Models.Dataverse.Flight flight);
        /// <summary>
        /// Add multiple flight records to the Dataverse table.
        /// </summary>
        /// <param name="flights">A list of flight records to add.</param>
        /// <returns>The Guids of the created entries.</returns>
        List<Guid> CreateMultiple(IEnumerable<Models.Dataverse.Flight> flights);

        /// <summary>
        /// Update a flight record in the Dataverse table.
        /// </summary>
        /// <param name="flight">The flight to update. Flight.Id must not be null.</param>
        void Update(Models.Dataverse.Flight flight);
        /// <summary>
        /// Update multiple flight records in the Dataverse table.
        /// </summary>
        /// <param name="flights">A list of flights to update.</param>
        void UpdateMultiple(IEnumerable<Models.Dataverse.Flight> flights);

        /// <summary>
        /// Remove a single flight record from the Dataverse table.
        /// </summary>
        /// <param name="id">The Guid of the flight to remove.</param>
        void Delete(Guid id);
        /// <summary>
        /// Remove multiple flight records from the Dataverse table.
        /// </summary>
        /// <param name="ids">The Guids of the flights to remove.</param>
        void DeleteMultiple(IEnumerable<Guid> ids);

        /// <summary>
        /// Either create or update a flight's entry in the Dataverse table.
        /// Makes an additional IDataverseFlight.Get call to check the entry exists.
        /// </summary>
        /// <param name="duty">The flight to upsert.</param>
        /// <returns>A Guid if an entry was created, null otherwise.</returns>
        Guid? Upsert(Models.Dataverse.Flight flight);
        /// <summary>
        /// Create or Update multiple flights' entries in the Dataverse table.
        /// Makes an additional IDataverseFlight.GetMultiple call to check if the entries exist.
        /// </summary>
        /// <param name="duties">A list of flights to upsert.</param>
        /// <returns>A list of Guids of created entries. Returns an empty list if no entries were created.</returns>
        List<Guid> UpsertMultiple(IEnumerable<Models.Dataverse.Flight> flights);

        /// <summary>
        /// Get a list of flights containing specific crew.
        /// </summary>
        /// <param name="crewIds">A list of Merlot empCodes.</param>
        /// <returns>A list of flights where at least one of the assigned crew's empCode is found in the provided crewIds.</returns>
        List<Models.Dataverse.Flight> GetContainingCrew(string[] crewIds);
        /// <summary>
        /// Get a list of flights within a specific time span.
        /// </summary>
        /// <param name="span">How long in the future to fetch flights for.</param>
        /// <returns>A list of flights whose Arrival and Departure times fall within the provided time span.</returns>
        List<Models.Dataverse.Flight> GetWithin(TimeSpan span);
        /// <summary>
        /// Get a list of flights from the past.
        /// </summary>
        /// <returns>A list of flights where Flight.ScheduledArrival is over 48 hours ago.</returns>
        List<Models.Dataverse.Flight> GetFinished();
    }

    public class Flights : IDataverseFlight
    {
        private readonly ILogger<Flights> _logger;
        private readonly IDataverseService _service;
        private readonly string prefix;
        private readonly string tableName;
        private readonly string[] columns;

        public Flights() { }

        public Flights(ILogger<Flights> logger, IDataverseService service, Config config)
        {
            _logger = logger;
            _service = service;
            prefix = config.GetValue("Dataverse:Prefix");
            tableName = config.GetValue("Dataverse:FlightTable");
            columns = new string[] { $"{prefix}eventid", $"{prefix}flightnumber", $"{prefix}followid", $"{prefix}lastmerlotupdate", $"{prefix}nonoperatingcrew", $"{prefix}operatingcrew", $"{prefix}scheduledarrival", $"{prefix}scheduleddeparture", $"{prefix}teamid", $"{prefix}{tableName}id" };
        }


        public Models.Dataverse.Flight Get(int id)
        {
            ConditionExpression condition = new()
            {
                AttributeName = $"{prefix}followid",
                Operator = ConditionOperator.Equal,
                Values = { id }
            };

            QueryExpression query = new($"{prefix}{tableName}");
            query.ColumnSet.AddColumns(columns);
            query.Criteria.AddCondition(condition);

            // need to use RetrieveMultiple since that's the only Retrieve method that allows search criteria
            var response = _service.GetClient().RetrieveMultiple(query);

            if (response.Entities.Count == 0)
                return null;

            return new Models.Dataverse.Flight(response.Entities.First(), prefix, tableName);
        }

        public List<Models.Dataverse.Flight> GetMultiple(IEnumerable<int> ids)
        {
            var batches = ids.Chunk(1000);
            List<Models.Dataverse.Flight> results = new();
            uint b = 0;

            foreach (var batch in batches)
            {
                b++;
                ConditionExpression condition = new($"{prefix}followid", ConditionOperator.In, batch);

                QueryExpression query = new($"{prefix}{tableName}");
                query.ColumnSet.AddColumns(columns);
                query.Criteria.AddCondition(condition);

                var response = _service.GetClient().RetrieveMultiple(query);

                if (response.Entities.Count > 0)
                    results.AddRange(response.Entities.Select(e => new Models.Dataverse.Flight(e, prefix, tableName)).ToList());
            }

            return results;
        }

        public Guid Create(Models.Dataverse.Flight flight)
        {
            Microsoft.Xrm.Sdk.Entity entity = flight.ToEntity(tableName, prefix);

            return _service.GetClient().Create(entity);
        }

        public List<Guid> CreateMultiple(IEnumerable<Models.Dataverse.Flight> flights)
        {
            var batches = flights.Chunk(1000);
            List<Guid> results = new();
            uint b = 0;

            foreach (var batch in batches)
            {
                b++;
                List<Microsoft.Xrm.Sdk.Entity> entities = batch.Select(flight => flight.ToEntity(tableName, prefix)).ToList();

                ExecuteMultipleRequest requests = new()
                {
                    Settings = new ExecuteMultipleSettings
                    {
                        ContinueOnError = false,
                        ReturnResponses = true
                    },
                    Requests = new OrganizationRequestCollection()
                };

                requests.Requests.AddRange(entities.Select(e =>
                    new CreateRequest() { Target = e }
                ));

                ExecuteMultipleResponse responses = (ExecuteMultipleResponse)_service.GetClient().Execute(requests);

                if (responses.IsFaulted)
                {
                    throw new DataverseException(responses.Responses.First().Fault.Message);
                }

                results.AddRange(responses.Responses.Select(r => ((CreateResponse)r.Response).id).ToList());
                _logger.LogInformation($"Created {batch.Count()} entities.");
            }
            return results;
        }

        public void Delete(Guid id)
        {
            _service.GetClient().Delete($"{prefix}{tableName}", id);
        }

        public void DeleteMultiple(IEnumerable<Guid> ids)
        {
            var batches = ids.Chunk(1000);
            uint b = 0;

            foreach (var batch in batches)
            {
                b++;
                ExecuteMultipleRequest requests = new()
                {
                    Settings = new ExecuteMultipleSettings
                    {
                        ContinueOnError = true,
                        ReturnResponses = true
                    },
                    Requests = new OrganizationRequestCollection()
                };

                requests.Requests.AddRange(batch.Select(id => new DeleteRequest() { Target = new EntityReference($"{prefix}{tableName}", id) }));
                var response = _service.GetClient().Execute(requests);
            }
        }

        public void Update(Models.Dataverse.Flight flight)
        {
            Microsoft.Xrm.Sdk.Entity entity = flight.ToUpdateEntity(tableName, prefix);

            _service.GetClient().Update(entity);
        }

        public void UpdateMultiple(IEnumerable<Models.Dataverse.Flight> flights)
        {
            var batches = flights.Chunk(1000);
            uint b = 0;

            foreach (var batch in batches)
            {
                b++;
                List<Microsoft.Xrm.Sdk.Entity> entities = batch.Select(flight => flight.ToUpdateEntity(tableName, prefix)).ToList();

                ExecuteMultipleRequest requests = new()
                {
                    Settings = new ExecuteMultipleSettings
                    { ContinueOnError = false },
                    Requests = new OrganizationRequestCollection()
                };

                requests.Requests.AddRange(entities.Select(e => new UpdateRequest() { Target = e }));
                _service.GetClient().Execute(requests);

                _logger.LogInformation($"Updated {entities.Count} entities.");
            }
        }

        public Guid? Upsert(Models.Dataverse.Flight flight)
        {
            var entity = Get(flight.FollowId);
            if (entity is null)
                return Create(flight);
            else
                Update(flight);
            return null;
        }

        public List<Guid> UpsertMultiple(IEnumerable<Models.Dataverse.Flight> flights)
        {
            List<Guid> responses = new();

            var entities = GetMultiple(flights.Select(f => f.FollowId));

            var toCreate = flights.Where(f => entities.All(e => e.FollowId != f.FollowId && !f.Id.HasValue)).ToList();
            var toUpdate = entities.Select(e =>
            {
                var flight = flights.Where(f => f.FollowId.Equals(e.FollowId) || f.Id.Equals(e.Id)).FirstOrDefault();
                if (flight is not null && (flight.LastMerlotUpdate - e.LastMerlotUpdate).TotalSeconds > 1)
                {
                    flight.Id = e.Id;
                    return flight;
                }
                return null;
            }).ToList();
            toUpdate.RemoveAll(f => f is null);

            if (toUpdate.Count == 0 && toCreate.Count == 0)
            {
                _logger.LogInformation($"No operations to perform.");
                return new List<Guid>();
            }

            if (toUpdate.Count > 0)
                UpdateMultiple(toUpdate);
            if (toCreate.Count > 0)
                responses.AddRange(CreateMultiple(toCreate));

            if (responses.Count > 0)
                return responses;

            return new List<Guid>();
        }


        private List<Core.Models.Dataverse.Flight> _get(FilterExpression filter = null, params ConditionExpression[] conditions)
        {
            QueryExpression query = new($"{prefix}{tableName}");
            query.ColumnSet.AddColumns(columns);
            foreach (ConditionExpression condition in conditions)
                query.Criteria.AddCondition(condition);

            if (filter is not null)
                query.Criteria.AddFilter(filter);

            var response = _service.GetClient().RetrieveMultiple(query);

            if (response.Entities.Count == 0)
                return new List<Models.Dataverse.Flight>();

            List<Core.Models.Dataverse.Flight> flights = response.Entities.Select(e =>
            {
                return new Core.Models.Dataverse.Flight(e, prefix, tableName);
            }).ToList();

            return flights;
        }

        public List<Core.Models.Dataverse.Flight> GetContainingCrew(string[] crewIds)
        {
            DateTime today = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);
            FilterExpression crewlist = new(LogicalOperator.Or);
            crewlist.AddCondition($"{prefix}operatingcrew", ConditionOperator.Contains, crewIds);
            crewlist.AddCondition($"{prefix}nonoperatingcrew", ConditionOperator.Contains, crewIds);

            FilterExpression dates = new(LogicalOperator.Or);
            dates.AddCondition($"{prefix}scheduledarrival", ConditionOperator.OnOrAfter, today);
            dates.AddCondition($"{prefix}scheduleddeparture", ConditionOperator.OnOrAfter, today);

            FilterExpression filter = new(LogicalOperator.Or);
            filter.AddFilter(crewlist);
            filter.AddFilter(dates);

            var flight = _get(filter);
            return flight.Where(f => f.OperatingCrew.Any(c => crewIds.Any(i => c.empCode.Equals(i))) || f.NonOperatingCrew.Any(c => crewIds.Any(i => c.empCode.Equals(i)))).ToList();
        }

        public List<Core.Models.Dataverse.Flight> GetWithin(TimeSpan span)
        {
            ConditionExpression condition = new();
            condition.AttributeName = $"{prefix}scheduleddeparture";
            condition.Operator = ConditionOperator.NextXHours;
            condition.Values.Add(span.TotalHours.ToString());

            return _get(null, condition);
        }

        public List<Models.Dataverse.Flight> GetUpcoming()
        {
            ConditionExpression condition = new();
            condition.AttributeName = $"{prefix}scheduleddeparture";
            condition.Operator = ConditionOperator.OnOrAfter;
            condition.Values.Add(DateTime.UtcNow);

            return _get(null, condition);
        }

        public List<Models.Dataverse.Flight> GetFinished()
        {
            ConditionExpression condition = new();
            condition.AttributeName = $"{prefix}scheduledarrival";
            condition.Operator = ConditionOperator.OnOrBefore;
            condition.Values.Add(DateTime.UtcNow.AddHours(-48));

            return _get(null, condition);
        }
    }
}
